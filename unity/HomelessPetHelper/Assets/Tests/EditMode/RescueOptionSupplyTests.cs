using System;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.Options;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueOptionSupplyTests
    {
        [Test]
        public void EverySeedAndLockedPairKeepsOneRealSafeChoiceAndAtMostOneFlavor()
        {
            var catalog = RescueOptionContentTests.LoadSaved().Catalog;
            string[] eligible = catalog.Entries.Select(entry => entry.Id).ToArray();
            string[] safe = { "wait" };
            foreach (string first in eligible)
            foreach (string second in eligible.Where(id => id != first))
            {
                if (new[] { first, second }.Count(id => catalog.Get(id).Kind == RescueOptionKind.Flavor) > 1)
                {
                    continue;
                }

                for (int seed = 0; seed < 25; seed++)
                {
                    var hand = new RescueOptionHand(Guid.NewGuid(), new RescueOptionRules(3, 1, 2));
                    string[] initial = { first, second, eligible.First(id => id != first && id != second) };
                    Assert.That(hand.TryRefresh(new OptionTurnId(hand.CurrentTurn.SessionId, 1),
                        initial.Select(id => new RescueActionDefinition(id)).ToArray(), initial, out _), Is.True);
                    foreach (RescueOption option in hand.Options.Take(2).ToArray())
                    {
                        Assert.That(hand.TrySetLocked(hand.CurrentTurn, option.InstanceId, true, out _), Is.True);
                    }

                    long used = hand.Options[2].InstanceId;
                    Assert.That(hand.TryConsumeSelection(hand.CurrentTurn, new[] { used }, out _, out _), Is.True);
                    var drawn = RescueOptionSupply.Draw(catalog, hand.Options, eligible, safe, 3, new Random(seed));
                    var ids = hand.Options.Where(option => option.IsLocked).Select(option => option.Definition.Id)
                        .Concat(drawn.Select(entry => entry.Id)).ToArray();
                    Assert.That(ids.Length, Is.EqualTo(3));
                    Assert.That(ids.Select(id => catalog.Get(id).GroupId).Distinct().Count(), Is.EqualTo(3));
                    Assert.That(ids.Count(id => catalog.Get(id).Kind == RescueOptionKind.Flavor),
                        Is.LessThanOrEqualTo(1));
                    Assert.That(ids, Does.Contain("wait"));
                    Assert.That(hand.TryRefresh(new OptionTurnId(hand.CurrentTurn.SessionId, 2), drawn, safe, out _),
                        Is.True);
                }
            }
        }

        [Test]
        public void FlavorCannotCountAsSafeOrFillMissingDistinctMethods()
        {
            var catalog = RescueOptionContentTests.LoadSaved().Catalog;
            var hand = new RescueOptionHand(Guid.NewGuid(), new RescueOptionRules(3, 1, 2));
            string[] eligible = catalog.Entries.Select(entry => entry.Id).ToArray();
            Assert.Throws<InvalidOperationException>(() => RescueOptionSupply.Draw(catalog, hand.Options,
                eligible, new[] { "phone.reply" }, 3, new Random(0)));
            Assert.Throws<InvalidOperationException>(() => RescueOptionSupply.Draw(catalog, hand.Options,
                new[] { "wait", "phone.reply", "phone.photo" }, new[] { "wait" }, 3, new Random(0)));
            Assert.That(hand.Options, Is.Empty);
            Assert.That(hand.CurrentTurn.TurnNumber, Is.Zero);
        }

        [Test]
        public void VariantsShareOneGroupAndInputOrderDoesNotDriveRandomness()
        {
            var original = RescueOptionContentTests.LoadSaved().Catalog;
            var catalog = new RescueOptionCatalog(original.Entries.Concat(new[]
            {
                new RescueOptionContent("wait.variant", "wait", RescueOptionKind.Rescue, "wait", "")
            }).ToArray());
            var hand = new RescueOptionHand(Guid.NewGuid(), new RescueOptionRules(3, 1, 2));
            string[] eligible = catalog.Entries.Select(entry => entry.Id).ToArray();
            for (int seed = 0; seed < 100; seed++)
            {
                var first = RescueOptionSupply.Draw(catalog, hand.Options, eligible, new[] { "wait" },
                    3, new Random(seed)).Select(entry => entry.Id).ToArray();
                var second = RescueOptionSupply.Draw(catalog, hand.Options, eligible.Reverse().ToArray(),
                    new[] { "wait" }, 3, new Random(seed)).Select(entry => entry.Id).ToArray();
                Assert.That(first, Is.EqualTo(second));
                Assert.That(first.Select(id => catalog.Get(id).GroupId).Distinct().Count(), Is.EqualTo(3));
                Assert.That(first, Does.Contain("wait"));
            }
        }
    }
}
