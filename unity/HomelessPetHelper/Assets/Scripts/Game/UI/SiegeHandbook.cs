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
            public readonly int Cost;
            public readonly string[] Attributes;
            public Entry(string text, string icon, int cost = 0, string[] attributes = null)
            { Text = text; Icon = icon; Cost = cost; Attributes = attributes; }
        }

        public static IReadOnlyList<Entry> Describe(SiegeCatalog catalog, string encounterId, SiegeText names,
            OptionPreviewText text, bool defense)
        {
            var encounter = catalog.Content.Encounter(encounterId);
            if (encounter == null) return System.Array.Empty<Entry>();
            if (defense) return DescribeDefense(catalog, encounter, names, text);

            var shown = new HashSet<string>();
            var entries = new List<Entry>();
            foreach (string unitId in encounter.UnitIds)
            {
                var unit = catalog.Unit(unitId);
                while (unit != null && shown.Add(unit.Id))
                {
                    var upgrade = catalog.Unit(unit.UpgradeId);
                    string copy = text.TryGet("handbook.copy." + unit.Id, out var written) ? written : string.Empty;
                    string ability = unit.Ability == "none" ? string.Empty : text.Get("handbook.ability",
                        text.TryGet("handbook.power." + unit.Id, out var power) ? power : names.Get(unit.DetailKey));
                    string merge = upgrade == null ? string.Empty : text.Get("handbook.merge",
                        catalog.MergeMaterialCount(unit.Id), names.Get(unit.NameKey), names.Get(upgrade.NameKey));
                    entries.Add(new Entry(Compose(names.Get(unit.NameKey), copy, ability, merge), unit.Icon,
                        unit.Cost, Attributes(text, unit.Damage, unit.AttackMs, unit.Reach, unit.Hp)));
                    unit = upgrade;
                }
            }
            return entries;
        }

        private static IReadOnlyList<Entry> DescribeDefense(SiegeCatalog catalog, SiegeEncounterDefinition encounter,
            SiegeText names, OptionPreviewText text)
        {
            var entries = encounter.InitialTowerIds.Concat(encounter.BuildTowerIds).Distinct().Select(id =>
            {
                var tower = catalog.Content.Tower(id);
                return new Entry(Compose(names.Get(tower.NameKey), text.Get("handbook.copy." + id),
                    text.Get("handbook.ability", text.Get("handbook.attack." + tower.Attack))), tower.Visual,
                    attributes: Attributes(
                        text, tower.Damage, tower.PeriodMs + tower.WarningMs, tower.Range, tower.Hp));
            }).ToList();
            string hold = encounter.HoldDurationMs > 0
                ? text.Get("handbook.hold", names.Get(encounter.HoldNameKey)) : "";
            string carry = encounter.CarryDurationMs > 0
                ? text.Get("handbook.carry", names.Get(encounter.CarryNameKey)) : "";
            entries.Add(new Entry(Compose(text.Get("handbook.animalTitle"), text.Get("handbook.animal",
                names.Get(encounter.GuardNameKey), names.Get(encounter.SweepNameKey)), hold, carry), "paw"));
            return entries;
        }

        private static string Compose(string title, params string[] sections) =>
            string.Join("\n", new[] { "<b>" + title + "</b>" }.Concat(sections.Where(line => line.Length > 0)));

        private static string[] Attributes(
            OptionPreviewText text, double damage, int interval, double range, double hp) =>
            new[]
            {
                damage == 0 ? text.Get("handbook.stat.damage", text.Get("handbook.rank.damage.zero")) :
                    Rank(text, "damage", damage, 1, 2, 3, 4),
                Rank(text, "cadence", -interval, -1600, -1100, -950, -850),
                Rank(text, "range", range, 28, 30, 40, 65),
                Rank(text, "durability", hp, 5, 7, 11, 16)
            };

        private static string Rank(OptionPreviewText text, string attribute, double value,
            double lower, double low, double high, double upper)
        {
            int grade = value < lower ? 1 : value < low ? 2 : value < high ? 3 : value < upper ? 4 : 5;
            return text.Get("handbook.stat." + attribute, text.Get($"handbook.rank.{attribute}.{grade}"));
        }
    }
}
