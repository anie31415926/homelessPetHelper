namespace HomelessPetHelper.Modules.Station.UI
{
    public readonly struct StationCareAvailability
    {
        public bool CanWash { get; }
        public bool CanGroom { get; }

        public StationCareAvailability(bool canWash, bool canGroom)
        {
            CanWash = canWash;
            CanGroom = canGroom;
        }
    }
}
