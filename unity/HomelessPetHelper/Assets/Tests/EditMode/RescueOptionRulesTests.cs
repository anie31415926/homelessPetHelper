using System;
using HomelessPetHelper.Modules.Rescue.Options;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueOptionRulesTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" action")]
        [TestCase("action ")]
        public void InvalidActionIdIsRejected(string id)
        {
            Assert.Throws<ArgumentException>(() => new RescueActionDefinition(id));
        }

        [Test]
        public void DefinitionKeepsItsExactStableId()
        {
            var definition = new RescueActionDefinition("test.new-action");
            Assert.That(definition.Id, Is.EqualTo("test.new-action"));
        }

        [TestCase(0, 1, 0)]
        [TestCase(-1, 1, 0)]
        [TestCase(3, 0, 2)]
        [TestCase(3, -1, 2)]
        [TestCase(3, 4, 2)]
        [TestCase(3, 1, -1)]
        [TestCase(3, 1, 3)]
        [TestCase(3, 1, 4)]
        public void InvalidCountsAreRejected(int offerCount, int useLimit, int lockLimit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RescueOptionRules(offerCount, useLimit, lockLimit));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(6)]
        public void IncreasingUseLimitDoesNotSilentlyEnableUnconfirmedMultiSelect(int useLimit)
        {
            Assert.Throws<NotSupportedException>(() => new RescueOptionRules(6, useLimit, 5));
        }

        [TestCase(1, 0)]
        [TestCase(3, 2)]
        [TestCase(6, 4)]
        public void ValidRulesKeepConfiguredCounts(int offerCount, int lockLimit)
        {
            var rules = new RescueOptionRules(offerCount, 1, lockLimit);
            Assert.That(rules.OfferCount, Is.EqualTo(offerCount));
            Assert.That(rules.UseLimit, Is.EqualTo(1));
            Assert.That(rules.LockLimit, Is.EqualTo(lockLimit));
        }

        [Test]
        public void SessionAndRulesAreRequired()
        {
            Assert.Throws<ArgumentException>(() =>
                new RescueOptionHand(Guid.Empty, new RescueOptionRules(3, 1, 2)));
            Assert.Throws<ArgumentNullException>(() => new RescueOptionHand(Guid.NewGuid(), null));
        }
    }
}
