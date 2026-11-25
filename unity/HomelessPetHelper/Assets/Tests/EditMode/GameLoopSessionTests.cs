using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Station;
using System;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Editor.CityMap;
using HomelessPetHelper.Editor.RescueCase;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class GameLoopSessionTests
    {
        [Test]
        public void AlternateActivityNeedsNoCardTypesAndTransfersOneIndividualExactlyOnce()
        {
            using (GameLoopSession loop = Create())
            {
                Guid outing = Guid.NewGuid();
                Assert.That(loop.TryOpenMap(outing, 17, out _), Is.True);
                Assert.That(loop.Outing.TrySelectAndDepart(outing, "parking", out _), Is.True);
                var activity = new TestActivity(outing);
                activity.SetSuspended(true);
                Assert.That(loop.TryEnterRouteFreeRescue(activity, out _), Is.False);
                activity.SetSuspended(false);
                AnimalSnapshot initial = activity.Individual.Current;
                Assert.That(loop.TryEnterRouteFreeRescue(activity, out _), Is.True);
                Assert.That(loop.TryConfirmRescue(out var early), Is.False);
                Assert.That(early, Is.EqualTo(OutingAssemblyFailure.RescueNotComplete));
                activity.Complete = true;
                Assert.That(loop.TryConfirmRescue(out _), Is.True);
                Assert.That(loop.TryReturnRescued(Condition(), out _), Is.True);
                Assert.That(activity.DisposeCount, Is.EqualTo(1));
                Assert.That(loop.Clock.Current.ElapsedMinutes,
                    Is.EqualTo(RouteFreeDuration(OutingReturnProgress.HandingOver)), "No opening-route charge.");
                Assert.That(loop.LastArrival.Arrival.AnimalAtArrival, Is.SameAs(initial));
                Assert.That(loop.Station.TryGetCurrentAnimal(initial.AnimalId, out var resident), Is.True);
                Assert.That(resident, Is.SameAs(initial));
                Assert.That(loop.TryReturnRescued(Condition(), out _), Is.False);
                Assert.That(loop.Station.Animals.Count, Is.EqualTo(1));
                Assert.That(loop.TryOpenMap(Guid.NewGuid(), 17, out _), Is.False);
                Assert.That(loop.Station.TryAssessKnownHealthyAnimal(loop.StationSessionId, initial.AnimalId,
                    out _), Is.True);
                Assert.That(loop.Outing.TryCompleteCare(outing, out _), Is.True);
                Assert.That(loop.TryOpenMap(Guid.NewGuid(), 17, out _), Is.True);
                Assert.That(loop.Station.TryGetCurrentAnimal(initial.AnimalId, out resident), Is.True);
                Assert.That(resident, Is.SameAs(initial));
                Assert.That(loop.TryReturnUnresolved(out _), Is.True);
                Assert.That(loop.Clock.Current.ElapsedMinutes,
                    Is.EqualTo(RouteFreeDuration(OutingReturnProgress.HandingOver)));
            }
        }

        [Test]
        public void InjectedAnimalIsNotResetBySampleStartAndUnknownCareDataIsNotPermission()
        {
            AnimalIndividual individual = NewAnimal();
            AnimalSnapshot before = individual.Current;
            using (var activity = new RescueCaseTrial(Guid.NewGuid(), 7, start: CaseStart.StressRecovery,
                standaloneStation: false, individual: individual))
            {
                Assert.That(activity.Individual, Is.SameAs(individual));
                Assert.That(activity.Individual.Current, Is.SameAs(before));
                Assert.That(activity.Animal.IsHungry, Is.False);
                Assert.That(activity.Animal.Stress, Is.EqualTo(CaseStress.None));
                Assert.That(activity.StationCare, Is.Null);
            }
            Assert.That(individual.Current, Is.SameAs(before));
            var unknown = new AnimalIndividual(Guid.NewGuid(), "cat.shy", before.Disposition, before.Health);
            Assert.That(unknown.Current.Coat, Is.Null);
            Assert.That(unknown.Current.CarePermissions.AcceptsTouch, Is.False);
            Assert.That(unknown.Current.CarePermissions.AcceptsWash, Is.False);
            Assert.That(unknown.Current.CarePermissions.AcceptsGroom, Is.False);
        }

        [Test]
        public void RefusalPauseAssistanceAndClosureDoNotFakeIntakeOrLoseRequest()
        {
            using (GameLoopSession loop = Create())
            {
                Guid outing = Guid.NewGuid();
                loop.TryOpenMap(outing, 3, out _);
                loop.Outing.TrySelectAndDepart(outing, "parking", out _);
                var wrong = new TestActivity(Guid.NewGuid());
                Assert.That(loop.TryEnterRouteFreeRescue(wrong, out _), Is.False);
                Assert.That(loop.Flow.Stage, Is.EqualTo(RescueStage.Approach));
                Assert.That(wrong.DisposeCount, Is.Zero, "Rejected activity still belongs to caller.");
                wrong.Dispose();
                var activity = new TestActivity(outing);
                loop.SetSuspended(true);
                Assert.That(loop.TryEnterRouteFreeRescue(activity, out _), Is.False);
                loop.SetSuspended(false);
                Assert.That(loop.TryEnterRouteFreeRescue(activity, out _), Is.True);
                loop.SetSuspended(true);
                Assert.That(activity.Suspended, Is.True);
                Assert.That(loop.TryReturnUnresolved(out _), Is.False);
                Assert.That(loop.Clock.Current.ElapsedMinutes, Is.Zero);
                loop.SetSuspended(false);
                activity.Assistance = true;
                Assert.That(loop.TryReturnUnresolved(out _), Is.True);
                Assert.That(loop.Station.Animals, Is.Empty);
                Assert.That(loop.Clock.Current.ElapsedMinutes,
                    Is.EqualTo(RouteFreeDuration(OutingReturnProgress.Negotiating)));
                Assert.That(loop.Map.TrySelectPoint("parking", out _, out _), Is.True);
                loop.Dispose();
                loop.Dispose();
                Assert.That(activity.DisposeCount, Is.EqualTo(1));
                Assert.That(loop.TryOpenMap(Guid.NewGuid(), 3, out _), Is.False);
            }
        }

        [Test]
        public void TypedAnimalSectionsPreserveEachOtherHistoryAndRejectOldWrites()
        {
            var animal = NewAnimal();
            AnimalSnapshot original = animal.Current;
            Assert.That(animal.TryApplyCoatCondition(original, new AnimalCoatCondition(40, 50)), Is.True);
            AnimalSnapshot cleaned = animal.Current;
            Assert.That(cleaned.Disposition, Is.SameAs(original.Disposition));
            Assert.That(cleaned.Health, Is.SameAs(original.Health));
            Assert.That(cleaned.CarePermissions, Is.SameAs(original.CarePermissions));
            Assert.That(original.Coat.Dirtiness, Is.EqualTo(65));
            Assert.That(animal.TryApplyCarePermissions(original, new AnimalCarePermissions(false, false, false)),
                Is.False);
            Assert.That(animal.TryApplyCarePermissions(cleaned, new AnimalCarePermissions(true, false, true)),
                Is.True);
            Assert.That(animal.Current.Coat, Is.SameAs(cleaned.Coat));
            Assert.That(animal.TryApplyDisposition(cleaned, original.Disposition), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnimalCoatCondition(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnimalCoatCondition(0, 101));
        }

        internal static GameLoopSession Create()
        {
            var config = new CityMapPreviewConfig(AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.ConfigPath));
            RequestPoolState requests = config.CreateRequestPool();
            return new GameLoopSession(config.InitialCapacity, config.MinutesPerDay,
                config.CreateRules(new PointUnlockState(), requests), requests, config.TimePolicy);
        }

        private static long RouteFreeDuration(OutingReturnProgress progress)
        {
            var config = new CityMapPreviewConfig(
                AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.ConfigPath));
            Assert.That(config.TimePolicy.TryGetDuration(progress, false, out long minutes, out _), Is.True);
            return minutes;
        }

        private static AnimalIntakeCondition Condition() =>
            new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 65, Array.Empty<string>());

        private static AnimalIndividual NewAnimal() => new AnimalIndividual(Guid.NewGuid(), "cat.shy",
            new AnimalDisposition(false, AnimalEmotion.Relaxed, AnimalStress.None, AnimalBehavior.Watching),
            new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()), new AnimalCarePermissions(true, true, true),
            new AnimalCoatCondition(65, 50));

        private sealed class TestActivity : IRescueActivity
        {
            public Guid SessionId { get; }
            public AnimalIndividual Individual { get; } = NewAnimal();
            public bool Complete { get; set; }
            public bool Assistance { get; set; }
            public bool Suspended { get; private set; }
            public bool IsSuspended => Suspended;
            public bool IsClosed => DisposeCount > 0;
            public int DisposeCount { get; private set; }
            public bool IsRescueComplete => Complete && !Suspended && DisposeCount == 0;
            public bool IsAssistanceRequested => Assistance;
            public TestActivity(Guid id) { SessionId = id; }
            public void SetSuspended(bool suspended) { Suspended = suspended; }
            public void Dispose() { DisposeCount++; }
        }
    }
}
