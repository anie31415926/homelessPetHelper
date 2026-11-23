using System;
using System.Linq;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.Cleaning;

namespace HomelessPetHelper.Game.Progress
{
    internal static class MainResidentCodec
    {
        internal static ResidentData Capture(StationAnimalIntake arrival, AnimalSnapshot current,
            StationCareSnapshot care, StationCoatCare coatCare, Guid requestId)
        {
            var coat = coatCare.Capture();
            return new ResidentData
            {
                RequestId = requestId.ToString("N"), OutingId = arrival.OutingId.ToString("N"),
                Arrival = CaptureAnimal(arrival.AnimalAtArrival), Current = CaptureAnimal(current),
                IntakeAssessment = (int)arrival.Condition.HealthAssessment,
                    IntakeDirtiness = arrival.Condition.Dirtiness,
                IntakeConditions = arrival.Condition.KnownConditionIds.ToArray(),
                    Assessment = (int)care.BasicAssessment,
                AssessedProblemIds = care.HealthAtAssessment?.Problems.Select(problem => problem.InstanceId).ToArray(),
                Coat = new CoatData
                {
                    Dirt = CaptureField(coat.CopyDirt()), Fur = CaptureField(coat.CopyFur()),
                    InitialDirtiness = coat.InitialCoat.Dirtiness, InitialLooseFur = coat.InitialCoat.LooseFur,
                    WashMinutes = coat.WashMinutes, GroomMinutes = coat.GroomMinutes,
                    WashSettled = coat.WashSettledMinutes, GroomSettled = coat.GroomSettledMinutes
                }
            };
        }

        internal static StationResidentState Restore(ResidentData data)
        {
            if (!Guid.TryParseExact(data.OutingId, "N", out var outingId) || data.Coat == null ||
                data.Arrival == null || data.IntakeConditions == null || data.IntakeConditions.Length != 0 ||
                data.IntakeAssessment != (int)StationHealthAssessment.Unchecked ||
                data.IntakeDirtiness != data.Arrival.Dirtiness ||
                data.Coat.InitialDirtiness != data.Arrival.Dirtiness ||
                data.Coat.InitialLooseFur != data.Arrival.LooseFur ||
                (data.AssessedProblemIds != null && data.AssessedProblemIds.Length != 0))
                throw new ArgumentException("Invalid healthy arrival/care history.");
            var arrival = RestoreAnimal(data.Arrival).Current;
            var current = RestoreAnimal(data.Current);
            var intake = StationAnimalIntake.Restore(outingId, arrival, current,
                new AnimalIntakeCondition((StationHealthAssessment)data.IntakeAssessment,
                    data.IntakeDirtiness, data.IntakeConditions));
            return new StationResidentState(intake, (StationHealthAssessment)data.Assessment,
                data.AssessedProblemIds == null ? null : new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()));
        }

        internal static StationCoatCareState RestoreCoat(CoatData data)
        {
            if (data == null) throw new ArgumentException("Missing coat state.");
            return new StationCoatCareState(RestoreField(data.Dirt), RestoreField(data.Fur),
                new AnimalCoatCondition(data.InitialDirtiness, data.InitialLooseFur),
                data.WashMinutes, data.GroomMinutes, data.WashSettled, data.GroomSettled);
        }

        internal static void ValidateMask(StationCoatCareState state, string definitionId,
            StationArtwork artwork, CityMapPreviewConfig config)
        {
            var presentation = config.GetAnimalPresentation(definitionId) ??
                throw new ArgumentException("Unknown resident presentation.");
            artwork.Get(presentation.DogArtwork, out var mask);
            foreach (var field in new[] { state.CopyDirt(), state.CopyFur() })
                if (field.Width != 128 || field.Height != 128 || !field.CopyMask().SequenceEqual(mask))
                    throw new ArgumentException("Coat mask is incompatible with current content.");
        }

        internal static AnimalData CaptureAnimal(AnimalSnapshot animal)
        {
            if (animal == null || animal.Coat == null || animal.Health.Problems.Count != 0)
                throw new ArgumentException("Only explicit healthy Main animals are supported by version 1.");
            return new AnimalData
            {
                Id = animal.AnimalId.ToString("N"), DefinitionId = animal.DefinitionId,
                Hungry = animal.Disposition.IsHungry, Emotion = (int)animal.Disposition.Emotion,
                Stress = (int)animal.Disposition.Stress, Behavior = (int)animal.Disposition.Behavior,
                HealthProblemIds = Array.Empty<string>(), Touch = animal.CarePermissions.AcceptsTouch,
                Wash = animal.CarePermissions.AcceptsWash, Groom = animal.CarePermissions.AcceptsGroom,
                Dirtiness = animal.Coat.Dirtiness, LooseFur = animal.Coat.LooseFur
            };
        }

        internal static AnimalIndividual RestoreAnimal(AnimalData data)
        {
            if (data == null || !Guid.TryParseExact(data.Id, "N", out var id) || data.HealthProblemIds == null ||
                data.HealthProblemIds.Length != 0)
                throw new ArgumentException("Missing or unsupported individual health snapshot.");
            return new AnimalIndividual(id, data.DefinitionId,
                new AnimalDisposition(data.Hungry, (AnimalEmotion)data.Emotion, (AnimalStress)data.Stress,
                    (AnimalBehavior)data.Behavior), new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()),
                new AnimalCarePermissions(data.Touch, data.Wash, data.Groom),
                new AnimalCoatCondition(data.Dirtiness, data.LooseFur));
        }

        private static FieldData CaptureField(DirtField field) => new FieldData
        {
            Width = field.Width, Height = field.Height, Mask = field.CopyMask(), Samples = field.CopySamples(),
            InitialAmount = field.InitialAmount
        };

        private static DirtField RestoreField(FieldData field)
        {
            if (field == null) throw new ArgumentException("Missing coat field.");
            return DirtField.Restore(field.Width, field.Height, field.Mask, field.Samples, field.InitialAmount);
        }
    }
}
