using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Station.Habitat;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.StationHabitat.EditMode
{
    public sealed class StationHabitatMotionTests
    {
        [TestCase(1)]
        [TestCase(84)]
        [TestCase(-12)]
        public void LongSessionStaysInClearFloorAndVisitsAllPoses(int seed)
        {
            var motion = new StationHabitatMotion(seed);
            var visited = new HashSet<HabitatAction>();
            for (int i = 0; i < 30000; i++)
            {
                var previous = motion.Position;
                motion.Tick(.05);
                Assert.That(StationHabitatMotion.IsWalkable(motion.Position), Is.True);
                Assert.That(previous.DistanceTo(motion.Position), Is.LessThanOrEqualTo(.005501));
                if (motion.Action == HabitatAction.Lie)
                    Assert.That(motion.Position.DistanceTo(StationHabitatMotion.RestPoint), Is.LessThan(.00001));
                visited.Add(motion.Action);
            }
            CollectionAssert.IsSubsetOf(new[] { HabitatAction.Observe, HabitatAction.Walk,
                HabitatAction.Sit, HabitatAction.Lie }, visited);
        }

        [TestCase(true, HabitatResponse.Willing)]
        [TestCase(false, HabitatResponse.NeedsSpace)]
        public void AttentionReadsPermissionDebouncesAndResumesTheSameRoute(bool allowed, HabitatResponse response)
        {
            var motion = new StationHabitatMotion(1);
            while (motion.Action != HabitatAction.Walk) motion.Tick(.05);
            var destination = motion.Destination;
            var position = motion.Position;
            Assert.That(motion.TryGetAttention(allowed), Is.True);
            Assert.That(motion.Response, Is.EqualTo(response));
            Assert.That(motion.TryGetAttention(!allowed), Is.False);
            for (int i = 0; i < 20; i++) motion.Tick(.05);
            Assert.That(motion.Position.DistanceTo(position), Is.Zero);
            for (int i = 0; i < 10; i++) motion.Tick(.05);
            Assert.That(motion.Action, Is.EqualTo(HabitatAction.Walk));
            Assert.That(motion.Destination.DistanceTo(destination), Is.Zero);
            Assert.That(motion.Response, Is.EqualTo(HabitatResponse.None));
        }

        [Test]
        public void PauseAndLargeDeltaNeverAdvanceOfflineTime()
        {
            var motion = new StationHabitatMotion(11);
            var position = motion.Position;
            for (int i = 0; i < 100; i++) motion.Tick(10000, true);
            Assert.That(motion.ActionSeconds, Is.Zero);
            motion.Tick(10000);
            Assert.That(motion.ActionSeconds, Is.EqualTo(.1));
            Assert.That(motion.Position.DistanceTo(position), Is.Zero);
        }

        [Test]
        public void RestingCatDoesNotJumpToStandingWhenClicked()
        {
            var motion = new StationHabitatMotion(1);
            while (motion.Action != HabitatAction.Lie) motion.Tick(.05);
            Assert.That(motion.TryGetAttention(false), Is.True);
            Assert.That(motion.Pose, Is.EqualTo(HabitatAction.Lie));
            for (int i = 0; i < 30; i++) motion.Tick(.05);
            Assert.That(motion.Action, Is.EqualTo(HabitatAction.Lie));
        }

        [Test]
        public void InvalidDeltaIsRejectedWithoutMutation()
        {
            var motion = new StationHabitatMotion(1);
            foreach (double seconds in new[] { -1, double.NaN, double.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => motion.Tick(seconds));
            Assert.That(motion.ActionSeconds, Is.Zero);
        }

        [Test]
        public void PresentationModelHasNoSteadyManagedAllocation()
        {
            var motion = new StationHabitatMotion(81);
            for (int i = 0; i < 1000; i++) motion.Tick(.05);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) motion.Tick(.05);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [Test]
        public void SeedReproducesDestinationsAndTiming()
        {
            var first = new StationHabitatMotion(42);
            var second = new StationHabitatMotion(42);
            for (int i = 0; i < 10000; i++)
            {
                first.Tick(.05); second.Tick(.05);
                Assert.That(first.Position.DistanceTo(second.Position), Is.Zero);
                Assert.That(first.Action, Is.EqualTo(second.Action));
            }
        }
    }
}
