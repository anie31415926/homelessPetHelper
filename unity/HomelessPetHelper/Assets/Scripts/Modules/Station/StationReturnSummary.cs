namespace HomelessPetHelper.Modules.Station
{
    /// <summary>
    /// Historical return facts only. Closing or reopening a view must not readmit this animal.
    /// </summary>
    public sealed class StationReturnSummary
    {
        public StationAnimalIntake Arrival { get; }

        public int OccupiedTemporaryPlacesOnArrival { get; }

        public int TemporaryCapacityOnArrival { get; }

        internal StationReturnSummary(StationAnimalIntake arrival, int occupiedPlaces, int temporaryCapacity)
        {
            Arrival = arrival;
            OccupiedTemporaryPlacesOnArrival = occupiedPlaces;
            TemporaryCapacityOnArrival = temporaryCapacity;
        }
    }
}
