using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.Options;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueOptionRejectionTests
    {
        private static readonly Guid SessionId = new Guid("b42a42b5-c02b-407e-b38c-f382f454aa87");

        [Test]
        public void FirstRejectedOfferDoesNotAdvanceTurnOrReserveInstanceIds()
        {
            var hand = CreateHand();
            string before = Describe(hand);
            AssertRefreshRejected(hand, Definitions(3), Array.Empty<string>(), OptionFailure.NoStabilizingChoice);
            Assert.That(Describe(hand), Is.EqualTo(before));
            Refresh(hand, Definitions(3), new[] { "test.0" });
            Assert.That(hand.Options[0].InstanceId, Is.EqualTo(1));
        }

        [Test]
        public void RefreshWhileChoosingDoesNotRerollOrUnlock()
        {
            var hand = CreateReadyHand();
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 1, true, out _), Is.True);
            AssertRefreshRejected(hand, Definitions(2), new[] { "test.0" }, OptionFailure.UnexpectedPhase);
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(4)]
        public void WrongOfferSizeLeavesInitialHandUnchanged(int count)
        {
            AssertRefreshRejected(CreateHand(), Definitions(count), new[] { "test.0" }, OptionFailure.InvalidOffer);
        }

        [Test]
        public void NullOrMalformedOfferIsRejectedWithoutPartialCommit()
        {
            var hand = CreateHand();
            AssertRefreshRejected(hand, null, new[] { "test.0" }, OptionFailure.InvalidOffer);
            AssertRefreshRejected(hand, Definitions(3), null, OptionFailure.InvalidOffer);
            var definitions = Definitions(3);
            definitions[2] = null;
            AssertRefreshRejected(hand, definitions, new[] { "test.0" }, OptionFailure.InvalidOffer);
            Refresh(hand, Definitions(3), new[] { "test.0" });
            Assert.That(hand.Options.Select(option => option.InstanceId), Is.EqualTo(new long[] { 1, 2, 3 }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" test.0")]
        public void MalformedStableIdCannotCertifyAnOffer(string id)
        {
            AssertRefreshRejected(CreateHand(), Definitions(3), new[] { id }, OptionFailure.InvalidOffer);
        }

        [Test]
        public void StableClassificationUsesOrdinalActionIdentity()
        {
            AssertRefreshRejected(CreateHand(), Definitions(3), new[] { "TEST.0" },
                OptionFailure.NoStabilizingChoice);
        }

        [Test]
        public void RetainedOptionIsRecheckedAgainstUpcomingTurnClassification()
        {
            var hand = CreateReadyHand();
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 1, true, out _), Is.True);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 2, true, out _), Is.True);
            Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new long[] { 3 }, out _, out _), Is.True);
            var replacement = new[] { new RescueActionDefinition("test.new") };
            AssertRefreshRejected(hand, replacement, new[] { "test.absent" }, OptionFailure.NoStabilizingChoice);

            Refresh(hand, replacement, new[] { "test.new" });
            Assert.That(hand.Options.Select(option => option.InstanceId), Is.EqualTo(new long[] { 1, 2, 4 }));
            Assert.That(hand.Options.Take(2).All(option => option.IsLocked), Is.True);
        }

        [Test]
        public void LockLimitRejectsThirdLockWithoutChangingExistingLocks()
        {
            var hand = CreateReadyHand();
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 1, true, out _), Is.True);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 2, true, out _), Is.True);
            string before = Describe(hand);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 3, true, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.LockLimitReached));
            Assert.That(Describe(hand), Is.EqualTo(before));
        }

        [Test]
        public void ZeroLockLimitStillAllowsUsingAndRefreshing()
        {
            var hand = new RescueOptionHand(SessionId, new RescueOptionRules(3, 1, 0));
            Refresh(hand, Definitions(3), new[] { "test.0" });
            string before = Describe(hand);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 1, true, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.LockLimitReached));
            Assert.That(Describe(hand), Is.EqualTo(before));
            Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new long[] { 1 }, out _, out _), Is.True);
            Refresh(hand, Definitions(3), new[] { "test.0" });
            Assert.That(hand.Options.Count, Is.EqualTo(3));
        }

        [Test]
        public void OfferSizeMustAccountForRetainedLocksAndConsumedLockedOption()
        {
            var hand = CreateReadyHand();
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 1, true, out _), Is.True);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 2, true, out _), Is.True);
            Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new long[] { 1 }, out _, out _), Is.True);
            AssertRefreshRejected(hand, Definitions(1), new[] { "test.0" }, OptionFailure.InvalidOffer);
            AssertRefreshRejected(hand, Definitions(3), new[] { "test.0" }, OptionFailure.InvalidOffer);
            Refresh(hand, Definitions(2), new[] { "test.0" });
            Assert.That(hand.Options.Select(option => option.InstanceId), Is.EqualTo(new long[] { 2, 4, 5 }));
        }

        [Test]
        public void UnknownLockTargetIsRejected()
        {
            var hand = CreateReadyHand();
            string before = Describe(hand);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 99, true, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.UnknownOption));
            Assert.That(Describe(hand), Is.EqualTo(before));
        }

        [Test]
        public void InvalidSelectionsReturnEmptyResultAndNoMutation()
        {
            var hand = CreateReadyHand();
            AssertUseRejected(hand, hand.CurrentTurn, null, OptionFailure.InvalidSelection);
            AssertUseRejected(hand, hand.CurrentTurn, Array.Empty<long>(), OptionFailure.InvalidSelection);
            AssertUseRejected(hand, hand.CurrentTurn, new long[] { 1, 1 }, OptionFailure.DuplicateSelection);
            AssertUseRejected(hand, hand.CurrentTurn, new long[] { 1, 2 }, OptionFailure.UseLimitExceeded);
            AssertUseRejected(hand, hand.CurrentTurn, new long[] { 0 }, OptionFailure.UnknownOption);
            AssertUseRejected(hand, hand.CurrentTurn, new long[] { -1 }, OptionFailure.UnknownOption);
            AssertUseRejected(hand, hand.CurrentTurn, new[] { long.MaxValue }, OptionFailure.UnknownOption);
        }

        [Test]
        public void DuplicateUseAndResponsePhaseLockAreRejected()
        {
            var hand = CreateReadyHand();
            Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new long[] { 1 }, out _, out _), Is.True);
            AssertUseRejected(hand, hand.CurrentTurn, new long[] { 1 }, OptionFailure.UnexpectedPhase);
            string before = Describe(hand);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 2, true, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.UnexpectedPhase));
            Assert.That(Describe(hand), Is.EqualTo(before));
        }

        [Test]
        public void InitialHandDoesNotAcceptChoiceCommands()
        {
            var hand = CreateHand();
            AssertUseRejected(hand, hand.CurrentTurn, new long[] { 1 }, OptionFailure.UnexpectedPhase);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 1, true, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.UnexpectedPhase));
        }

        [Test]
        public void StaleTurnAndStaleInstanceAreDifferentRejections()
        {
            var hand = CreateReadyHand();
            OptionTurnId oldTurn = hand.CurrentTurn;
            Assert.That(hand.TryConsumeSelection(oldTurn, new long[] { 1 }, out _, out _), Is.True);
            Refresh(hand, Definitions(3), new[] { "test.0" });
            AssertUseRejected(hand, oldTurn, new long[] { 4 }, OptionFailure.WrongTurn);
            AssertUseRejected(hand, hand.CurrentTurn, new long[] { 2 }, OptionFailure.UnknownOption);
            string before = Describe(hand);
            Assert.That(hand.TrySetLocked(oldTurn, 4, true, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.WrongTurn));
            Assert.That(Describe(hand), Is.EqualTo(before));
        }

        [Test]
        public void OtherSessionCannotUseMatchingNumericInstanceId()
        {
            var hand = CreateReadyHand();
            var otherTurn = new OptionTurnId(Guid.NewGuid(), 1);
            AssertUseRejected(hand, otherTurn, new long[] { 1 }, OptionFailure.WrongSession);
            string before = Describe(hand);
            Assert.That(hand.TrySetLocked(otherTurn, 1, true, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.WrongSession));
            Assert.That(Describe(hand), Is.EqualTo(before));

            var initial = CreateHand();
            Assert.That(initial.TryRefresh(otherTurn, Definitions(3), new[] { "test.0" }, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.WrongSession));
            Assert.That(initial.Options, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(int.MaxValue)]
        public void OutOfSequenceRefreshIsRejected(int requestedTurn)
        {
            var hand = CreateHand();
            string before = Describe(hand);
            Assert.That(hand.TryRefresh(new OptionTurnId(SessionId, requestedTurn), Definitions(3),
                new[] { "test.0" }, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.WrongTurn));
            Assert.That(Describe(hand), Is.EqualTo(before));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SuspensionBlocksAllCommandsWithoutDiscardingProgress(int stage)
        {
            var hand = CreateAt(stage);
            hand.SetSuspended(true);
            string before = Describe(hand);
            AssertUseRejected(hand, hand.CurrentTurn, new long[] { 2 }, OptionFailure.Suspended);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 2, true, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.Suspended));
            AssertRefreshRejected(hand, Definitions(3), new[] { "test.0" }, OptionFailure.Suspended);
            Assert.That(Describe(hand), Is.EqualTo(before));
            hand.SetSuspended(false);

            if (stage == 1)
            {
                Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new long[] { 2 }, out _, out _), Is.True);
            }
            else
            {
                Refresh(hand, Definitions(3), new[] { "test.0" });
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void CloseIsTerminalAndIdempotent(int stage)
        {
            var hand = CreateAt(stage);
            hand.Close();
            hand.Close();
            Assert.That(hand.Options, Is.Empty);
            Assert.That(hand.Phase, Is.EqualTo(OptionHandPhase.Closed));
            AssertUseRejected(hand, hand.CurrentTurn, new long[] { 1 }, OptionFailure.Closed);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, 1, true, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OptionFailure.Closed));
            AssertRefreshRejected(hand, Definitions(3), new[] { "test.0" }, OptionFailure.Closed);
        }

        private static RescueOptionHand CreateHand()
        {
            return new RescueOptionHand(SessionId, new RescueOptionRules(3, 1, 2));
        }

        private static RescueOptionHand CreateReadyHand()
        {
            return CreateAt(1);
        }

        private static RescueOptionHand CreateAt(int stage)
        {
            var hand = CreateHand();
            if (stage >= 1)
            {
                Refresh(hand, Definitions(3), new[] { "test.0" });
            }

            if (stage >= 2)
            {
                Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new long[] { 1 }, out _, out _), Is.True);
            }

            return hand;
        }

        private static RescueActionDefinition[] Definitions(int count)
        {
            return Enumerable.Range(0, count).Select(index => new RescueActionDefinition("test." + index)).ToArray();
        }

        private static string Describe(RescueOptionHand hand)
        {
            string options = string.Join("|", hand.Options.Select(option =>
                option.InstanceId + ":" + option.Definition.Id + ":" + option.IsLocked));
            return hand.CurrentTurn.SessionId + ":" + hand.CurrentTurn.TurnNumber + ":" + hand.Phase + ":" +
                hand.IsSuspended + ":" + options;
        }

        private static void AssertUseRejected(RescueOptionHand hand, OptionTurnId turn, IReadOnlyList<long> ids,
            OptionFailure expected)
        {
            string before = Describe(hand);
            Assert.That(hand.TryConsumeSelection(turn, ids, out var used, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(used, Is.Empty);
            Assert.That(Describe(hand), Is.EqualTo(before));
        }

        private static void AssertRefreshRejected(RescueOptionHand hand, RescueActionDefinition[] definitions,
            string[] stableIds, OptionFailure expected)
        {
            string before = Describe(hand);
            var nextTurn = new OptionTurnId(SessionId, hand.CurrentTurn.TurnNumber + 1);
            Assert.That(hand.TryRefresh(nextTurn, definitions, stableIds, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(Describe(hand), Is.EqualTo(before));
        }

        private static void Refresh(RescueOptionHand hand, RescueActionDefinition[] definitions, string[] stableIds)
        {
            var nextTurn = new OptionTurnId(SessionId, hand.CurrentTurn.TurnNumber + 1);
            Assert.That(hand.TryRefresh(nextTurn, definitions, stableIds, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(OptionFailure.None));
        }
    }
}
