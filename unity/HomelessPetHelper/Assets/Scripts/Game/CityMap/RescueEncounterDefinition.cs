namespace HomelessPetHelper.Game.CityMap
{
    /// <summary>Explicit, provisional healthy encounters. Rescue victory never supplies a diagnosis.</summary>
    public sealed class RescueEncounterDefinition
    {
        public string TemplateId { get; }
        public string AnimalDefinitionId { get; }
        public string EncounterId { get; }
        public bool DogArtwork { get; }
        public bool KnownHealthy { get; }
        public string RescueProfile { get; }
        public bool IsHungry { get; }
        public bool AcceptsTouch { get; }
        public bool AcceptsWash { get; }
        public bool AcceptsGroom { get; }
        public int WashMinutes { get; }
        public int GroomMinutes { get; }
        public int Dirtiness { get; }
        public int LooseFur { get; }

        internal RescueEncounterDefinition(CityMapPreviewConfigData.RescueEncounter source)
        {
            TemplateId = source.templateId;
            AnimalDefinitionId = source.animalDefinitionId;
            EncounterId = source.encounterId;
            DogArtwork = source.dogArtwork;
            KnownHealthy = source.knownHealthy;
            RescueProfile = source.rescueProfile;
            IsHungry = source.isHungry;
            AcceptsTouch = source.acceptsTouch;
            AcceptsWash = source.acceptsWash;
            AcceptsGroom = source.acceptsGroom;
            WashMinutes = source.washMinutes;
            GroomMinutes = source.groomMinutes;
            Dirtiness = source.dirtiness;
            LooseFur = source.looseFur;
        }
    }
}
