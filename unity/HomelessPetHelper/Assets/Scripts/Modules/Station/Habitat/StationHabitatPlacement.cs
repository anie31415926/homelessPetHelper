using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Modules.Station.Habitat
{
    /// <summary>Read-only presentation gate, not a recovery or cage-capacity system.</summary>
    public static class StationHabitatPlacement
    {
        public static bool NeedsObservation(StationHealthAssessment assessment, AnimalSnapshot animal)
        {
            return animal == null || assessment != StationHealthAssessment.NoUrgentCareRequired ||
                animal.Health.Problems.Count != 0 || animal.Disposition.Stress == AnimalStress.Acute;
        }
    }
}
