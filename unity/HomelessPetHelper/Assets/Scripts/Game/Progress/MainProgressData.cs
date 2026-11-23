using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace HomelessPetHelper.Game.Progress
{
    // Version 2 adds retained field results and one resumable outing to the station aggregate.
    // Explicit fields and constructors preserve the contract on stripped/AOT builds.
    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class MainProgressData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public long ElapsedMinutes;
        [Preserve, JsonProperty(Required = Required.Always)] public int Capacity;
        [Preserve, JsonProperty(Required = Required.Always)] public RequestData[] Requests;
        [Preserve, JsonProperty(Required = Required.Always)] public ResidentData[] Residents;
        [Preserve, JsonProperty(Required = Required.Always)] public WorldFieldData Fields;
        [Preserve] public MainProgressData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class RequestData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public string Id;
        [Preserve, JsonProperty(Required = Required.Always)] public string TemplateId;
        [Preserve, JsonProperty(Required = Required.Always)] public string PointId;
        [Preserve, JsonProperty(Required = Required.Always)] public int Version;
        [Preserve, JsonProperty(Required = Required.Always)] public int State;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public string OwnerId;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public string ReceivingId;
        [Preserve] public RequestData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class AnimalData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public string Id;
        [Preserve, JsonProperty(Required = Required.Always)] public string DefinitionId;
        [Preserve, JsonProperty(Required = Required.Always)] public bool Hungry;
        [Preserve, JsonProperty(Required = Required.Always)] public int Emotion;
        [Preserve, JsonProperty(Required = Required.Always)] public int Stress;
        [Preserve, JsonProperty(Required = Required.Always)] public int Behavior;
        [Preserve, JsonProperty(Required = Required.Always)] public string[] HealthProblemIds;
        [Preserve, JsonProperty(Required = Required.Always)] public bool Touch;
        [Preserve, JsonProperty(Required = Required.Always)] public bool Wash;
        [Preserve, JsonProperty(Required = Required.Always)] public bool Groom;
        [Preserve, JsonProperty(Required = Required.Always)] public int Dirtiness;
        [Preserve, JsonProperty(Required = Required.Always)] public int LooseFur;
        [Preserve] public AnimalData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class ResidentData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public string RequestId;
        [Preserve, JsonProperty(Required = Required.Always)] public string OutingId;
        [Preserve, JsonProperty(Required = Required.Always)] public AnimalData Arrival;
        [Preserve, JsonProperty(Required = Required.Always)] public AnimalData Current;
        [Preserve, JsonProperty(Required = Required.Always)] public int IntakeAssessment;
        [Preserve, JsonProperty(Required = Required.Always)] public int IntakeDirtiness;
        [Preserve, JsonProperty(Required = Required.Always)] public string[] IntakeConditions;
        [Preserve, JsonProperty(Required = Required.Always)] public int Assessment;
        [Preserve, JsonProperty(Required = Required.AllowNull)] public string[] AssessedProblemIds;
        [Preserve, JsonProperty(Required = Required.Always)] public CoatData Coat;
        [Preserve] public ResidentData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class CoatData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public FieldData Dirt;
        [Preserve, JsonProperty(Required = Required.Always)] public FieldData Fur;
        [Preserve, JsonProperty(Required = Required.Always)] public int InitialDirtiness;
        [Preserve, JsonProperty(Required = Required.Always)] public int InitialLooseFur;
        [Preserve, JsonProperty(Required = Required.Always)] public int WashMinutes;
        [Preserve, JsonProperty(Required = Required.Always)] public int GroomMinutes;
        [Preserve, JsonProperty(Required = Required.Always)] public long WashSettled;
        [Preserve, JsonProperty(Required = Required.Always)] public long GroomSettled;
        [Preserve] public CoatData() { }
    }

    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class FieldData
    {
        [Preserve, JsonProperty(Required = Required.Always)] public int Width;
        [Preserve, JsonProperty(Required = Required.Always)] public int Height;
        [Preserve, JsonProperty(Required = Required.Always)] public byte[] Mask;
        [Preserve, JsonProperty(Required = Required.Always)] public byte[] Samples;
        [Preserve, JsonProperty(Required = Required.Always)] public long InitialAmount;
        [Preserve] public FieldData() { }
    }
}
