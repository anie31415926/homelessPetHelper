using HomelessPetHelper.Modules.Station.UI.Affection;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Station;
using NUnit.Framework;
using UnityEditor;

namespace HomelessPetHelper.Tests.EditMode
{
    [TestFixture]
    public sealed class DoudouAffectionTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void SavedMainRegistersAnIndependentThirdDefinitionAppearanceAndMask()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            Assert.DoesNotThrow(content.Validate);
            var map = content.CreateMapConfig();
            string id = StationArtwork.AffectionDefinitionId;
            Assert.That(map.GetRescue("ordinary").AnimalDefinitionId, Is.EqualTo("cat.shy"));
            Assert.That(map.GetRescue("ordinary-dog").AnimalDefinitionId, Is.EqualTo("dog"));
            Assert.That(map.GetRescue("park-doudou").AnimalDefinitionId, Is.EqualTo(id));
            Assert.That(map.CreateRequestPool().Requests.Single(item => item.TemplateId == "park-doudou").PointId,
                Is.EqualTo("park"));
            var art = content.Artwork;
            var doudou = art.Get(id, true, out var mask);
            Assert.That(doudou, Is.Not.SameAs(art.Get(true, out var originalDogMask)));
            Assert.That(doudou, Is.Not.SameAs(art.Get(false, out _)));
            Assert.That(mask.SequenceEqual(originalDogMask), Is.False);
            Assert.That(mask.Count(pixel => pixel != 0), Is.GreaterThan(4000));
            Assert.That(art.AffectionView.HasArt, Is.True);
            Assert.That(art.AffectionView.mainTexture.width, Is.EqualTo(1024));
            Assert.That(content.Font.HasCharacter('\u8c46'), Is.True);
        }


        private static void AssertFace(PetAffectionReaction pet, float now, AffectionFace expected)
        {
            Assert.That(pet.Face(now), Is.EqualTo(expected), $"face at t={now}");
        }

        private static void AssertWag(PetAffectionReaction pet, float now, float expected)
        {
            Assert.That(pet.Wag(now), Is.EqualTo(expected).Within(Tolerance), $"wag at t={now}");
        }

        /// <summary>Drives the pet into a smile at t=0.20 and returns it.</summary>
        private static PetAffectionReaction Smiling()
        {
            var pet = new PetAffectionReaction();
            pet.Begin(0f);
            pet.Stroke(0f, 2f);
            pet.Stroke(0.10f, 40f); // 40 units, but age 0.10 is too short
            pet.Stroke(0.20f, 1f);  // 41 units, age 0.20 -> smile
            return pet;
        }

        /// <summary>Drives the pet into a laugh pulse at t=0.60 and returns it.</summary>
        private static PetAffectionReaction Laughing()
        {
            var pet = new PetAffectionReaction();
            pet.Begin(0f);
            pet.Stroke(0f, 2f);
            pet.Stroke(0.10f, 36f);
            pet.Stroke(0.20f, 36f);
            pet.Stroke(0.30f, 36f); // 108 units, age 0.30 -> still no laugh
            Assert.That(pet.Stroke(0.60f, 10f), Is.True, "laugh pulse at t=0.60");
            return pet;
        }


        [Test]
        public void NewInstance_StaysCalm()
        {
            var pet = new PetAffectionReaction();
            AssertFace(pet, 0f, AffectionFace.Calm);
            AssertWag(pet, 0f, 0f);
            Assert.That(pet.Stroke(0f, 10f), Is.False, "stroke without Begin is ignored");
        }

        [Test]
        public void ShortStroke_DoesNotSmile()
        {
            var pet = new PetAffectionReaction();
            pet.Begin(0f);
            pet.Stroke(0f, 2f);

            Assert.That(pet.Stroke(0.04f, 20f), Is.False);
            AssertFace(pet, 0.04f, AffectionFace.Calm);

            Assert.That(pet.Stroke(0.10f, 18f), Is.False); // 38 units but age 0.10
            AssertFace(pet, 0.10f, AffectionFace.Calm);
        }

        [Test]
        public void Smile_RequiresDistanceAndTime()
        {
            var pet = new PetAffectionReaction();
            pet.Begin(0f);
            pet.Stroke(0f, 2f);
            pet.Stroke(0.10f, 40f);

            AssertFace(pet, 0.10f, AffectionFace.Calm); // distance met, time not

            Assert.That(pet.Stroke(0.16f, 1f), Is.False); // 41 units, age 0.16
            AssertFace(pet, 0.16f, AffectionFace.Smile);
            AssertWag(pet, 0.16f, 0.30f);
        }


        [Test]
        public void Laugh_RequiresFullStreakAndVisibleSmile()
        {
            var pet = new PetAffectionReaction();
            pet.Begin(0f);
            pet.Stroke(0f, 2f);
            pet.Stroke(0.10f, 36f);
            pet.Stroke(0.20f, 36f); // 72 units -> smile
            AssertFace(pet, 0.20f, AffectionFace.Smile);

            Assert.That(pet.Stroke(0.30f, 36f), Is.False, "108 units but age 0.30 < 0.54");
            AssertFace(pet, 0.30f, AffectionFace.Smile);

            Assert.That(pet.Stroke(0.56f, 10f), Is.True, "118 units, age 0.56, smile visible 0.36");
            AssertFace(pet, 0.56f, AffectionFace.Laugh);
            AssertWag(pet, 0.56f, 1f);
        }

        [Test]
        public void Laugh_ExpiresAfterItsLifeAndRelaxesToSmile()
        {
            var pet = Laughing(); // last move 0.60, laugh until 1.38

            AssertFace(pet, 1.30f, AffectionFace.Laugh);
            AssertFace(pet, 1.40f, AffectionFace.Smile);  // relaxed to smile
            AssertFace(pet, 2.38f, AffectionFace.Smile);  // smile tucked until 0.60 + 1.8
            AssertFace(pet, 3.20f, AffectionFace.Calm);
        }

        [Test]
        public void HoldingStill_DoesNotRefreshLaugh()
        {
            var pet = Laughing(); // laugh until 1.38

            Assert.That(pet.Stroke(1.00f, 0f), Is.False, "zero distance is not movement");
            AssertFace(pet, 1.30f, AffectionFace.Laugh);
            AssertFace(pet, 1.40f, AffectionFace.Smile); // no extension from holding still
        }

        [Test]
        public void LongGap_DropsPartialStreak()
        {
            var pet = new PetAffectionReaction();
            pet.Begin(0f);
            pet.Stroke(0f, 2f);
            pet.Stroke(0.04f, 30f);
            pet.Stroke(0.10f, 30f); // 60 units, age 0.10
            AssertFace(pet, 0.10f, AffectionFace.Calm);

            Assert.That(pet.Stroke(0.60f, 5f), Is.False, "gap 0.50 > 0.42 restarts the streak");
            AssertFace(pet, 0.60f, AffectionFace.Calm);

            Assert.That(pet.Stroke(0.80f, 40f), Is.False, "only 45 units in the new streak");
            AssertFace(pet, 0.80f, AffectionFace.Smile); // smile re-earned, no laugh
        }

        [Test]
        public void SingleEventDistance_IsCapped()
        {
            var pet = new PetAffectionReaction();
            pet.Begin(0f);
            pet.Stroke(0f, 2f);

            Assert.That(pet.Stroke(0.10f, 500f), Is.False);
            Assert.That(pet.Stroke(0.20f, 500f), Is.False); // 72 units only
            AssertFace(pet, 0.20f, AffectionFace.Smile);

            Assert.That(pet.Stroke(0.60f, 500f), Is.True); // 108 units -> laugh
            AssertFace(pet, 0.60f, AffectionFace.Laugh);
        }

        [Test]
        public void NonPositiveAndNonFiniteDistance_IsIgnored()
        {
            var pet = new PetAffectionReaction();
            pet.Begin(0f);
            pet.Stroke(0f, 2f);

            Assert.That(pet.Stroke(0.10f, -5f), Is.False);
            Assert.That(pet.Stroke(0.20f, float.NaN), Is.False);
            Assert.That(pet.Stroke(0.30f, float.PositiveInfinity), Is.False);
            AssertFace(pet, 0.30f, AffectionFace.Calm);
        }

        [Test]
        public void RepeatPulse_NeedsBothTimeAndDistance()
        {
            var pet = Laughing(); // pulse at 0.60

            Assert.That(pet.Stroke(0.70f, 20f), Is.False, "20px, 0.10s since pulse");
            Assert.That(pet.Stroke(0.90f, 20f), Is.False, "40px, 0.30s since pulse");
            Assert.That(pet.Stroke(1.10f, 20f), Is.False, "60px but only 0.50s since pulse");

            Assert.That(pet.Stroke(1.26f, 20f), Is.True, "80px, 0.65s since pulse -> renewal");
            AssertFace(pet, 1.30f, AffectionFace.Laugh); // renewed until 2.03
        }


        [Test]
        public void Release_LingersSmileThenCalms()
        {
            var pet = Smiling();
            AssertFace(pet, 0.25f, AffectionFace.Smile);

            pet.End(0.25f); // smile until 2.05
            AssertFace(pet, 0.30f, AffectionFace.Smile);
            AssertFace(pet, 2.04f, AffectionFace.Smile);
            AssertFace(pet, 2.10f, AffectionFace.Calm);
        }

        [Test]
        public void Release_FromCalm_ForcesNoSmile()
        {
            var pet = new PetAffectionReaction();
            pet.Begin(0f);
            pet.Stroke(0f, 2f);
            pet.Stroke(0.05f, 5f);
            pet.End(0.10f);

            AssertFace(pet, 0.20f, AffectionFace.Calm);
            AssertFace(pet, 1.90f, AffectionFace.Calm);
            AssertWag(pet, 0.20f, 0f);
        }

        [Test]
        public void Release_FromLaughImmediatelySettlesToSmile()
        {
            var pet = Laughing();

            pet.End(.66f);

            AssertFace(pet, .66f, AffectionFace.Smile);
            AssertFace(pet, 2.47f, AffectionFace.Calm);
        }

        [Test]
        public void ReTouchWithoutMovement_DoesNotExtendSmile()
        {
            var pet = Smiling();
            pet.End(0.25f); // smile until 2.05

            pet.Begin(1.00f); // silent re-touch, no movement
            AssertFace(pet, 1.50f, AffectionFace.Smile); // leftover linger still visible
            AssertFace(pet, 2.10f, AffectionFace.Calm);  // but never extended
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var pet = Laughing();
            AssertFace(pet, 0.61f, AffectionFace.Laugh);

            pet.Reset();

            AssertFace(pet, 0.61f, AffectionFace.Calm);
            AssertWag(pet, 0.61f, 0f);
            Assert.That(pet.Stroke(0.70f, 10f), Is.False, "no contact after reset");
            Assert.That(pet.Preview(0.70f), Is.True, "preview is free again");
        }


        [Test]
        public void Preview_PlaysFullSequence()
        {
            var pet = new PetAffectionReaction();

            Assert.That(pet.Preview(0f), Is.True);
            AssertFace(pet, 0f, AffectionFace.Smile);
            AssertFace(pet, 0.20f, AffectionFace.Smile);
            AssertFace(pet, 0.40f, AffectionFace.Laugh);
            AssertFace(pet, 1.00f, AffectionFace.Laugh);
            AssertFace(pet, 1.05f, AffectionFace.Smile);
            AssertFace(pet, 2.79f, AffectionFace.Smile);
            AssertFace(pet, 2.85f, AffectionFace.Calm);
            Assert.That(pet.Preview(3.00f), Is.True, "a new preview may start once the old one ended");
        }

        [Test]
        public void Preview_RejectsConcurrentAndInsideTouch()
        {
            var pet = new PetAffectionReaction();
            Assert.That(pet.Preview(0f), Is.True);
            Assert.That(pet.Preview(0.10f), Is.False, "concurrent preview is rejected");

            var touched = new PetAffectionReaction();
            touched.Begin(0f);
            Assert.That(touched.Preview(0.10f), Is.False, "no preview while a real contact runs");
        }

        [Test]
        public void Preview_DoesNotFakeContact()
        {
            var pet = new PetAffectionReaction();
            Assert.That(pet.Preview(0f), Is.True);

            Assert.That(pet.Stroke(0.60f, 36f), Is.False, "preview never takes strokes");

            AssertFace(pet, 0.60f, AffectionFace.Laugh); // preview timeline untouched
            Assert.That(pet.Wag(0.60f), Is.GreaterThan(.5f));

            Assert.That(pet.Stroke(3.00f, 36f), Is.False, "preview left no touch behind");
            AssertFace(pet, 3.00f, AffectionFace.Calm);
        }

        [Test]
        public void RealContact_CancelsButtonPreview()
        {
            var pet = new PetAffectionReaction();
            Assert.That(pet.Preview(0f), Is.True);

            pet.Begin(.10f);
            pet.End(.20f);

            Assert.That(pet.Face(.60f), Is.Not.EqualTo(AffectionFace.Laugh));
        }

        [Test]
        public void Wag_TracksCurrentFace()
        {
            var pet = new PetAffectionReaction();
            pet.Begin(0f);
            pet.Stroke(0f, 2f);
            pet.Stroke(0.10f, 40f);
            pet.Stroke(0.20f, 1f);
            AssertWag(pet, 0.20f, 0.30f); // smile wag

            pet.Stroke(0.30f, 36f);
            Assert.That(pet.Stroke(0.60f, 36f), Is.True); // 113 units -> laugh
            AssertWag(pet, 0.60f, 1f); // laugh wag
        }

        [Test]
        public void Wag_DecaysWhileIdle()
        {
            var pet = Laughing(); // last move 0.60, wag 1.0

            AssertWag(pet, 0.60f, 1f);
            Assert.That(pet.Wag(0.90f), Is.InRange(0.4f, 0.6f), "wag decays with idle");
            Assert.That(pet.Wag(1.30f), Is.LessThan(pet.Wag(0.90f)), "decay keeps falling");
        }

        [Test]
        public void Wag_ReleasesSmoothlyToZero()
        {
            var pet = Smiling();
            AssertWag(pet, 0.25f, 0.30f);

            pet.End(0.30f); // release over 0.30 .. 0.86
            AssertWag(pet, 0.30f, 0.30f);
            AssertWag(pet, 0.58f, 0.075f);
            AssertWag(pet, 0.86f, 0f);
            AssertWag(pet, 1.20f, 0f);
        }
    }
}
