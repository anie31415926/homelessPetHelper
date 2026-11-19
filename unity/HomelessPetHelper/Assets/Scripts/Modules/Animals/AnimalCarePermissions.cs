namespace HomelessPetHelper.Modules.Animals
{
    /// <summary>Current willingness for distinct actions, not a diagnosis or transport permission.</summary>
    public sealed class AnimalCarePermissions
    {
        public bool AcceptsTouch { get; }
        public bool AcceptsWash { get; }
        public bool AcceptsGroom { get; }

        public AnimalCarePermissions(bool acceptsTouch, bool acceptsWash, bool acceptsGroom)
        {
            AcceptsTouch = acceptsTouch;
            AcceptsWash = acceptsWash;
            AcceptsGroom = acceptsGroom;
        }
    }
}
