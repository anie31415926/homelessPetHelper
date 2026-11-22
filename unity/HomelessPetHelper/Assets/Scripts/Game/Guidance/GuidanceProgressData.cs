using System;
using Newtonsoft.Json;

namespace HomelessPetHelper.Game.Guidance
{
    [Serializable]
    public sealed class GuidanceProgressData
    {
        [JsonProperty(Required = Required.Always)]
        public string[] Completed = Array.Empty<string>();
    }
}
