using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.Options;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueOptionHandTests
    {
        private static readonly Guid SessionId = new Guid("a1bf44ad-b746-4e71-a4d7-c105ad42f33d");

        [Test]
        public void NewHandWaitsForInitialOffer()
        {
            var hand = CreateHand();
            Assert.That(hand.Phase, Is.EqualTo(OptionHandPhase.AwaitingInitialOffer));
            Assert.That(hand.CurrentTurn.SessionId, Is.EqualTo(SessionId));
            Assert.That(hand.CurrentTurn.TurnNumber, Is.Zero);
            Assert.That(hand.IsSuspended, Is.False);
            Assert.That(hand.Options, Is.Empty);
        }

        [Test]
        public void FirstOfferHasDistinctInstancesAndNoImplicitLocks()
        {
            var hand = CreateReadyHand();
            Assert.That(hand.Phase, Is.EqualTo(OptionHandPhase.Choosing));
            Assert.That(hand.CurrentTurn.TurnNumber, Is.EqualTo(1));
            Assert.That(hand.Options.Select(option => option.InstanceId), Is.EqualTo(new long[] { 1, 2, 3 }));
            Assert.That(hand.Options.All(option => !option.IsLocked), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UsedOptionIsConsumedRegardlessOfLock(bool isLocked)
        {
            var hand = CreateReadyHand();
            long id = hand.Options[0].InstanceId;
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, id, isLocked, out _), Is.True);
            var before = hand.Options;

            Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new[] { id }, out var used, out var failure),
                Is.True);
            Assert.That(failure, Is.EqualTo(OptionFailure.None));
            Assert.That(used.Select(option => option.InstanceId), Is.EqualTo(new[] { id }));
            Assert.That(used[0].IsLocked, Is.EqualTo(isLocked));
            Assert.That(hand.Options.Count, Is.EqualTo(2));
            Assert.That(hand.Options.Any(option => option.InstanceId == id), Is.False);
            Assert.That(hand.Phase, Is.EqualTo(OptionHandPhase.AwaitingAnimalResponse));
            Assert.That(before.Count, Is.EqualTo(3));
        }

        [Test]
        public void TwoLocksPersistAcrossManyTurnsWithoutExpiryOrGrowth()
        {
            var hand = CreateReadyHand();
            long[] keptIds = hand.Options.Take(2).Select(option => option.InstanceId).ToArray();
            foreach (long id in keptIds)
            {
                Assert.That(hand.TrySetLocked(hand.CurrentTurn, id, true, out _), Is.True);
            }

            for (int turn = 1; turn <= 20; turn++)
            {
                long usedId = hand.Options.Single(option => !option.IsLocked).InstanceId;
                Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new[] { usedId }, out _, out _), Is.True);
                Refresh(hand, Definitions("test.next-" + turn), new[] { "test.first" });
                Assert.That(hand.Options.Count, Is.EqualTo(3));
                Assert.That(hand.Options.Where(option => option.IsLocked).Select(option => option.InstanceId),
                    Is.EqualTo(keptIds));
                Assert.That(hand.Options.Single(option => !option.IsLocked).InstanceId, Is.EqualTo(turn + 3));
            }
        }

        [Test]
        public void UnlockMakesOptionEligibleForReplacement()
        {
            var hand = CreateReadyHand();
            long keptId = hand.Options[0].InstanceId;
            long releasedId = hand.Options[1].InstanceId;
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, keptId, true, out _), Is.True);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, releasedId, true, out _), Is.True);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, releasedId, false, out _), Is.True);
            Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new[] { hand.Options[2].InstanceId },
                out _, out _), Is.True);

            Refresh(hand, Definitions("test.new-one", "test.new-two"), new[] { "test.new-one" });
            Assert.That(hand.Options[0].InstanceId, Is.EqualTo(keptId));
            Assert.That(hand.Options[0].IsLocked, Is.True);
            Assert.That(hand.Options.Any(option => option.InstanceId == releasedId), Is.False);
            Assert.That(hand.Options.Skip(1).All(option => !option.IsLocked), Is.True);
        }

        [Test]
        public void RepeatedLockAndUnlockAreIdempotent()
        {
            var hand = CreateReadyHand();
            foreach (RescueOption option in hand.Options.Take(2))
            {
                Assert.That(hand.TrySetLocked(hand.CurrentTurn, option.InstanceId, true, out _), Is.True);
                Assert.That(hand.TrySetLocked(hand.CurrentTurn, option.InstanceId, true, out _), Is.True);
            }

            long id = hand.Options[0].InstanceId;
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, id, false, out _), Is.True);
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, id, false, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(OptionFailure.None));
            Assert.That(hand.Options.Count(option => option.IsLocked), Is.EqualTo(1));
        }

        [Test]
        public void IdenticalDefinitionsStillHaveIndependentLockAndUseIdentity()
        {
            var hand = CreateHand();
            var definition = new RescueActionDefinition("test.shared");
            Refresh(hand, new[] { definition, definition, definition }, new[] { definition.Id });
            long lockedId = hand.Options[1].InstanceId;
            long usedId = hand.Options[0].InstanceId;
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, lockedId, true, out _), Is.True);
            Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new[] { usedId }, out _, out _), Is.True);

            Refresh(hand, Definitions("test.fourth", "test.fifth"), new[] { definition.Id });
            Assert.That(hand.Options[0].InstanceId, Is.EqualTo(lockedId));
            Assert.That(hand.Options[0].Definition, Is.SameAs(definition));
            Assert.That(hand.Options.Any(option => option.InstanceId == usedId), Is.False);
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(12)]
        public void LargerOfferAndNewContentUseTheSameLifecycle(int offerCount)
        {
            var hand = CreateHand(offerCount, offerCount - 1);
            var initial = Enumerable.Range(0, offerCount)
                .Select(index => new RescueActionDefinition("test.expanded-" + index)).ToArray();
            Refresh(hand, initial, new[] { initial[0].Id });
            foreach (RescueOption option in hand.Options.Take(offerCount - 1))
            {
                Assert.That(hand.TrySetLocked(hand.CurrentTurn, option.InstanceId, true, out _), Is.True);
            }

            long lastId = hand.Options[offerCount - 1].InstanceId;
            Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new[] { lastId }, out _, out _), Is.True);
            Refresh(hand, Definitions("test.brand-new"), new[] { "test.brand-new" });
            Assert.That(hand.Options.Count, Is.EqualTo(offerCount));
            Assert.That(hand.Options.Count(option => option.IsLocked), Is.EqualTo(offerCount - 1));
            Assert.That(hand.Options.Last().Definition.Id, Is.EqualTo("test.brand-new"));
        }

        [Test]
        public void InputsAndOldSnapshotsCannotMutateHand()
        {
            var hand = CreateHand();
            var definitions = Definitions("test.first", "test.second", "test.third");
            Refresh(hand, definitions, new[] { "test.first" });
            var old = hand.Options;
            definitions[0] = new RescueActionDefinition("test.replaced-input");
            Assert.That(hand.Options[0].Definition.Id, Is.EqualTo("test.first"));
            Assert.Throws<NotSupportedException>(() => ((IList<RescueOption>)old).Clear());

            long id = old[0].InstanceId;
            Assert.That(hand.TrySetLocked(hand.CurrentTurn, id, true, out _), Is.True);
            Assert.That(old[0].IsLocked, Is.False);
            Assert.That(hand.Options[0].IsLocked, Is.True);
            var selectedIds = new[] { id };
            Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, selectedIds, out var used, out _), Is.True);
            selectedIds[0] = 999;
            Assert.That(used[0].InstanceId, Is.EqualTo(id));
            Assert.Throws<NotSupportedException>(() => ((IList<RescueOption>)used).Clear());
        }

        private static RescueOptionHand CreateHand(int offerCount = 3, int lockLimit = 2)
        {
            return new RescueOptionHand(SessionId, new RescueOptionRules(offerCount, 1, lockLimit));
        }

        private static RescueOptionHand CreateReadyHand()
        {
            var hand = CreateHand();
            Refresh(hand, Definitions("test.first", "test.second", "test.third"), new[] { "test.first" });
            return hand;
        }

        private static RescueActionDefinition[] Definitions(params string[] ids)
        {
            return ids.Select(id => new RescueActionDefinition(id)).ToArray();
        }

        private static void Refresh(RescueOptionHand hand, RescueActionDefinition[] definitions, string[] stableIds)
        {
            var nextTurn = new OptionTurnId(SessionId, hand.CurrentTurn.TurnNumber + 1);
            Assert.That(hand.TryRefresh(nextTurn, definitions, stableIds, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(OptionFailure.None));
        }
    }
}
