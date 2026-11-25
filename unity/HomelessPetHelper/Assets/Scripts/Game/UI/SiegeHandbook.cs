using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;

namespace HomelessPetHelper.Game.UI
{
    internal static class SiegeHandbook
    {
        internal readonly struct Entry
        {
            public readonly string Text, Icon;
            public readonly bool UsesSupply;
            public Entry(string text, string icon = null, bool usesSupply = false)
            { Text = text; Icon = icon; UsesSupply = usesSupply; }
        }

        public static IReadOnlyList<Entry> Describe(SiegeCatalog catalog, string encounterId, SiegeText names,
            OptionPreviewText text, bool defense)
        {
            if (defense)
            {
                var towers = catalog.Content.Towers.Select(tower => new Entry(text.Get("handbook.tower",
                    names.Get(tower.NameKey), text.Get("handbook.attack." + tower.Attack)), tower.Visual));
                var encounter = catalog.Content.Encounter(encounterId);
                if (encounter == null) return towers.ToArray();
                string special = encounter.HoldDurationMs > 0 ? text.Get("handbook.hold", names.Get(encounter.HoldNameKey)) :
                    encounter.CarryDurationMs > 0 ? text.Get("handbook.carry", names.Get(encounter.CarryNameKey)) : string.Empty;
                return towers.Append(new Entry(text.Get("handbook.animal", names.Get(encounter.GuardNameKey),
                    names.Get(encounter.SweepNameKey), special))).ToArray();
            }
            var units = catalog.Units;
            return units.Select(unit =>
            {
                string attributes = text.Get("handbook.attributes",
                    Rank(units.Select(item => item.Speed), unit.Speed, text),
                    Rank(units.Select(item => item.Hp), unit.Hp, text),
                    Rank(units.Select(item => item.Damage), unit.Damage, text),
                    Rank(units.Select(item => item.Reach), unit.Reach, text));
                string merge = string.IsNullOrEmpty(unit.UpgradeId) ? string.Empty : "\n" +
                    text.Get("handbook.merge", names.Get(catalog.Unit(unit.UpgradeId).NameKey));
                string detail = names.Get(unit.DetailKey);
                bool custom = text.TryGet("handbook.copy." + unit.Id, out var copy);
                string ability = custom && unit.Ability != "none" ? "\n" + text.Get("handbook.ability", detail) : string.Empty;
                return new Entry(text.Get("handbook.unit", names.Get(unit.NameKey), attributes, custom ? copy : detail) +
                    ability + merge, unit.Icon, unit.Cost > 0);
            }).ToArray();
        }

        private static string Rank(IEnumerable<double> values, double value,
            OptionPreviewText text)
        {
            double low = values.Min(), high = values.Max();
            double fraction = high == low ? .5 : (value - low) / (high - low);
            return text.Get(fraction < 1.0 / 3 ? "handbook.rank.low" : fraction > 2.0 / 3
                ? "handbook.rank.high" : "handbook.rank.medium");
        }
    }
}
