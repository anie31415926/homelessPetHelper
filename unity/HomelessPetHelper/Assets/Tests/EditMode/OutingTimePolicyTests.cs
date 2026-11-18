using System;
using HomelessPetHelper.Modules.Outing;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class OutingTimePolicyTests
    {
        [TestCase(OutingReturnProgress.BeforeDeparture, 0L)]
        [TestCase(OutingReturnProgress.Exploring, 5L)]
        [TestCase(OutingReturnProgress.OpeningRoute, 15L)]
        [TestCase(OutingReturnProgress.Negotiating, 30L)]
        [TestCase(OutingReturnProgress.HandingOver, 35L)]
        public void DurationDependsOnReachedStageNotWhetherAnAnimalWasFound(
            OutingReturnProgress progress, long expected)
        {
            var policy = new OutingTimePolicy(5, 10, 15, 5);
            Assert.That(policy.TryGetDuration(progress, out var minutes, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(OutingFailure.None));
            Assert.That(minutes, Is.EqualTo(expected));
        }

        [TestCase(-1)]
        [TestCase(5)]
        public void InvalidProgressReturnsZeroAndExplicitFailure(int progress)
        {
            var policy = new OutingTimePolicy(5, 10, 15, 5);
            Assert.That(policy.TryGetDuration((OutingReturnProgress)progress,
                out var minutes, out var failure), Is.False);
            Assert.That(minutes, Is.Zero);
            Assert.That(failure, Is.EqualTo(OutingFailure.InvalidProgress));
        }

        [Test]
        public void RepeatedQueriesAreReadOnlyAndDoNotAccumulateDuration()
        {
            var policy = new OutingTimePolicy(5, 10, 15, 5);
            policy.TryGetDuration(OutingReturnProgress.Negotiating, out var first, out _);
            policy.TryGetDuration(OutingReturnProgress.Negotiating, out var second, out _);
            Assert.That(first, Is.EqualTo(30));
            Assert.That(second, Is.EqualTo(first));
        }

        [TestCase(0, 1, 1, 1)]
        [TestCase(1, -1, 1, 1)]
        [TestCase(1, 1, 0, 1)]
        [TestCase(1, 1, 1, 0)]
        public void InvalidStageIncrementsAreRejected(int exploring, int opening, int negotiating, int handover)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new OutingTimePolicy(exploring, opening, negotiating, handover));
        }

        [Test]
        public void CumulativeDurationOverflowIsRejectedAtConfigurationBoundary()
        {
            Assert.Throws<OverflowException>(() => new OutingTimePolicy(long.MaxValue, 1, 1, 1));
        }
    }
}
