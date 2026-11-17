using System;
using HomelessPetHelper.Modules.WorldTime;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class ActionWorldClockTests
    {
        private Guid _sessionId;
        private ActionWorldClock _clock;

        [SetUp]
        public void SetUp()
        {
            _sessionId = Guid.NewGuid();
            _clock = new ActionWorldClock(_sessionId, 1440);
        }

        [Test]
        public void NewClockStartsAtExplicitGameTime()
        {
            Assert.That(_clock.Current.ElapsedMinutes, Is.Zero);
            Assert.That(_clock.Current.MinuteOfDay, Is.Zero);
            Assert.That(_clock.Current.DayIndex, Is.Zero);
            Assert.That(_clock.Current.Version.SessionId, Is.EqualTo(_sessionId));
            Assert.That(_clock.IsClosed, Is.False);
            Assert.That(_clock.IsSuspended, Is.False);
        }

        [Test]
        public void InvalidConstructionIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new ActionWorldClock(Guid.Empty, 1440));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActionWorldClock(_sessionId, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActionWorldClock(_sessionId, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActionWorldClock(_sessionId, 1440, -1));
        }

        [Test]
        public void CompletedActionAdvancesOnceAndReturnsImmutableBeforeAndAfter()
        {
            var before = _clock.Current;
            Assert.That(_clock.TryAdvance(before.Version, 45, out var change, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(WorldTimeFailure.None));
            Assert.That(change.Before, Is.SameAs(before));
            Assert.That(change.After, Is.SameAs(_clock.Current));
            Assert.That(change.AdvancedMinutes, Is.EqualTo(45));
            Assert.That(before.ElapsedMinutes, Is.Zero);
            Assert.That(_clock.Current.ElapsedMinutes, Is.EqualTo(45));
            Assert.That(_clock.Current.MinuteOfDay, Is.EqualTo(45));
        }

        [Test]
        public void RepeatedCompletionWithCapturedVersionIsRejectedWithoutSecondAdvance()
        {
            var version = _clock.Current.Version;
            Assert.That(_clock.TryAdvance(version, 45, out _, out _), Is.True);
            var snapshot = _clock.Current;
            Assert.That(_clock.TryAdvance(version, 45, out var change, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(WorldTimeFailure.StaleVersion));
            Assert.That(change, Is.Null);
            Assert.That(_clock.Current, Is.SameAs(snapshot));
        }

        [Test]
        public void DistinctActionsCanHaveDifferentDurations()
        {
            Assert.That(_clock.TryAdvance(_clock.Current.Version, 120, out var first, out _), Is.True);
            Assert.That(_clock.TryAdvance(_clock.Current.Version, 15, out var second, out _), Is.True);
            Assert.That(_clock.Current.ElapsedMinutes, Is.EqualTo(135));
            Assert.That(first.AdvancedMinutes, Is.EqualTo(120));
            Assert.That(second.AdvancedMinutes, Is.EqualTo(15));
            Assert.That(first.After.ElapsedMinutes, Is.EqualTo(120));
        }

        [TestCase(1439, 0, 1439)]
        [TestCase(1440, 1, 0)]
        [TestCase(1441, 1, 1)]
        [TestCase(4505, 3, 185)]
        public void ExplicitAdvanceCanCrossDayBoundaries(long minutes, long dayIndex, int minuteOfDay)
        {
            Assert.That(_clock.TryAdvance(_clock.Current.Version, minutes, out _, out _), Is.True);
            Assert.That(_clock.Current.DayIndex, Is.EqualTo(dayIndex));
            Assert.That(_clock.Current.MinuteOfDay, Is.EqualTo(minuteOfDay));
            Assert.That(_clock.Current.MinutesPerDay, Is.EqualTo(1440));
        }

        [Test]
        public void DayLengthComesFromCallerRatherThanThreeBackgroundSlots()
        {
            var clock = new ActionWorldClock(_sessionId, 12, 11);
            Assert.That(clock.TryAdvance(clock.Current.Version, 2, out _, out _), Is.True);
            Assert.That(clock.Current.DayIndex, Is.EqualTo(1));
            Assert.That(clock.Current.MinuteOfDay, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(long.MinValue)]
        public void InvalidDurationChangesNoTime(long duration)
        {
            var snapshot = _clock.Current;
            Assert.That(_clock.TryAdvance(snapshot.Version, duration, out var change, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(WorldTimeFailure.InvalidDuration));
            Assert.That(change, Is.Null);
            Assert.That(_clock.Current, Is.SameAs(snapshot));
            Assert.That(_clock.TryAdvance(snapshot.Version, 1, out _, out _), Is.True);
        }

        [Test]
        public void WrongOrEmptySessionChangesNoTime()
        {
            foreach (Guid id in new[] { Guid.Empty, Guid.NewGuid() })
            {
                Assert.That(_clock.TryAdvance(new WorldTimeVersion(id, 0), 1,
                    out var change, out var failure), Is.False);
                Assert.That(change, Is.Null);
                Assert.That(failure, Is.EqualTo(WorldTimeFailure.WrongSession));
            }

            Assert.That(_clock.Current.ElapsedMinutes, Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(1)]
        [TestCase(long.MaxValue)]
        public void IncorrectExpectedTimeIsRejected(long expectedMinutes)
        {
            Assert.That(_clock.TryAdvance(new WorldTimeVersion(_sessionId, expectedMinutes), 1,
                out var change, out var failure), Is.False);
            Assert.That(change, Is.Null);
            Assert.That(failure, Is.EqualTo(WorldTimeFailure.StaleVersion));
            Assert.That(_clock.Current.ElapsedMinutes, Is.Zero);
        }

        [Test]
        public void SuspensionAndResumeDoNotCatchUpHiddenTime()
        {
            var snapshot = _clock.Current;
            _clock.SetSuspended(true);
            _clock.SetSuspended(true);
            Assert.That(_clock.TryAdvance(snapshot.Version, 100, out var change, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(WorldTimeFailure.Suspended));
            Assert.That(change, Is.Null);
            _clock.SetSuspended(false);
            Assert.That(_clock.Current, Is.SameAs(snapshot));
            Assert.That(_clock.TryAdvance(snapshot.Version, 5, out _, out _), Is.True);
            Assert.That(_clock.Current.ElapsedMinutes, Is.EqualTo(5));
        }

        [Test]
        public void ReadingSnapshotsDoesNotAdvanceTime()
        {
            var snapshot = _clock.Current;
            for (int index = 0; index < 100; index++)
            {
                Assert.That(_clock.Current.MinuteOfDay, Is.Zero);
                Assert.That(_clock.Current.DayIndex, Is.Zero);
                Assert.That(_clock.Current.Version.ElapsedMinutes, Is.Zero);
            }

            Assert.That(_clock.Current, Is.SameAs(snapshot));
        }

        [Test]
        public void ClosedClockRejectsActionsEvenAfterResume()
        {
            var snapshot = _clock.Current;
            _clock.Close();
            _clock.Close();
            _clock.SetSuspended(false);
            Assert.That(_clock.TryAdvance(snapshot.Version, 1, out var change, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(WorldTimeFailure.Closed));
            Assert.That(change, Is.Null);
            Assert.That(_clock.Current, Is.SameAs(snapshot));
        }

        [Test]
        public void MaximumTimeIsRepresentableButCannotOverflow()
        {
            var clock = new ActionWorldClock(_sessionId, 1440, long.MaxValue - 1);
            var version = clock.Current.Version;
            Assert.That(clock.TryAdvance(version, 2, out var change, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(WorldTimeFailure.TimeOverflow));
            Assert.That(change, Is.Null);
            Assert.That(clock.Current.ElapsedMinutes, Is.EqualTo(long.MaxValue - 1));
            Assert.That(clock.TryAdvance(version, 1, out _, out _), Is.True);
            Assert.That(clock.Current.ElapsedMinutes, Is.EqualTo(long.MaxValue));
            Assert.That(clock.TryAdvance(clock.Current.Version, 1, out _, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(WorldTimeFailure.TimeOverflow));
        }

        [Test]
        public void VeryLargeExplicitAdvanceDoesNotLoopOverMinutes()
        {
            Assert.That(_clock.TryAdvance(_clock.Current.Version, long.MaxValue, out var change, out _), Is.True);
            Assert.That(change.AdvancedMinutes, Is.EqualTo(long.MaxValue));
            Assert.That(_clock.Current.DayIndex, Is.EqualTo(long.MaxValue / 1440));
            Assert.That(_clock.Current.MinuteOfDay, Is.EqualTo((int)(long.MaxValue % 1440)));
        }

        [Test]
        public void InstancesDoNotShareTimeOrLifecycle()
        {
            var other = new ActionWorldClock(Guid.NewGuid(), 1440);
            Assert.That(_clock.TryAdvance(_clock.Current.Version, 5, out _, out _), Is.True);
            _clock.Close();
            Assert.That(other.Current.ElapsedMinutes, Is.Zero);
            Assert.That(other.IsClosed, Is.False);
            Assert.That(other.IsSuspended, Is.False);
        }
    }
}
