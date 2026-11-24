using System;
using System.Linq;
using System.Collections.Generic;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    public sealed class SiegeSession
    {
        private SiegeState _state;
        private readonly List<SiegeReplay.Step> _history = new List<SiegeReplay.Step>();
        private bool _restoring;
        public int Revision { get; private set; }
        public event Action DecisionCommitted;
        public event Action PhaseChanged;
        public SiegeCatalog Catalog { get; }
        public SiegeState Snapshot => _state.Clone();
        public void CopyPresentationTo(SiegeState target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _state.CopyTo(target);
        }
        public SiegeState.BattlePhase Phase => _state.Phase;
        public bool Paused => _state.Paused;
        public int ElapsedMs => _state.ElapsedMs;
        public bool Realtime => _state.Realtime;
        public void EnableRealtime()
        {
            if (Realtime) return;
            SiegeRealtime.Enable(_state, Catalog);
            if (!_restoring) _history.Add(new SiegeReplay.Step(0, false, false, realtime: true));
            Revision++;
        }
        public bool PauseAtDecision { get; private set; } = true;
        private bool _decisionHold;
        public bool IsAdvancing => !_state.Paused && (Realtime
            ? !_decisionHold && _state.Phase == BattlePhase.Running
            : _state.Phase == BattlePhase.Running || _state.Phase == BattlePhase.Decision &&
                !PauseAtDecision && !_decisionHold);

        public void SetPauseAtDecision(bool pause) => PauseAtDecision = Realtime ? false : pause;
        public void SetDecisionHold(bool held) => _decisionHold = held;

        public SiegeReplay CaptureReplay() => new SiegeReplay(_history.Select(step => step.Copy()),
            _state.AccumulatorMs);

        /// <summary>Replays into a detached session, then commits once. Invalid input changes nothing.</summary>
        public void RestoreReplay(SiegeReplay replay)
        {
            if (_history.Count != 0 || Revision != 0) throw new InvalidOperationException("Battle is already live.");
            int maxTicks = Catalog.Rules.DurationMs / Catalog.Rules.StepMs + 1;
            if (replay == null || replay.Steps.Count > 8 * maxTicks + 8 ||
                !SiegeCatalog.Finite(replay.AccumulatorMs) || replay.AccumulatorMs < 0 ||
                replay.AccumulatorMs >= Catalog.Rules.StepMs)
                throw new ArgumentException("Invalid battle replay header.");
            var staged = new SiegeSession(Catalog, _state.Seed, _state.Profile, _state.SessionId,
                _state.EncounterId) { _restoring = true };
            int ticks = 0;
            foreach (var step in replay.Steps)
            {
                if (step == null || step.Ticks < 0 || step.Ticks > maxTicks - ticks ||
                    (step.Order == null ? step.Ticks == 0 && !step.Realtime : step.Ticks != 0))
                    throw new ArgumentException("Invalid battle replay step.");
                if (step.Realtime) staged.EnableRealtime();
                else if (staged.Realtime) throw new ArgumentException("Replay cannot revert simulation mode.");
                staged.SetPauseAtDecision(step.PauseAtDecision);
                staged.SetDecisionHold(step.DecisionHeld);
                var order = step.Order;
                if (order != null && !staged.TryOrder(order, true, out _, out _))
                    throw new ArgumentException("Rejected battle replay order.");
                for (int i = 0; i < step.Ticks; i++)
                {
                    if (!staged.IsAdvancing) throw new ArgumentException("Replay advances a stopped battle.");
                    staged.Tick();
                }
                ticks += step.Ticks;
            }
            staged._state.AccumulatorMs = replay.AccumulatorMs;
            _state = staged._state;
            _history.AddRange(replay.Steps.Select(step => step.Copy()));
            Revision++;
        }

        private void RecordTick()
        {
            Revision++;
            if (_restoring) return;
            var last = _history.LastOrDefault();
            if (last != null && last.Ticks > 0 && last.PauseAtDecision == PauseAtDecision &&
                last.DecisionHeld == _decisionHold && last.Realtime == Realtime) last.Ticks++;
            else _history.Add(new SiegeReplay.Step(1, PauseAtDecision, _decisionHold, realtime: Realtime));
        }

        public SiegeSession(SiegeCatalog catalog, uint seed, string profile, string sessionId,
            string encounterId = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            var preset = catalog.Profile(profile);
            if (preset == null || string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Invalid siege identity.");
            var encounter = encounterId == null ? null : catalog.Content.Encounter(encounterId);
            if (encounterId != null && encounter == null)
                throw new ArgumentException("Unknown siege encounter: " + encounterId);
            _state = new SiegeState
            {
                SessionId = sessionId,
                Seed = seed,
                Rng = seed,
                Profile = profile,
                EncounterId = encounterId,
                RotatingSupply = encounter?.RotateSupply == true,
                ManualMergeOnly = encounter?.ManualMergeOnly == true,
                Phase = BattlePhase.Decision,
                Turn = 1,
                Energy = catalog.Rules.OpeningEnergy,
            };
            var a = _state.AnimalState;
            a.Vigilance = preset.Vigilance;
            a.Stamina = preset.Stamina;
            a.BuildsLeft = catalog.Rules.AnimalBuilds;
            a.RepairsLeft = catalog.Rules.AnimalRepairs;
            string openingLane = catalog.Lanes.FirstOrDefault(x => x.Id == "mid" && x.Open)?.Id
                ?? catalog.Lanes.First(x => x.Open).Id;
            SiegeAnimalCasting.Plan(_state, catalog, IntentKind.Guard, new SkillTarget(openingLane));
            int towerIndex = 0;
            foreach (var pad in catalog.Pads.Where(x => x.Initial && catalog.Lane(x.LaneId).Open))
            {
                string typeId = encounter == null ? null
                    : encounter.InitialTowerIds[towerIndex++ % encounter.InitialTowerIds.Count];
                _state.Defenses.Add(
                    new Tower
                    {
                        Id = pad.Id,
                        DefinitionId = typeId,
                        LaneId = pad.LaneId,
                        Distance = catalog.Lane(pad.LaneId).Length * pad.At,
                        Hp = typeId == null ? catalog.Tower.Hp : catalog.Content.Tower(typeId).Hp,
                    }
                );
            }
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
            if (!IsAdvancing) return;
            var previousPhase = _state.Phase;
            _state.AccumulatorMs += elapsedMs;
            while (_state.AccumulatorMs >= Catalog.Rules.StepMs && IsAdvancing)
            {
                _state.AccumulatorMs -= Catalog.Rules.StepMs;
                Tick();
            }
            if (!IsAdvancing) _state.AccumulatorMs = 0;
            if (previousPhase != _state.Phase) PhaseChanged?.Invoke();
        }

        private void Tick()
        {
            RecordTick();
            var s = _state;
            var r = Catalog.Rules;
            var a = s.AnimalState;
            s.ElapsedMs += r.StepMs;
            if (Realtime) SiegeRealtime.Advance(s, Catalog);
            else s.PlayerChargeMs = Math.Min(r.PlayerPeriodMs, s.PlayerChargeMs + r.StepMs);
            a.Charge = Math.Min(1, a.Charge + (double)r.StepMs / Catalog.Conditions(a.Vigilance, a.Stamina).PeriodMs);
            SiegeCombat.Advance(s, Catalog, Realtime || !PauseAtDecision && !_decisionHold);
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
            if (!Realtime && s.Phase == BattlePhase.Running && s.PlayerChargeMs >= r.PlayerPeriodMs)
            {
                s.Turn++;
                s.Phase = BattlePhase.Decision;
                s.Energy = Math.Min(r.EnergyCap, s.Energy + r.TurnEnergy);
                SiegeEntities.Draw(s, Catalog, r.TurnDraw);
                SiegeEntities.Record(s, Catalog, "event.turn");
                if (PauseAtDecision || _decisionHold) return;
            }
            SiegeAnimalCasting.TryRelease(s, Catalog, Realtime || !PauseAtDecision && !_decisionHold);
        }

        /// <summary>Preview and commit share validation. Rejection leaves state, identity and RNG unchanged.</summary>
        public bool TryOrder(SiegeOrder order, bool commit, out string reason, out int cost)
        {
            if (Realtime && order != null && order.DeployCardId.HasValue)
                return TryDeploy(order, commit, out reason, out cost);
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
                if (!Realtime) { next.PlayerChargeMs = 0; next.AccumulatorMs = 0; }
                next.Energy -= cost;
                _state = next;
                Revision++;
                if (!_restoring) _history.Add(new SiegeReplay.Step(0, PauseAtDecision, _decisionHold, order, Realtime));
                DecisionCommitted?.Invoke();
            }
            return true;
        }

        private bool TryDeploy(SiegeOrder order, bool commit, out string reason, out int cost)
        {
            cost = 0;
            reason = ValidateHeader(order);
            if (reason != null) return false;
            if (order.MergeIds != null || order.RerouteUnitId.HasValue || !order.DeployPoint.HasValue)
            { reason = "reason.order"; return false; }
            Card card = null;
            foreach (var candidate in _state.Cards)
                if (candidate.Id == order.DeployCardId.Value) { card = candidate; break; }
            if (card == null) { reason = "reason.card"; return false; }
            var point = order.DeployPoint.Value;
            if (!SiegeDeployment.CanDeploy(Catalog, point)) { reason = "field.dropOutside"; return false; }
            var lane = SiegeDeployment.NearestLane(Catalog, point);
            if (lane == null) { reason = "reason.lane"; return false; }
            if (_state.Troops.Count >= Catalog.Rules.MaxTroops) { reason = "field.unitLimit"; return false; }
            var definition = Catalog.Unit(card.DefinitionId);
            if (definition.Cost > _state.Energy) { reason = "reason.energy"; return false; }
            cost = definition.Cost;
            if (!commit) return true;
            var unit = SiegeEntities.Spawn(_state, definition, lane.Id);
            unit.Distance = lane.Project(point);
            unit.FreePosition = point;
            _state.EmitEffect(EffectKind.Deploy, point, _state.ElapsedMs + 320,
                startMs: _state.ElapsedMs, visual: definition.Icon, unitId: unit.Id);
            SiegeEntities.PlayCard(_state, card);
            _state.Energy -= cost;
            _state.Stats.Deployed++;
            SiegeEntities.Draw(_state, Catalog, 1);
            SiegeEntities.Record(_state, Catalog, "event.deploy");
            Revision++;
            if (!_restoring) _history.Add(new SiegeReplay.Step(0, false, _decisionHold, order, true));
            DecisionCommitted?.Invoke();
            return true;
        }

        private string ValidateHeader(SiegeOrder o)
        {
            if ((!Realtime && _state.Phase != BattlePhase.Decision || Realtime &&
                _state.Phase != BattlePhase.Running) || _state.Paused)
                return "reason.phase";
            if (o == null || o.SessionId != _state.SessionId || o.Turn != _state.Turn)
                return "reason.stale";
            if (((Realtime || _state.RotatingSupply) && o.DiscardCardId.HasValue) ||
                (Realtime && !o.DeployCardId.HasValue && o.MergeIds == null && !o.RerouteUnitId.HasValue))
                return "reason.order";
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
                SiegeEntities.PlayCard(s, card);
                spawned = SiegeEntities.Spawn(s, definition, o.DeployLaneId).Id;
                s.Stats.Deployed++;
                SiegeEntities.Record(s, Catalog, "event.deploy");
            }
            if (o.MergeIds != null)
            {
                int[] ids = o.MergeIds.Select(x => x == SiegeOrder.NewUnit ? spawned : x).ToArray();
                int anchor = o.AnchorId == SiegeOrder.NewUnit ? spawned : o.AnchorId;
                if (ids.Length < 2 || ids.Length > 3 || ids.Distinct().Count() != ids.Length || !ids.Contains(anchor))
                    return "reason.merge";
                var units = ids.Select(x => s.Troops.FirstOrDefault(u => u.Id == x)).ToArray();
                if (units.Any(x => x == null || x.Hp <= 0 || x.TransitRemainingMs > 0))
                    return "reason.merge";
                if (units.Any(x => x.IsCarried))
                    return "reason.carried";
                if (
                    units.Any(x => x.DefinitionId != units[0].DefinitionId)
                    || SiegeEntities.MergeCount(Catalog.Unit(units[0].DefinitionId)) != ids.Length
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
                if (unit.HeldUntilMs > s.ElapsedMs)
                    return "reason.held";
                if (unit.IsCarried)
                    return "reason.carried";
                if (Catalog.Lane(o.RerouteLaneId)?.Open != true)
                    return "reason.lane";
                SiegeEntities.Reroute(unit, Catalog, o.RerouteLaneId);
                SiegeEntities.Record(s, Catalog, "event.reroute");
            }
            return cost > _state.Energy ? "reason.energy" : null;
        }
    }
}
