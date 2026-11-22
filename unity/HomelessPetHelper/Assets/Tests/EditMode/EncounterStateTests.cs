using System;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Siege;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class EncounterStateTests
    {
        private GameContent Content => AssetDatabase.LoadAssetAtPath<GameContent>(
            "Assets/Settings/Game/GameContent.asset");
        [TestCase("ordinary")] [TestCase("ordinary-dog")]
        public void VictoryUsesSameIndividualAndPreservesAllUnrelatedCurrentFacts(string template)
        {
            var encounter = Content.CreateMapConfig().GetRescue(template);
            var individual = encounter.CreateIndividual();
            var initial = individual.Current;
            Assert.That(initial.Disposition, Is.SameAs(encounter.InitialDisposition));
            Assert.That(initial.Coat.Dirtiness, Is.EqualTo(encounter.Dirtiness));
            using var activity = new SiegeRescueActivity(Guid.NewGuid(), 0, Content.CreateSiegeCatalog(),
                encounter, individual);
            activity.SynchronizeOutcome();
            Assert.That(individual.Current, Is.SameAs(initial), "An unfinished rescue has no success effect.");
            Assert.That(individual.TryApplyDisposition(initial, new AnimalDisposition(false,
                initial.Disposition.Emotion, initial.Disposition.Stress, initial.Disposition.Behavior)), Is.True);
            var before = individual.Current;
            Win(activity.Battle);
            activity.SynchronizeOutcome();
            var rescued = individual.Current;
            Assert.That(activity.Individual, Is.SameAs(individual));
            Assert.That(rescued.AnimalId, Is.EqualTo(initial.AnimalId));
            Assert.That(rescued.Disposition.IsHungry, Is.False, "Victory must preserve the current hunger.");
            Assert.That(rescued.Disposition.Emotion, Is.EqualTo(encounter.RescuedDisposition.Emotion));
            Assert.That(rescued.Disposition.Behavior, Is.EqualTo(encounter.RescuedDisposition.Behavior));
            Assert.That(rescued.Coat, Is.SameAs(before.Coat));
            Assert.That(rescued.Health, Is.SameAs(before.Health));
            Assert.That(rescued.CarePermissions, Is.SameAs(before.CarePermissions));
            activity.SynchronizeOutcome(); Assert.That(individual.Current, Is.SameAs(rescued));
        }

        [Test]
        public void DispositionComesFromConfigAndInvalidEnumIsRejected()
        {
            string json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Game/CityMapConfig.json").text;
            var configured = new TextAsset(json.Replace("\"emotion\": 0", "\"emotion\": 1"));
            var invalid = new TextAsset(json.Replace("\"emotion\": 0", "\"emotion\": 99"));
            try
            {
                var encounter = new CityMapPreviewConfig(configured).GetRescue("ordinary");
                Assert.That(encounter.CreateIndividual().Current.Disposition.Emotion,
                    Is.EqualTo(AnimalEmotion.Watchful));
                Assert.Throws<ArgumentOutOfRangeException>(() => new CityMapPreviewConfig(invalid));
                Assert.Throws<ArgumentException>(() => new SiegeRescueActivity(Guid.NewGuid(), 0,
                    Content.CreateSiegeCatalog(), encounter, Content.CreateMapConfig()
                        .GetRescue("ordinary-dog").CreateIndividual()));
            }
            finally { UnityEngine.Object.DestroyImmediate(configured); UnityEngine.Object.DestroyImmediate(invalid); }
        }

        [Test]
        public void CoatPatternHasStableIdentityAndTracksSeverityWithoutCreatingCleanMarks()
        {
            Guid id = Guid.Parse("20000000-0000-0000-0000-000000000001");
            int seed = AnimalCoatPattern.Seed(id);
            long mild = 0, severe = 0;
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    Assert.That(AnimalCoatPattern.Sample(x, y, seed, false, 0), Is.Zero);
                    mild += AnimalCoatPattern.Sample(x, y, seed, false, 20);
                    severe += AnimalCoatPattern.Sample(x, y, seed, false, 80);
                }
            Assert.That(seed, Is.EqualTo(AnimalCoatPattern.Seed(id)));
            Assert.That(mild, Is.GreaterThan(0)); Assert.That(severe, Is.GreaterThan(mild * 3));
        }

        private static void Win(SiegeSession battle)
        {
            for (int i = 0; i < 1000 && battle.Phase != SiegeState.BattlePhase.Won &&
                battle.Phase != SiegeState.BattlePhase.Timeout; i++)
            {
                if (battle.Phase == SiegeState.BattlePhase.Decision)
                {
                    var state = battle.Snapshot;
                    var order = new SiegeOrder { SessionId = state.SessionId, Turn = state.Turn,
                        DeployCardId = state.Hand.First().Id, DeployLaneId = battle.Catalog.Lanes[state.Turn % 3].Id };
                    if (!battle.TryOrder(order, true, out _, out _))
                        Assert.That(battle.TryOrder(new SiegeOrder { SessionId = state.SessionId,
                            Turn = state.Turn }, true, out _, out _), Is.True);
                }
                battle.Advance(1000);
            }
            Assert.That(battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Won));
        }
    }
}
