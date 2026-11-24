namespace HomelessPetHelper.Modules.Siege
{
    public sealed class SiegeOrder
    {
        public const int NewUnit = -1;
        public string SessionId { get; set; }
        public int Turn { get; set; }
        public int? DeployCardId { get; set; }
        public string DeployLaneId { get; set; }
        public SiegePoint? DeployPoint { get; set; }
        public int[] MergeIds { get; set; }
        public int AnchorId { get; set; }
        public int? RerouteUnitId { get; set; }
        public string RerouteLaneId { get; set; }
        public int? DiscardCardId { get; set; }
    }
}
