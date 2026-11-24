using System;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal static class SiegeRealtime
    {
        internal static void Enable(SiegeState s, SiegeCatalog c)
        {
            if (s.Realtime) return;
            s.Realtime = true;
            s.DefenderEnergy = c.Rules.DefenderOpeningEnergy;
            s.AnimalState.BuildsLeft = 0;
            if (s.Phase == BattlePhase.Decision) s.Phase = BattlePhase.Running;
            var occupied = new System.Collections.Generic.HashSet<int>();
            foreach (var tower in s.Defenses)
            {
                if (tower.Status == TowerStatus.Destroyed) continue;
                int cell = SiegeDeployment.Cell(c, SiegeGeometry.Position(c, tower));
                if (occupied.Contains(cell))
                    for (int candidate = 0; candidate < c.Rules.DefenseGridSize * c.Rules.DefenseGridSize; candidate++)
                        if (!occupied.Contains(candidate) && SiegeDeployment.Fraction(c,
                            SiegeDeployment.CellCenter(c, candidate)) > c.Rules.DeploymentFraction)
                        { cell = candidate; break; }
                occupied.Add(cell);
                tower.FreePosition = SiegeDeployment.CellCenter(c, cell);
            }
            if (s.AnimalState.Intent == IntentKind.Build)
                SiegeAnimalCasting.Plan(s, c, IntentKind.Guard, new SkillTarget(c.Lanes[0].Id));
            if (s.AnimalState.Cast?.Kind == IntentKind.Build) s.AnimalState.Cast = null;
            SiegeEntities.Draw(s, c, c.Rules.HandSize);
        }

        internal static void Advance(SiegeState s, SiegeCatalog c)
        {
            int period = c.Rules.ResourcePeriodMs;
            s.ResourceChargeMs += c.Rules.StepMs;
            s.DefenderChargeMs += c.Rules.StepMs;
            while (s.ResourceChargeMs >= period)
            {
                s.ResourceChargeMs -= period;
                s.Energy = Math.Min(c.Rules.EnergyCap, s.Energy + 1);
            }
            while (s.DefenderChargeMs >= c.Rules.DefenderResourcePeriodMs)
            {
                s.DefenderChargeMs -= c.Rules.DefenderResourcePeriodMs;
                s.DefenderEnergy = Math.Min(c.Rules.DefenderEnergyCap, s.DefenderEnergy + 1);
            }
            s.PlayerChargeMs = s.ResourceChargeMs;
            if (s.DefenderEnergy >= c.Rules.DefenderBuildCost) TryBuild(s, c);
        }

        private static void TryBuild(SiegeState s, SiegeCatalog c)
        {
            // Fixed grid bounds both work and tower count. Stable cell order breaks equal scores.
            int count = c.Rules.DefenseGridSize * c.Rules.DefenseGridSize;
            int best = -1;
            double bestScore = double.NegativeInfinity;
            for (int cell = 0; cell < count; cell++)
            {
                var point = SiegeDeployment.CellCenter(c, cell);
                double fraction = SiegeDeployment.Fraction(c, point);
                if (fraction <= c.Rules.DeploymentFraction || fraction >= .95) continue;
                bool occupied = false;
                foreach (var tower in s.Defenses)
                    if (tower.Status != TowerStatus.Destroyed &&
                        SiegeDeployment.Cell(c, SiegeGeometry.Position(c, tower)) == cell)
                    { occupied = true; break; }
                if (occupied) continue;
                double score = -point.Distance(c.Lanes[0].PointAt(c.Lanes[0].Length)) * .01;
                foreach (var unit in s.Troops)
                    if (unit.Hp > 0 && !unit.IsCarried)
                        score += Math.Max(0, 1 - SiegeGeometry.Position(c, unit).Distance(point) / c.Tower.Range);
                if (score <= bestScore) continue;
                best = cell; bestScore = score;
            }
            if (best < 0) return;
            var position = SiegeDeployment.CellCenter(c, best);
            var lane = SiegeDeployment.NearestLane(c, position);
            var encounter = c.Content.Encounter(s.EncounterId);
            string type = encounter == null ? null : encounter.BuildTowerIds[s.AnimalState.BuildsLeft %
                encounter.BuildTowerIds.Count];
            // Destroyed occupants are replaced; identifiers are never reused.
            s.Defenses.RemoveAll(tower => tower.Status == TowerStatus.Destroyed);
            s.Defenses.Add(new Tower
            {
                Id = "grid." + (++s.Serial), DefinitionId = type, LaneId = lane.Id,
                Distance = lane.Project(position), FreePosition = position,
                Hp = type == null ? c.Tower.Hp : c.Content.Tower(type).Hp,
                Status = TowerStatus.Building,
                ReadyAtMs = s.ElapsedMs + c.Rules.FastBuildDurationMs,
                NextShotMs = s.ElapsedMs + c.Rules.FastBuildDurationMs
            });
            s.AnimalState.BuildsLeft++;
            s.DefenderEnergy -= c.Rules.DefenderBuildCost;
            SiegeEntities.Record(s, c, "event.build");
        }
    }
}
