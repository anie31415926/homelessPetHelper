using System;
using System.Linq;
using System.Reflection;
using HomelessPetHelper.Modules.Siege;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.Siege.EditMode
{
    public sealed class SiegeEffectPoolTests
    {
        private const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Emit(SiegeState state, string kind, int until, string visual = null,
            double radius = 0, int unitId = 0) => typeof(SiegeState).GetMethod("EmitEffect", Internal).Invoke(state,
                new object[] { kind, new SiegePoint(3, 4), until, default(SiegePoint), radius, 0, visual, unitId });
        private static void Expire(SiegeState state) =>
            typeof(SiegeState).GetMethod("ExpireEffects", Internal).Invoke(state, null);
        private static SiegeState Clone(SiegeState state) =>
            (SiegeState)typeof(SiegeState).GetMethod("Clone", Internal).Invoke(state, null);

        [Test]
        public void ExpiredRecordsReuseObjectsAndResetAllOptionalData()
        {
            var state = new SiegeState();
            Emit(state, "sweep", 0, "old", 99, 31);
            var first = state.Effects.Single();
            Assert.That(first.Sequence, Is.EqualTo(1));
            Assert.That(first.UnitId, Is.EqualTo(31));
            Expire(state);
            Assert.That(state.Effects, Is.Empty);
            Emit(state, "slap", 100);
            var next = state.Effects.Single();
            Assert.That(next, Is.SameAs(first));
            Assert.That(next.Kind, Is.EqualTo("slap"));
            Assert.That(next.Visual, Is.Null);
            Assert.That(next.Radius, Is.Zero);
            Assert.That(next.To, Is.EqualTo(default(SiegePoint)));
            Assert.That(next.UntilMs, Is.EqualTo(100));
            Assert.That(next.Sequence, Is.EqualTo(2));
            Assert.That(next.UnitId, Is.Zero);
        }

        [Test]
        public void CapacityRecyclesOldestAndKeepsNewestInStableOrder()
        {
            var state = new SiegeState();
            for (int index = 0; index < 192; index++) Emit(state, "shot", 100, index.ToString());
            var first = state.Effects[0];
            Emit(state, "shot", 100, "newest");
            Assert.That(state.Effects.Count, Is.EqualTo(192));
            Assert.That(state.Effects[0].Visual, Is.EqualTo("1"));
            Assert.That(state.Effects[191], Is.SameAs(first));
            Assert.That(state.Effects[191].Visual, Is.EqualTo("newest"));
        }

        [Test]
        public void ClonedStateOwnsItsRecordsAndFreePoolIndependently()
        {
            var state = new SiegeState();
            Emit(state, "active", 100);
            Emit(state, "expired", 0);
            var free = state.Effects[1];
            Expire(state);
            var clone = Clone(state);
            var originalActive = state.Effects[0];
            Emit(clone, "independent", 100);
            Emit(state, "replacement", 100);
            Assert.That(state.Effects[1], Is.SameAs(free));
            Assert.That(clone.Effects[0], Is.Not.SameAs(originalActive));
            Assert.That(clone.Effects[1], Is.Not.SameAs(state.Effects[1]));
            Assert.That(clone.Effects[1].Kind, Is.EqualTo("independent"));
            Assert.That(state.Effects[1].Kind, Is.EqualTo("replacement"));
        }

        [Test]
        public void RepeatedBurstsKeepTheSameBoundedSetOfObjects()
        {
            var state = new SiegeState();
            for (int index = 0; index < 192; index++) Emit(state, "shot", 0);
            var objects = state.Effects.ToArray();
            for (int wave = 0; wave < 100; wave++)
            {
                Expire(state);
                for (int index = 0; index < 192; index++) Emit(state, "shot", 0);
                Assert.That(state.Effects, Is.EquivalentTo(objects));
            }
        }
    }
}
