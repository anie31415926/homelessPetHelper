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
                Brace = definition.Ability == "brace" || definition.Ability == "braceAura" ? 1 : 0,
                AbilityCharges = definition.Ability == "braceAura" ? 1 : definition.AbilityCharges,
                Reroutes = definition.Ability == "reroute" ? 1 : 0,
            };
            s.Troops.Add(unit);
            return unit;
        }

        internal static void Merge(SiegeState s, SiegeCatalog c, int[] ids, int anchorId)
        {
            var anchor = s.Troops.First(x => x.Id == anchorId);
            var definition = c.Unit(anchor.DefinitionId);
            var materials = s.Troops.Where(x => ids.Contains(x.Id)).ToArray();
            double fraction = materials.Sum(x => x.Hp) / (definition.Hp * materials.Length);
            if (s.EncounterId != null)
                foreach (var material in s.Troops.Where(x => ids.Contains(x.Id)))
                    s.EmitEffect(kind: "mergePart",
                        from: Position(c, material),
                        to: Position(c, anchor),
                        visual: definition.Icon,
                        startMs: s.ElapsedMs,
                        untilMs: s.ElapsedMs + 450);
            s.Troops.RemoveAll(x => ids.Contains(x.Id));
            var merged = Spawn(s, c.Unit(definition.UpgradeId), anchor.LaneId);
            merged.Distance = anchor.Distance;
            merged.FreePosition = anchor.FreePosition;
            merged.Hp = Math.Max(0.01, c.Unit(merged.DefinitionId).Hp * fraction);
            SiegeAnimalCasting.TransferMerge(s, materials, merged);
            SiegeHold.TransferMerge(s, materials, merged);
            s.EmitEffect(kind: "merge",
                from: Position(c, merged),
                startMs: s.ElapsedMs,
                visual: c.Unit(merged.DefinitionId).Icon,
                untilMs: s.ElapsedMs + 650);
            s.Stats.Merges++;
            Record(s, c, "event.merge");
        }

        internal static int MergeCount(SiegeUnitDefinition definition) =>
            string.IsNullOrEmpty(definition?.UpgradeId) ? 0 : definition.MergeCount == 0 ? 3 : definition.MergeCount;

        internal static bool NaturalMerge(SiegeState s, SiegeCatalog c)
        {
            if (s.ManualMergeOnly) return false;
            for (int anchorIndex = 0; anchorIndex < s.Troops.Count; anchorIndex++)
            {
                var anchor = s.Troops[anchorIndex];
                if (!CanNaturallyMerge(anchor, c)) continue;
                var origin = Position(c, anchor);
                int count = MergeCount(c.Unit(anchor.DefinitionId));
                for (int firstIndex = anchorIndex + 1; firstIndex < s.Troops.Count; firstIndex++)
                {
                    var first = s.Troops[firstIndex];
                    if (!MergeNeighbour(anchor, first, origin, c)) continue;
                    if (count == 2)
                    {
                        Merge(s, c, new[] { anchor.Id, first.Id }, anchor.Id);
                        return true;
                    }
                    var firstPoint = Position(c, first);
                    for (int secondIndex = firstIndex + 1; secondIndex < s.Troops.Count; secondIndex++)
                    {
                        var second = s.Troops[secondIndex];
                        if (!MergeNeighbour(anchor, second, origin, c) ||
                            firstPoint.Distance(Position(c, second)) > c.Rules.MergeRadius) continue;
                        Merge(s, c, new[] { anchor.Id, first.Id, second.Id }, anchor.Id);
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool CanNaturallyMerge(SiegeState.Unit unit, SiegeCatalog c) =>
            !string.IsNullOrEmpty(c.Unit(unit.DefinitionId).UpgradeId) && unit.TransitRemainingMs == 0 &&
            !unit.IsCarried && unit.Distance >= c.Rules.NaturalMergeMinDistance;

        private static bool MergeNeighbour(SiegeState.Unit anchor, SiegeState.Unit unit, SiegePoint origin,
            SiegeCatalog c) => unit.DefinitionId == anchor.DefinitionId && unit.LaneId == anchor.LaneId &&
            CanNaturallyMerge(unit, c) && origin.Distance(Position(c, unit)) <= c.Rules.MergeRadius;

        internal static void Hit(SiegeState.Unit unit, double damage, double push = 0, int elapsedMs = 0)
        {
            if (unit.IsCarried) return;
            if (unit.Shield > 0)
                unit.Shield--;
            else
                unit.Hp -= damage;
            if (unit.HeldUntilMs > elapsedMs)
                return;
            if (push > 0 && unit.Brace > 0)
            {
                unit.Brace--;
                unit.BraceReadyMs = elapsedMs + unit.BraceRearmMs;
            }
            else if (push > 0 && unit.TransitRemainingMs == 0)
            {
                unit.Distance = Math.Max(0, unit.Distance - push);
                unit.FreePosition = null;
                unit.BurstHitsLeft = 0;
                unit.NextAttackMs = Math.Max(unit.NextAttackMs, unit.BurstCooldownMs);
            }
        }

        internal static void Reroute(SiegeState.Unit unit, SiegeCatalog c, string laneId)
        {
            var from = Position(c, unit);
            var lane = c.Lane(laneId);
            unit.Distance = Math.Min(lane.Length * 0.97, lane.Project(from));
            unit.LaneId = laneId;
            unit.FreePosition = null;
            unit.TransitFrom = from;
            unit.TransitTo = lane.PointAt(unit.Distance);
            unit.TransitRemainingMs = c.Rules.RerouteDurationMs;
            unit.Reroutes--;
            unit.BurstHitsLeft = 0;
            unit.NextAttackMs = Math.Max(unit.NextAttackMs, unit.BurstCooldownMs);
        }

        internal static SiegePoint Position(SiegeCatalog c, SiegeState.Unit unit) => SiegeGeometry.Position(c, unit);

        internal static void Record(SiegeState s, SiegeCatalog c, string key)
        {
            s.Events.Add(key);
            if (s.Events.Count > c.Rules.MaxLog)
                s.Events.RemoveAt(0);
        }

        internal static void PlayCard(SiegeState state, SiegeState.Card card)
        {
            state.Cards.Remove(card);
            if (state.RotatingSupply) state.Bag.Add(card.DefinitionId);
        }

        internal static void Draw(SiegeState s, SiegeCatalog c, int limit)
        {
            for (int added = 0; added < limit && s.Cards.Count < c.Rules.HandSize; added++)
            {
                if (s.Bag.Count == 0 && s.RotatingSupply)
                {
                    foreach (string unitId in c.Content.Encounter(s.EncounterId).UnitIds) s.Bag.Add(unitId);
                }
                if (s.Bag.Count == 0)
                {
                    var encounter = s.EncounterId == null ? null : c.Content.Encounter(s.EncounterId);
                    var supply = encounter == null ? c.Units.Where(x => x.Bag)
                        : encounter.UnitIds.Select(id => c.Unit(id));
                    foreach (var unit in supply)
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
            s.NextCardDefinitionId = s.RotatingSupply && s.Bag.Count > 0 ? s.Bag[0] : null;
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
