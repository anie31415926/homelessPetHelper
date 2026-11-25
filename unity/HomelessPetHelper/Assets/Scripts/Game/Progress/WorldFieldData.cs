using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace HomelessPetHelper.Game.Progress
{
    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class WorldFieldData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public EncounterData[] Encounters;
        [Preserve, JsonProperty(Required = Required.Always)] public LocationData[] Locations;
        [Preserve, JsonProperty(Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public ExplorationData[] Exploration;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public ActiveOutingData Active;
        [Preserve] public WorldFieldData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class ExplorationData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public string PointId, LayoutId, ContentVersion, TemplateId;
        [Preserve, JsonProperty(Required = Required.Always)] public int Mode;
        [Preserve, JsonProperty(Required = Required.Always)] public OpeningData Opening;
        [Preserve] public ExplorationData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class EncounterData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public string RequestId;
        [Preserve, JsonProperty(Required = Required.Always)] public string PointId;
        [Preserve, JsonProperty(Required = Required.Always)] public int Seed;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public string LayoutId;
        [Preserve, JsonProperty(Required = Required.Always)] public string ContentVersion;
        [Preserve, JsonProperty(Required = Required.Always)] public string RuleVersion;
        [Preserve, JsonProperty(Required = Required.Always)] public string TemplateId;
        [Preserve, JsonProperty(Required = Required.Always)] public int Mode;
        [Preserve, JsonProperty(Required = Required.Always)] public AnimalData Animal;
        [Preserve, JsonProperty(Required = Required.Always)] public OpeningData Opening;
        [Preserve] public EncounterData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class LocationData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public string RequestId;
        [Preserve, JsonProperty(Required = Required.Always)] public string PointId;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public string LayoutId;
        [Preserve, JsonProperty(Required = Required.Always)] public string ContentVersion;
        [Preserve, JsonProperty(Required = Required.Always)] public string TemplateId;
        [Preserve, JsonProperty(Required = Required.Always)] public int Mode;
        [Preserve, JsonProperty(Required = Required.Always)] public OpeningData Opening;
        [Preserve, JsonProperty(Required = Required.Always)] public long UpdatedMinutes;
        [Preserve] public LocationData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class ActiveOutingData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public string OutingId;
        [Preserve, JsonProperty(Required = Required.Always)] public string RequestId;
        [Preserve, JsonProperty(Required = Required.Always)] public int OutingSeed;
        [Preserve, JsonProperty(Required = Required.Always)] public bool OpenedRoute;
        [Preserve, JsonProperty(Required = Required.Always)] public int Stage;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public BattleReplayData Battle;
        [Preserve] public ActiveOutingData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class OpeningData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public int[] Boxes;
        [Preserve, JsonProperty(Required = Required.Always)] public int Bridge;
        [Preserve, JsonProperty(Required = Required.Always)] public double[] Remaining;
        [Preserve, JsonProperty(Required = Required.Always)] public PuzzleUndoData[] Undo;
        [Preserve] public OpeningData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class PuzzleUndoData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public int[] Boxes;
        [Preserve, JsonProperty(Required = Required.Always)] public int Bridge;
        [Preserve] public PuzzleUndoData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class BattleReplayData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public int SimulationVersion;
        [Preserve, JsonProperty(Required = Required.Always)] public ReplayStepData[] Steps;
        [Preserve, JsonProperty(Required = Required.Always)] public double AccumulatorMs;
        [Preserve] public BattleReplayData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class ReplayStepData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public int Ticks;
        [Preserve, JsonProperty] public bool Realtime;
        [Preserve, JsonProperty(Required = Required.Always)] public bool PauseAtDecision;
        [Preserve, JsonProperty(Required = Required.Always)] public bool DecisionHeld;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public OrderData Order;
        [Preserve] public ReplayStepData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class OrderData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public string SessionId;
        [Preserve, JsonProperty(Required = Required.Always)] public int Turn;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public int? DeployCardId;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public string DeployLaneId;
        [Preserve, JsonProperty] public double? DeployX;
        [Preserve, JsonProperty] public double? DeployY;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public int[] MergeIds;
        [Preserve, JsonProperty(Required = Required.Always)] public int AnchorId;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public int? RerouteUnitId;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public string RerouteLaneId;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public int? DiscardCardId;
        [Preserve] public OrderData() { }
    }
}
