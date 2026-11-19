using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HomelessPetHelper.Editor.StationPreview.Loop
{
    /// <summary>Only explicitly configured healthy, route-free fixtures enter the provisional adapter.</summary>
    public sealed class RescueLoopConfig
    {
        private readonly Data _data;

        public RescueLoopConfig(TextAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            _data = JsonUtility.FromJson<Data>(asset.text);
            if (_data == null || _data.schemaVersion != 1 || _data.encounters == null)
                throw new ArgumentException("Invalid loop fixture configuration.");
            var keys = new HashSet<string>();
            foreach (Encounter entry in _data.encounters)
            {
                if (entry == null || entry.activityId != "parking-healthy" || !entry.routeFree ||
                    entry.animalDefinitionId != "cat.shy" || string.IsNullOrWhiteSpace(entry.sceneTemplateId) ||
                    !keys.Add(entry.sceneTemplateId + ":" + entry.animalDefinitionId))
                    throw new ArgumentException("Unsupported or duplicate loop encounter.");
            }
        }

        public bool Supports(IReadOnlyList<string> scenes, string animalDefinitionId) =>
            _data.encounters.Any(entry => entry.animalDefinitionId == animalDefinitionId &&
                scenes.Contains(entry.sceneTemplateId));

        [Serializable]
        private sealed class Data
        {
            public int schemaVersion = 0;
            public Encounter[] encounters = Array.Empty<Encounter>();
        }

        [Serializable]
        private sealed class Encounter
        {
            public string sceneTemplateId = string.Empty;
            public string animalDefinitionId = string.Empty;
            public string activityId = string.Empty;
            public bool routeFree = false;
        }
    }
}
