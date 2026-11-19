using System;
using System.Linq;

namespace HomelessPetHelper.Modules.Siege
{
    internal static class SiegeEntities
    {
        internal static SiegeState.Unit Spawn(SiegeState s, SiegeUnitDefinition definition, string lane)
        {
            var unit = new SiegeState.Unit
            {
                Id = ++s.Serial,
                DefinitionId = definition.Id,
                LaneId = lane,
                Hp = definition.Hp,
                NextAttackMs = s.ElapsedMs,
                NextAuraMs = s.ElapsedMs,
                Brace = definition.Ability == "brace" ? 1 : 0,
                Reroutes = definition.Ability == "reroute" ? 1 : 0,
            };
            s.Troops.Add(unit);
            return unit;
        }

        internal static void Merge(SiegeState s, SiegeCatalog c, int[] ids, int anchorId)
        {
            var anchor = s.Troops.First(x => x.Id == anchorId);
            var definition = c.Unit(anchor.DefinitionId);
            double fraction = s.Troops.Where(x => ids.Contains(x.Id)).Sum(x => x.Hp) / (definition.Hp * 3);
            s.Troops.RemoveAll(x => ids.Contains(x.Id));
            var merged = Spawn(s, c.Unit(definition.UpgradeId), anchor.LaneId);
            merged.Distance = anchor.Distance;
            merged.Hp = Math.Max(0.01, c.Unit(merged.DefinitionId).Hp * fraction);
            s.VisualEffects.Add(
                new SiegeState.Effect
                {
                    Kind = "merge",
                    From = Position(c, merged),
                    UntilMs = s.ElapsedMs + 650,
                }
            );
            s.Stats.Merges++;
            Record(s, c, "event.merge");
        }

        internal static bool NaturalMerge(SiegeState s, SiegeCatalog c)
        {
            foreach (var anchor in s.Troops)
            {
                if (
                    string.IsNullOrEmpty(c.Unit(anchor.DefinitionId).UpgradeId)
                    || anchor.TransitRemainingMs > 0
                    || anchor.Distance < c.Rules.NaturalMergeMinDistance
                )
                    continue;
                var candidates = s
                    .Troops.Where(x =>
                        x.Id != anchor.Id
                        && x.DefinitionId == anchor.DefinitionId
                        && x.LaneId == anchor.LaneId
                        && x.TransitRemainingMs == 0
                        && x.Distance >= c.Rules.NaturalMergeMinDistance
                        && Position(c, x).Distance(Position(c, anchor)) <= c.Rules.MergeRadius
                    )
                    .ToArray();
                for (int i = 0; i < candidates.Length; i++)
                {
                    for (int j = i + 1; j < candidates.Length; j++)
                    {
                        if (Position(c, candidates[i]).Distance(Position(c, candidates[j])) > c.Rules.MergeRadius)
                            continue;
                        Merge(s, c, new[] { anchor.Id, candidates[i].Id, candidates[j].Id }, anchor.Id);
                        return true;
                    }
                }
            }
            return false;
        }

        internal static void Hit(SiegeState.Unit unit, double damage, double push = 0)
        {
            if (unit.Shield > 0)
                unit.Shield--;
            else
                unit.Hp -= damage;
            if (push > 0 && unit.Brace > 0)
                unit.Brace--;
            else if (push > 0 && unit.TransitRemainingMs == 0)
                unit.Distance = Math.Max(0, unit.Distance - push);
        }

        internal static void Reroute(SiegeState.Unit unit, SiegeCatalog c, string laneId)
        {
            var from = Position(c, unit);
            var lane = c.Lane(laneId);
            unit.Distance = Math.Min(lane.Length * 0.97, lane.Project(from));
            unit.LaneId = laneId;
            unit.TransitFrom = from;
            unit.TransitTo = lane.PointAt(unit.Distance);
            unit.TransitRemainingMs = c.Rules.RerouteDurationMs;
            unit.Reroutes--;
        }

        internal static SiegePoint Position(SiegeCatalog c, SiegeState.Unit unit) => SiegeGeometry.Position(c, unit);

        internal static void Record(SiegeState s, SiegeCatalog c, string key)
        {
            s.Events.Add(key);
            if (s.Events.Count > c.Rules.MaxLog)
                s.Events.RemoveAt(0);
        }

        internal static void Draw(SiegeState s, SiegeCatalog c, int limit)
        {
            for (int added = 0; added < limit && s.Cards.Count < c.Rules.HandSize; added++)
            {
                if (s.Bag.Count == 0)
                {
                    foreach (var unit in c.Units.Where(x => x.Bag))
                        for (int i = 0; i < c.Rules.BagCopies; i++)
                            s.Bag.Add(unit.Id);
                    for (int i = s.Bag.Count - 1; i > 0; i--)
                    {
                        int other = (int)(Roll(s) * (i + 1));
                        string value = s.Bag[i];
                        s.Bag[i] = s.Bag[other];
                        s.Bag[other] = value;
                    }
                }
                string id = s.Bag[0];
                s.Bag.RemoveAt(0);
                s.Cards.Add(new SiegeState.Card { Id = ++s.Serial, DefinitionId = id });
            }
        }

        private static double Roll(SiegeState s)
        {
            unchecked
            {
                s.Rng += 0x6D2B79F5;
                uint value = s.Rng;
                value = (value ^ (value >> 15)) * (value | 1);
                value ^= value + (value ^ (value >> 7)) * (value | 61);
                return (value ^ (value >> 14)) / 4294967296.0;
            }
        }
    }
}
