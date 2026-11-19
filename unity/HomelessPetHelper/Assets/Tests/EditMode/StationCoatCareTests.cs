using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.Cleaning;
using HomelessPetHelper.Modules.WorldTime;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class StationCoatCareTests
    {
        [Test]
        public void PartialCareChangesCurrentCoatOnlyAndRetainsWorkWithoutDoubleCharging()
        {
            var f = new Fixture();
            AnimalSnapshot arrival = f.Animal.Current;
            Assert.That(f.Care.TryBegin(false, out var failure), Is.False, "Basic check is required.");
            Assert.That(failure, Is.EqualTo(StationCoatCareFailure.AssessmentRequired));
            f.Check();
            Assert.That(f.Care.TryBegin(false, out _), Is.True);
            Assert.That(f.Care.TryStroke(0.5f, 0.5f, 255), Is.True);
            Assert.That(f.Care.TryStroke(0.3f, 0.3f, 255), Is.True);
            AnimalSnapshot partial = f.Animal.Current;
            Assert.That(partial.Coat.Dirtiness, Is.LessThan(65));
            Assert.That(partial.Coat.LooseFur, Is.EqualTo(50));
            Assert.That(partial.Disposition, Is.SameAs(arrival.Disposition));
            Assert.That(partial.Health, Is.SameAs(arrival.Health));
            Assert.That(f.Summary.Arrival.AnimalAtArrival, Is.SameAs(arrival));
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.Zero);
            f.Clock.SetSuspended(true);
            long amount = f.Care.Dirt.RemainingAmount;
            Assert.That(f.Care.TryStroke(0.3f, 0.3f, 255), Is.False);
            Assert.That(f.Care.TryEnd(out _, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationCoatCareFailure.Suspended));
            Assert.That(f.Care.Dirt.RemainingAmount, Is.EqualTo(amount));
            f.Clock.SetSuspended(false);
            Assert.That(f.Care.TryEnd(out long minutes, out _), Is.True);
            Assert.That(minutes, Is.GreaterThan(0));
            Assert.That(f.Care.TryEnd(out _, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationCoatCareFailure.NotActive));
            Assert.That(f.Care.TryBegin(false, out _), Is.True);
            Assert.That(f.Care.TryEnd(out long repeated, out _), Is.True);
            Assert.That(repeated, Is.Zero);
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
            Assert.That(f.Animal.Current, Is.SameAs(partial));
        }

        [Test]
        public void DifferentPermissionsWrongSessionHealthAndStaleCoatCannotBeBypassed()
        {
            var f = new Fixture();
            f.Check();
            var current = f.Animal.Current;
            f.Animal.TryApplyCarePermissions(current, new AnimalCarePermissions(true, false, true));
            Assert.That(f.Care.TryBegin(false, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationCoatCareFailure.WashNotAccepted));
            Assert.That(f.Care.CanBegin(false), Is.False);
            Assert.That(f.Care.CanBegin(true), Is.True);
            Assert.That(f.Care.TryBegin(true, out _), Is.True);
            Assert.That(f.Care.TryStroke(0.5f, 0.5f, 255), Is.False, "Dirt blocks grooming.");
            Assert.That(f.Care.TryEnd(out var minutes, out _), Is.True);
            Assert.That(minutes, Is.Zero);
            var wrong = f.CreateCare(Guid.NewGuid());
            Assert.That(wrong.TryBegin(true, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationCoatCareFailure.SessionUnavailable));
            f.Animal.TryApplyCoatCondition(f.Animal.Current, new AnimalCoatCondition(10, 30));
            Assert.That(f.Care.TryBegin(true, out failure), Is.False, "Stale fields must not overwrite a newer coat.");
            Assert.That(failure, Is.EqualTo(StationCoatCareFailure.CoatChanged));
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.Zero);
            var unhealthy = new Fixture(new[] { new AnimalHealthProblem("one", "test.problem") });
            Assert.That(unhealthy.Station.TryAssessKnownHealthyAnimal(unhealthy.Id,
                unhealthy.Animal.Current.AnimalId, out _), Is.False);
            Assert.That(unhealthy.Care.TryBegin(false, out _), Is.False);
        }

        [Test]
        public void StaleTimeAndClosedStationRefuseEndingWithoutConfirmingPendingWork()
        {
            var f = new Fixture();
            f.Check();
            f.Care.TryBegin(false, out _);
            f.Care.TryStroke(0.5f, 0.5f, 255);
            f.Care.TryStroke(0.3f, 0.3f, 255);
            Assert.That(f.Clock.TryAdvance(f.Clock.Current.Version, 1, out _, out _), Is.True);
            Assert.That(f.Care.TryEnd(out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationCoatCareFailure.TimeVersionStale));
            Assert.That(f.Care.IsActive, Is.True);
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.EqualTo(1));
            f.Station.Close();
            Assert.That(f.Care.TryStroke(0.2f, 0.2f, 255), Is.False);
            Assert.That(f.Care.TryEnd(out _, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationCoatCareFailure.SessionUnavailable));
        }

        [Test]
        public void ConstructorInputsCannotMutateOwnedFieldsAndReadOnlyViewsTrackAuthorizedWork()
        {
            var f = new Fixture();
            f.Check();
            var dirt = Fixture.Field();
            var fur = Fixture.Field();
            var care = new StationCoatCare(f.Station, f.Id, f.Animal.Current.AnimalId, f.Clock, dirt, fur, 20, 12);
            IReadOnlyDirtField view = care.Dirt;
            Assert.That(view, Is.Not.InstanceOf<DirtField>());
            Assert.That(care.Fur, Is.Not.InstanceOf<DirtField>());
            long amount = view.RemainingAmount;
            AnimalSnapshot before = f.Animal.Current;
            dirt.ClearResidue();
            fur.TryErase(0.5f, 0.5f, 1, 255);
            Assert.That(view.RemainingAmount, Is.EqualTo(amount));
            Assert.That(care.Fur.RemainingAmount, Is.EqualTo(amount));
            Assert.That(f.Animal.Current, Is.SameAs(before));
            Assert.That(care.TryStroke(0.5f, 0.5f, 255), Is.False, "No active session.");
            Assert.That(care.TryBegin(false, out _), Is.True);
            Assert.That(care.TryStroke(0.5f, 0.5f, 255), Is.True);
            Assert.That(care.Dirt, Is.SameAs(view));
            Assert.That(view.RemainingAmount, Is.LessThan(amount));
            Assert.That(view.Sample(44), Is.Zero);
            Assert.That(f.Animal.Current.Coat.Dirtiness, Is.LessThan(before.Coat.Dirtiness));
            Assert.That(care.TryStroke(0.3f, 0.3f, 255), Is.True);
            f.Clock.SetSuspended(true);
            amount = view.RemainingAmount;
            Assert.That(care.TryStroke(0.3f, 0.3f, 255), Is.False);
            Assert.That(view.RemainingAmount, Is.EqualTo(amount));
            f.Clock.SetSuspended(false);
            Assert.That(care.TryEnd(out long minutes, out _), Is.True);
            Assert.That(minutes, Is.GreaterThan(0));
        }

        [Test]
        public void OwnedCopyPreservesInitialAndRemainingAmountsOfPartlyWorkedFields()
        {
            var f = new Fixture();
            var dirt = Fixture.Field();
            dirt.TryErase(0.5f, 0.5f, 0.2f, 100);
            var care = new StationCoatCare(f.Station, f.Id, f.Animal.Current.AnimalId, f.Clock,
                dirt, Fixture.Field(), 20, 12);
            Assert.That(care.Dirt.InitialAmount, Is.EqualTo(dirt.InitialAmount));
            Assert.That(care.Dirt.RemainingAmount, Is.EqualTo(dirt.RemainingAmount));
            Assert.That(care.Dirt.RemovedFraction, Is.EqualTo(dirt.RemovedFraction));
            dirt.ClearResidue();
            Assert.That(care.Dirt.RemainingAmount, Is.GreaterThan(0));
        }

        private sealed class Fixture
        {
            public Guid Id { get; } = Guid.NewGuid();
            public RescueStation Station { get; }
            public AnimalIndividual Animal { get; }
            public ActionWorldClock Clock { get; }
            public StationReturnSummary Summary { get; }
            public StationCoatCare Care { get; }

            public Fixture(AnimalHealthProblem[] problems = null)
            {
                Station = new RescueStation(Id, 1);
                Clock = new ActionWorldClock(Guid.NewGuid(), 1440);
                Animal = new AnimalIndividual(Guid.NewGuid(), "cat.shy",
                    new AnimalDisposition(false, AnimalEmotion.Relaxed, AnimalStress.None, AnimalBehavior.Watching),
                    new AnimalHealthSnapshot(problems ?? Array.Empty<AnimalHealthProblem>()),
                    new AnimalCarePermissions(true, true, true), new AnimalCoatCondition(65, 50));
                Station.TryReceive(Id, new StationAnimalIntake(Guid.NewGuid(), Animal,
                    new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 65, Array.Empty<string>())),
                    out var summary, out _);
                Summary = summary;
                Care = CreateCare(Id);
            }

            public StationCoatCare CreateCare(Guid session) => new StationCoatCare(Station, session,
                Animal.Current.AnimalId, Clock, Field(), Field(), 20, 12);
            public void Check() => Assert.That(Station.TryAssessKnownHealthyAnimal(Id,
                Animal.Current.AnimalId, out _), Is.True);
            internal static DirtField Field()
            {
                var mask = new byte[100];
                var samples = new byte[100];
                for (int i = 0; i < 100; i++) { mask[i] = 255; samples[i] = 100; }
                return new DirtField(10, 10, mask, samples);
            }
        }
    }
}
