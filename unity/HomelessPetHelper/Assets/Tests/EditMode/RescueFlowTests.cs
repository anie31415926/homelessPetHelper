using HomelessPetHelper.Modules.Rescue;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueFlowTests
    {
        [Test]
        public void NewFlowStartsAtStation()
        {
            var flow = new RescueFlow();
            Assert.That(flow.Stage, Is.EqualTo(RescueStage.Station));
            Assert.That(flow.IsSuspended, Is.False);
        }

        [Test]
        public void CompleteLoopCanStartAgain()
        {
            var flow = new RescueFlow();
            Advance(flow, RescueSignal.LeaveStation, RescueStage.Approach);
            Advance(flow, RescueSignal.ApproachCompleted, RescueStage.Negotiation);
            Advance(flow, RescueSignal.NegotiationCompleted, RescueStage.Rescued);
            Advance(flow, RescueSignal.ReturnToStation, RescueStage.Care);
            Advance(flow, RescueSignal.CareCompleted, RescueStage.Station);
            Advance(flow, RescueSignal.LeaveStation, RescueStage.Approach);
        }

        [Test]
        public void EveryStageRejectsEveryUnrelatedSignalWithoutMutation()
        {
            for (int stageIndex = 0; stageIndex < 5; stageIndex++)
            {
                var flow = CreateFlowAt(stageIndex);
                for (int signalIndex = 0; signalIndex < 5; signalIndex++)
                {
                    if (stageIndex == signalIndex)
                    {
                        continue;
                    }

                    Assert.That(flow.TryAdvance((RescueSignal)signalIndex, out var failure), Is.False);
                    Assert.That(failure, Is.EqualTo(TransitionFailure.UnexpectedSignal));
                    Assert.That(flow.Stage, Is.EqualTo((RescueStage)stageIndex));
                }
            }
        }

        [TestCase(-1)]
        [TestCase(5)]
        [TestCase(int.MaxValue)]
        public void UnknownSignalIsRejected(int signal)
        {
            var flow = new RescueFlow();
            Assert.That(flow.TryAdvance((RescueSignal)signal, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(TransitionFailure.InvalidSignal));
            Assert.That(flow.Stage, Is.EqualTo(RescueStage.Station));
        }

        [Test]
        public void DuplicateCompletionDoesNotAdvanceTwice()
        {
            var flow = CreateFlowAt(1);
            Advance(flow, RescueSignal.ApproachCompleted, RescueStage.Negotiation);
            Assert.That(flow.TryAdvance(RescueSignal.ApproachCompleted, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(TransitionFailure.UnexpectedSignal));
            Assert.That(flow.Stage, Is.EqualTo(RescueStage.Negotiation));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SuspensionBlocksTransitionAndResumeKeepsProgress(int stageIndex)
        {
            var flow = CreateFlowAt(stageIndex);
            flow.SetSuspended(true);
            flow.SetSuspended(true);
            Assert.That(flow.TryAdvance((RescueSignal)stageIndex, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(TransitionFailure.Suspended));
            Assert.That(flow.Stage, Is.EqualTo((RescueStage)stageIndex));
            flow.SetSuspended(false);
            Assert.That(flow.TryAdvance((RescueSignal)stageIndex, out failure), Is.True);
            Assert.That(failure, Is.EqualTo(TransitionFailure.None));
        }

        [Test]
        public void InstancesDoNotShareProgressOrSuspension()
        {
            var first = CreateFlowAt(1);
            first.SetSuspended(true);
            var second = new RescueFlow();
            Assert.That(second.Stage, Is.EqualTo(RescueStage.Station));
            Assert.That(second.IsSuspended, Is.False);
        }

        private static RescueFlow CreateFlowAt(int stageIndex)
        {
            var flow = new RescueFlow();
            for (int index = 0; index < stageIndex; index++)
            {
                Advance(flow, (RescueSignal)index, (RescueStage)(index + 1));
            }

            return flow;
        }

        private static void Advance(RescueFlow flow, RescueSignal signal, RescueStage expected)
        {
            Assert.That(flow.TryAdvance(signal, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(TransitionFailure.None));
            Assert.That(flow.Stage, Is.EqualTo(expected));
        }
    }
}
