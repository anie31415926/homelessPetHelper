using System;
using System.Linq;
using HomelessPetHelper.Modules.Siege;
using UnityEngine;

namespace HomelessPetHelper.Game.Progress
{
    internal enum StationPurchase { Beds, Cleaning, Clip }

    [Serializable]
    internal sealed class StationEconomyRules
    {
        public int initialCapacity, rescueAid, assessmentAid, adoptionAid, extraBeds;
        public int bedsPrice, cleaningPrice, clipPrice;
        public string clipUnit;
        private SiegeCatalogData.UnitDto _clip;

        [Serializable] private sealed class LoadoutSource { public SiegeCatalogData catalog; }

        internal static StationEconomyRules Load(TextAsset rules, TextAsset loadout)
        {
            if (rules == null || loadout == null) throw new ArgumentException("Missing station economy content.");
            var result = JsonUtility.FromJson<StationEconomyRules>(rules.text);
            if (result == null || string.IsNullOrEmpty(result.clipUnit) || result.initialCapacity <= 0 ||
                result.extraBeds <= 0 || result.rescueAid <= 0 ||
                result.assessmentAid <= 0 || result.adoptionAid <= 0 || result.bedsPrice <= 0 ||
                result.cleaningPrice <= 0 || result.clipPrice <= 0)
                throw new ArgumentException("Invalid station economy values.");
            result._clip = JsonUtility.FromJson<LoadoutSource>(loadout.text)?.catalog?.contentUnits
                ?.SingleOrDefault(unit => unit.id == result.clipUnit);
            if (result._clip == null) throw new ArgumentException("Invalid rescue equipment binding.");
            return result;
        }

        internal int Price(StationPurchase item) => item switch
        {
            StationPurchase.Beds => bedsPrice,
            StationPurchase.Cleaning => cleaningPrice,
            StationPurchase.Clip => clipPrice,
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };

        internal void ApplyClip(SiegeCatalogData data)
        {
            data.contentUnits = data.contentUnits.Append(_clip).ToArray();
            foreach (var encounter in data.encounters)
                encounter.unitIds[encounter.unitIds.Length - 1] = _clip.id;
        }
    }
}
