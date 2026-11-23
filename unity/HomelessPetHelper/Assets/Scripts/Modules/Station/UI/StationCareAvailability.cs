namespace HomelessPetHelper.Modules.Station.UI
{
    public readonly struct StationCareAvailability
    {
        public bool CanWash { get; }
        public bool CanGroom { get; }
        public string Explanation { get; }

        public StationCareAvailability(bool canWash, bool canGroom, string explanation = null)
        {
            CanWash = canWash;
            CanGroom = canGroom;
            Explanation = explanation;
        }
    }
}
