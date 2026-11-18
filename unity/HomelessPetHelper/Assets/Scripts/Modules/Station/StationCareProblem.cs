namespace HomelessPetHelper.Modules.Station
{
    /// <summary>A necessary arrangement, not a diagnosis or a claim of recovery.</summary>
    public sealed class StationCareProblem
    {
        public string Id { get; }
        public ProfessionalCareStatus Status { get; }

        internal StationCareProblem(string id, ProfessionalCareStatus status)
        {
            Id = id;
            Status = status;
        }
    }
}
