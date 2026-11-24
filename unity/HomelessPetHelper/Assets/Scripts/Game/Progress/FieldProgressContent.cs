using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Game.CityMap;

namespace HomelessPetHelper.Game.Progress
{
    /// <summary>Explicit content binding for saved field instances; incompatible content never rerolls.</summary>
    public sealed class FieldProgressContent
    {
        private readonly GameContent _content;
        private readonly OutingFieldCatalog _fields;
        private readonly ApproachCatalog _fallback;
        private readonly string _battleVersion, _openingVersion, _legacyBattleVersion;
        private readonly Dictionary<string, SiegeCatalog> _battles = new Dictionary<string, SiegeCatalog>();

        internal FieldProgressContent(GameContent content, OutingFieldCatalog fields, string battle, string opening)
        {
            _content = content;
            _fields = fields;
            _fallback = content.CreateApproachCatalog();
            _battleVersion = Hash(battle);
            // Exact shipped v1 catalog hash; only this known configuration is compatible.
            _legacyBattleVersion = "479fd4be8dfb8f8fd0a03c4e34c30dd43ca4413c49b7c21d182394478d99fd25";
            _openingVersion = Hash(opening);
        }

        internal static string Hash(string value)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "")
                .ToLowerInvariant();
        }

        internal OutingFieldLayout Layout(string pointId, string layoutId)
        {
            if (layoutId == null) return null;
            return _fields.Find(pointId, layoutId) ?? throw new ArgumentException("Saved field layout is unavailable.");
        }

        internal string Version(string pointId, string layoutId) =>
            Hash((Layout(pointId, layoutId)?.ContentVersion ?? _openingVersion) + _battleVersion);

        internal bool IsCompatibleVersion(string pointId, string layoutId, string version) =>
            version == Version(pointId, layoutId) || version ==
                Hash((Layout(pointId, layoutId)?.ContentVersion ?? _openingVersion) + _legacyBattleVersion);

        internal static string EncounterVersion(RescueEncounterDefinition encounter, OutingTimePolicy policy)
        {
            var outcome = encounter.RescuedDisposition;
            var parts = new List<string>
            {
                encounter.AnimalDefinitionId, encounter.EncounterId, encounter.RescueProfile,
                outcome.Emotion.ToString(), outcome.Stress.ToString(), outcome.Behavior.ToString()
            };
            foreach (OutingReturnProgress stage in Enum.GetValues(typeof(OutingReturnProgress)))
            {
                policy.TryGetDuration(stage, out long minutes, out _);
                parts.Add(minutes.ToString(CultureInfo.InvariantCulture));
            }
            return Hash(string.Join("\n", parts));
        }

        internal ApproachCatalog Opening(string pointId, string layoutId) =>
            Layout(pointId, layoutId)?.Opening ?? _fallback;

        internal SiegeCatalog Battle(string pointId, string layoutId)
        {
            string key = pointId + ":" + layoutId;
            if (!_battles.TryGetValue(key, out var catalog))
            {
                var layout = Layout(pointId, layoutId);
                catalog = layout == null ? _content.CreateSiegeCatalog() : _content.CreateFieldSiegeCatalog(layout);
                _battles.Add(key, catalog);
            }
            return catalog;
        }
    }
}
