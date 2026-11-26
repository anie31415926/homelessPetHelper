namespace HomelessPetHelper.Modules.Station.UI
{
    public readonly struct StationCareAvailability
    {
        public bool CanWash { get; }
        public bool CanGroom { get; }
        public string Explanation { get; }
        public bool ExplainWashOnClick { get; }
        public bool ExplainGroomOnClick { get; }

        public StationCareAvailability(bool canWash, bool canGroom, string explanation = null,
            bool explainWashOnClick = false, bool explainGroomOnClick = false)
        {
            CanWash = canWash;
            CanGroom = canGroom;
            Explanation = explanation;
            ExplainWashOnClick = explainWashOnClick;
            ExplainGroomOnClick = explainGroomOnClick;
        }
    }
}
