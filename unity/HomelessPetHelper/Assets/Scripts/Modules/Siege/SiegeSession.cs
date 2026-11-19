using System;
using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    public sealed class SiegeSession
    {
        private SiegeState _state;
        public SiegeCatalog Catalog { get; }
        public SiegeState Snapshot => _state.Clone();
        public SiegeState.BattlePhase Phase => _state.Phase;
        public bool Paused => _state.Paused;
        public int ElapsedMs => _state.ElapsedMs;

        public SiegeSession(SiegeCatalog catalog, uint seed, string profile, string sessionId)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            var preset = catalog.Profile(profile);
            if (preset == null || string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Invalid siege identity.");
            _state = new SiegeState
            {
                SessionId = sessionId,
                Seed = seed,
                Rng = seed,
                Profile = profile,
                Phase = BattlePhase.Decision,
                Turn = 1,
                Energy = catalog.Rules.OpeningEnergy,
            };
            var a = _state.AnimalState;
            a.Vigilance = preset.Vigilance;
            a.Stamina = preset.Stamina;
            a.BuildsLeft = catalog.Rules.AnimalBuilds;
            a.RepairsLeft = catalog.Rules.AnimalRepairs;
            a.IntentLane = catalog.Lanes.FirstOrDefault(x => x.Id == "mid" && x.Open)?.Id
                ?? catalog.Lanes.First(x => x.Open).Id;
            a.SeenTurn = 1;
            foreach (var pad in catalog.Pads.Where(x => x.Initial && catalog.Lane(x.LaneId).Open))
                _state.Defenses.Add(
                    new Tower
                    {
                        Id = pad.Id,
                        LaneId = pad.LaneId,
                        Distance = catalog.Lane(pad.LaneId).Length * pad.At,
                        Hp = catalog.Tower.Hp,
                    }
                );
            SiegeEntities.Draw(_state, catalog, catalog.Rules.HandSize);
            SiegeEntities.Record(_state, catalog, "event.start");
        }

        public SiegePoint Position(Unit unit) => SiegeEntities.Position(Catalog, unit);

        public void SetPaused(bool paused)
        {
            if (_state.Phase != BattlePhase.Decision && _state.Phase != BattlePhase.Running)
                return;
            _state.Paused = paused;
            _state.AccumulatorMs = 0;
        }

        public void Advance(double elapsedMs)
        {
            if (!SiegeCatalog.Finite(elapsedMs) || elapsedMs < 0 || elapsedMs > 1000)
                throw new ArgumentOutOfRangeException(nameof(elapsedMs));
            if (_state.Phase != BattlePhase.Running || _state.Paused)
                return;
            _state.AccumulatorMs += elapsedMs;
            while (_state.AccumulatorMs >= Catalog.Rules.StepMs && _state.Phase == BattlePhase.Running)
            {
                _state.AccumulatorMs -= Catalog.Rules.StepMs;
                Tick();
            }
            if (_state.Phase != BattlePhase.Running)
                _state.AccumulatorMs = 0;
        }

        private void Tick()
        {
            var s = _state;
            var r = Catalog.Rules;
            var a = s.AnimalState;
            s.ElapsedMs += r.StepMs;
            s.PlayerChargeMs = Math.Min(r.PlayerPeriodMs, s.PlayerChargeMs + r.StepMs);
            a.Charge = Math.Min(1, a.Charge + (double)r.StepMs / Catalog.Conditions(a.Vigilance, a.Stamina).PeriodMs);
            SiegeCombat.Advance(s, Catalog);
            if (a.Vigilance == "high" && s.Progress >= r.Target * r.RelaxAtProgress)
            {
                a.Vigilance = "watchful";
                SiegeEntities.Record(s, Catalog, "event.relax");
            }
            if (s.Progress >= r.Target)
            {
                s.Phase = BattlePhase.Won;
                SiegeEntities.Record(s, Catalog, "event.win");
                return;
            }
            if (s.ElapsedMs >= r.DurationMs)
            {
                s.Phase = BattlePhase.Timeout;
                SiegeEntities.Record(s, Catalog, "event.timeout");
                return;
            }
            // A simultaneous full gauge opens strategy first; the animal keeps its pending charge.
            if (s.PlayerChargeMs >= r.PlayerPeriodMs)
            {
                s.Turn++;
                s.Phase = BattlePhase.Decision;
                s.Energy = Math.Min(r.EnergyCap, s.Energy + r.TurnEnergy);
                SiegeEntities.Draw(s, Catalog, r.TurnDraw);
                SiegeEntities.Record(s, Catalog, "event.turn");
                return;
            }
            if (
                a.Charge >= 1
                && s.ElapsedMs >= a.RecoveryUntilMs
                && (a.Intent == IntentKind.Guard || s.ResolvedTurn >= a.SeenTurn)
            )
                SiegeAnimal.Act(s, Catalog);
        }

        /// <summary>Preview and commit share validation. Rejection leaves state, identity and RNG unchanged.</summary>
        public bool TryOrder(SiegeOrder order, bool commit, out string reason, out int cost)
        {
            cost = 0;
            reason = ValidateHeader(order);
            if (reason != null)
                return false;
            var next = _state.Clone();
            reason = ApplyPlan(next, order, out cost);
            if (reason != null)
            {
                cost = 0;
                return false;
            }
            if (commit)
            {
                next.ResolvedTurn = next.Turn;
                next.Phase = BattlePhase.Running;
                next.PlayerChargeMs = 0;
                next.AccumulatorMs = 0;
                next.Energy -= cost;
                _state = next;
            }
            return true;
        }

        private string ValidateHeader(SiegeOrder o)
        {
            if (_state.Phase != BattlePhase.Decision || _state.Paused)
                return "reason.phase";
            if (o == null || o.SessionId != _state.SessionId || o.Turn != _state.Turn)
                return "reason.stale";
            if (o.MergeIds != null && o.RerouteUnitId.HasValue)
                return "reason.order";
            if (o.DiscardCardId.HasValue && (o.DeployCardId.HasValue || o.MergeIds != null || o.RerouteUnitId.HasValue))
                return "reason.discard";
            if (
                (!o.DeployCardId.HasValue && o.DeployLaneId != null)
                || (!o.RerouteUnitId.HasValue && o.RerouteLaneId != null)
                || (o.MergeIds == null && o.AnchorId != 0)
            )
                return "reason.order";
            return null;
        }

        private string ApplyPlan(SiegeState s, SiegeOrder o, out int cost)
        {
            cost = 0;
            int spawned = 0;
            if (o.DiscardCardId.HasValue)
            {
                if (s.Cards.RemoveAll(x => x.Id == o.DiscardCardId.Value) != 1)
                    return "reason.card";
                SiegeEntities.Record(s, Catalog, "event.discard");
            }
            if (o.DeployCardId.HasValue)
            {
                var card = s.Cards.FirstOrDefault(x => x.Id == o.DeployCardId.Value);
                if (card == null)
                    return "reason.card";
                if (Catalog.Lane(o.DeployLaneId)?.Open != true)
                    return "reason.lane";
                var definition = Catalog.Unit(card.DefinitionId);
                cost += definition.Cost;
                s.Cards.Remove(card);
                spawned = SiegeEntities.Spawn(s, definition, o.DeployLaneId).Id;
                s.Stats.Deployed++;
                SiegeEntities.Record(s, Catalog, "event.deploy");
            }
            if (o.MergeIds != null)
            {
                int[] ids = o.MergeIds.Select(x => x == SiegeOrder.NewUnit ? spawned : x).ToArray();
                int anchor = o.AnchorId == SiegeOrder.NewUnit ? spawned : o.AnchorId;
                if (ids.Length != 3 || ids.Distinct().Count() != 3 || !ids.Contains(anchor))
                    return "reason.merge";
                var units = ids.Select(x => s.Troops.FirstOrDefault(u => u.Id == x)).ToArray();
                if (units.Any(x => x == null || x.Hp <= 0 || x.TransitRemainingMs > 0))
                    return "reason.merge";
                if (
                    units.Any(x => x.DefinitionId != units[0].DefinitionId)
                    || string.IsNullOrEmpty(Catalog.Unit(units[0].DefinitionId).UpgradeId)
                )
                    return "reason.merge";
                cost += Catalog.Rules.MergeCost;
                SiegeEntities.Merge(s, Catalog, ids, anchor);
            }
            if (o.RerouteUnitId.HasValue)
            {
                var unit = s.Troops.FirstOrDefault(x => x.Id == o.RerouteUnitId.Value);
                if (unit == null || unit.Reroutes < 1 || unit.TransitRemainingMs > 0 || unit.LaneId == o.RerouteLaneId)
                    return "reason.reroute";
                if (Catalog.Lane(o.RerouteLaneId)?.Open != true)
                    return "reason.lane";
                SiegeEntities.Reroute(unit, Catalog, o.RerouteLaneId);
                SiegeEntities.Record(s, Catalog, "event.reroute");
            }
            return cost > _state.Energy ? "reason.energy" : null;
        }
    }
}
