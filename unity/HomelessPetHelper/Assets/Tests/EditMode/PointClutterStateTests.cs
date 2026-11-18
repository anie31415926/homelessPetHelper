using System;
using HomelessPetHelper.Modules.Outing;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class PointClutterStateTests
    {
        [Test]
        public void ClearedHeavyPointStaysClearUntilConfiguredGameplayTimeThenReturnsOnlyToLight()
        {
            var state = Create(ClutterLevel.Heavy);
            Assert.That(state.TryReduceTo(ClutterLevel.Clear, out _), Is.True);
            var before = state.Capture();

            Assert.That(state.TryAdvanceTo(119, out _), Is.True);
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Clear));
            Assert.That(state.TryAdvanceTo(120, out _), Is.True);
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Light));
            Assert.That(state.TryAdvanceTo(long.MaxValue, out _), Is.True);
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Light));
            Assert.That(before.Level, Is.EqualTo(ClutterLevel.Clear));
            Assert.That(before.ElapsedMinutes, Is.Zero);
            Assert.That(before.ClearSinceMinutes, Is.Zero);
        }

        [Test]
        public void CapturingOrReopeningViewsDoesNotAdvanceTimeOrRegrow()
        {
            var state = Create(ClutterLevel.Clear);
            for (var index = 0; index < 10; index++)
            {
                Assert.That(state.Capture().Level, Is.EqualTo(ClutterLevel.Clear));
            }

            Assert.That(state.ElapsedMinutes, Is.Zero);
        }

        [TestCase(ClutterLevel.Light)]
        [TestCase(ClutterLevel.Moderate)]
        [TestCase(ClutterLevel.Heavy)]
        public void NonclearLevelsDoNotAutomaticallyEscalateOrReset(ClutterLevel initial)
        {
            var state = Create(initial);
            Assert.That(state.TryAdvanceTo(long.MaxValue, out _), Is.True);
            Assert.That(state.Level, Is.EqualTo(initial));
            Assert.That(state.Capture().ClearSinceMinutes, Is.Null);
        }

        [Test]
        public void PartialReductionDoesNotDemandACompletelyClearScene()
        {
            var state = Create(ClutterLevel.Heavy);
            Assert.That(state.TryReduceTo(ClutterLevel.Moderate, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(OutingFailure.None));
            Assert.That(state.TryAdvanceTo(120, out _), Is.True);
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Moderate));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidLevelDoesNotChangeState(int value)
        {
            var state = Create(ClutterLevel.Heavy);
            Assert.That(state.TryReduceTo((ClutterLevel)value, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.InvalidClutterLevel));
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Heavy));
            Assert.That(state.Capture().ClearSinceMinutes, Is.Null);
        }

        [Test]
        public void DuplicateClearCannotPostponeRegrowth()
        {
            var state = Create(ClutterLevel.Heavy);
            Assert.That(state.TryReduceTo(ClutterLevel.Clear, out _), Is.True);
            Assert.That(state.TryAdvanceTo(119, out _), Is.True);
            Assert.That(state.TryReduceTo(ClutterLevel.Clear, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.ClutterNotReduced));
            Assert.That(state.TryAdvanceTo(120, out _), Is.True);
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Light));
        }

        [Test]
        public void SecondClearingStartsANewDelayAndRepeatedTimestampDoesNotRegrowTwice()
        {
            var state = Create(ClutterLevel.Clear);
            state.TryAdvanceTo(120, out _);
            Assert.That(state.TryAdvanceTo(120, out _), Is.True);
            Assert.That(state.TryReduceTo(ClutterLevel.Clear, out _), Is.True);
            Assert.That(state.TryAdvanceTo(120, out _), Is.True);
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Clear));
            state.TryAdvanceTo(239, out _);
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Clear));
            state.TryAdvanceTo(240, out _);
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Light));
        }

        [Test]
        public void PauseRejectsMutationAndDoesNotCatchUpWithoutAWorldTimeCommit()
        {
            var state = Create(ClutterLevel.Heavy);
            state.SetSuspended(true);
            Assert.That(state.TryReduceTo(ClutterLevel.Clear, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.Suspended));
            Assert.That(state.TryAdvanceTo(120, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.Suspended));
            Assert.That(state.ElapsedMinutes, Is.Zero);
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Heavy));
            state.SetSuspended(false);
            Assert.That(state.ElapsedMinutes, Is.Zero);
        }

        [TestCase(-1L)]
        [TestCase(9L)]
        public void StaleOrNegativeTimeIsRefusedWithoutRegrowth(long minutes)
        {
            var state = new PointClutterState("parking", ClutterLevel.Clear, 10, 120);
            Assert.That(state.TryAdvanceTo(minutes, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.InvalidTime));
            Assert.That(state.ElapsedMinutes, Is.EqualTo(10));
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Clear));
        }

        [Test]
        public void UpperTimeBoundaryUsesSubtractionWithoutOverflow()
        {
            var state = new PointClutterState("parking", ClutterLevel.Clear, long.MaxValue - 5, 5);
            Assert.That(state.TryAdvanceTo(long.MaxValue, out _), Is.True);
            Assert.That(state.Level, Is.EqualTo(ClutterLevel.Light));
        }

        [Test]
        public void DifferentLocationsRemainIndependent()
        {
            var first = Create(ClutterLevel.Heavy);
            var second = new PointClutterState("yard", ClutterLevel.Heavy, 0, 120);
            first.TryReduceTo(ClutterLevel.Clear, out _);
            first.TryAdvanceTo(120, out _);
            Assert.That(second.Level, Is.EqualTo(ClutterLevel.Heavy));
            Assert.That(second.ElapsedMinutes, Is.Zero);
        }

        [Test]
        public void InvalidDefinitionsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new PointClutterState("", ClutterLevel.Clear, 0, 120));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PointClutterState("p", (ClutterLevel)4, 0, 120));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PointClutterState("p", ClutterLevel.Clear, -1, 120));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PointClutterState("p", ClutterLevel.Clear, 0, 0));
        }

        private static PointClutterState Create(ClutterLevel level)
        {
            return new PointClutterState("parking", level, 0, 120);
        }
    }
}
