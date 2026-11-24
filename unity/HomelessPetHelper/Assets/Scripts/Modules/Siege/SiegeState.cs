using System.Collections.Generic;
using System.Linq;

namespace HomelessPetHelper.Modules.Siege
{
    // Public reads are detached snapshots; only the owning session mutates its live state.
    public sealed class SiegeState
    {
        public enum BattlePhase
        {
            Decision = 0,
            Running = 1,
            Won = 2,
            Timeout = 3,
        }

        public enum IntentKind
        {
            Guard = 0,
            Sweep = 1,
            Build = 2,
            Repair = 3,
            Hold = 4,
            Carry = 5,
        }

        public enum TowerStatus
        {
            Active = 0,
            Building = 1,
            Destroyed = 2,
        }

        public enum CastPhase
        {
            Windup = 0,
            Active = 1,
            Recovery = 2,
        }

        public readonly struct SkillTarget
        {
            public string LaneId { get; }
            public string PadId { get; }
            public int UnitId { get; }

            internal SkillTarget(string laneId, string padId = null, int unitId = 0)
            {
                LaneId = laneId;
                PadId = padId;
                UnitId = unitId;
            }
        }

        public sealed class SkillCast
        {
            public IntentKind Kind { get; internal set; }
            public CastPhase Phase { get; internal set; }
            public SkillTarget Target { get; internal set; }
            public SiegePoint Point { get; internal set; }
            public double Radius { get; internal set; }
            public int StartedMs { get; internal set; }
            public int ResolveAtMs { get; internal set; }
            public int UntilMs { get; internal set; }
            internal int RecoveryMs;

            internal void CopyTo(SkillCast target)
            {
                target.Kind = Kind;
                target.Phase = Phase;
                target.Target = Target;
                target.Point = Point;
                target.Radius = Radius;
                target.StartedMs = StartedMs;
                target.ResolveAtMs = ResolveAtMs;
                target.UntilMs = UntilMs;
            }

            internal SkillCast Clone() => (SkillCast)MemberwiseClone();
        }

        public bool Realtime { get; internal set; }
        public int ResourceChargeMs { get; internal set; }
        public int DefenderEnergy { get; internal set; }
        public int DefenderChargeMs { get; internal set; }
        public string SessionId { get; internal set; }
        public uint Seed { get; internal set; }
        public string Profile { get; internal set; }
        internal bool ManualMergeOnly { get; set; }
        public bool RotatingSupply { get; internal set; }
        public string NextCardDefinitionId { get; internal set; }
        public string EncounterId { get; internal set; }
        public BattlePhase Phase { get; internal set; }
        public bool Paused { get; internal set; }
        public int ElapsedMs { get; internal set; }
        public int PlayerChargeMs { get; internal set; }
        public int Turn { get; internal set; }
        public int ResolvedTurn { get; internal set; }
        public int Energy { get; internal set; }
        public double Progress { get; internal set; }
        public Animal AnimalState { get; internal set; } = new Animal();
        public Statistics Stats { get; internal set; } = new Statistics();
        private IReadOnlyList<Card> _handView;
        public IReadOnlyList<Card> Hand => _handView ?? (_handView = Cards.AsReadOnly());
        private IReadOnlyList<Unit> _unitsView;
        public IReadOnlyList<Unit> Units => _unitsView ?? (_unitsView = Troops.AsReadOnly());
        private IReadOnlyList<Tower> _towersView;
        public IReadOnlyList<Tower> Towers => _towersView ?? (_towersView = Defenses.AsReadOnly());
        private IReadOnlyList<Effect> _effectsView;
        public IReadOnlyList<Effect> Effects => _effectsView ?? (_effectsView = VisualEffects.AsReadOnly());
        private IReadOnlyList<SlowZone> _slowzonesView;
        public IReadOnlyList<SlowZone> SlowZones => _slowzonesView ?? (_slowzonesView = Zones.AsReadOnly());
        private IReadOnlyList<string> _logView;
        public IReadOnlyList<string> Log => _logView ?? (_logView = Events.AsReadOnly());
        internal uint Rng;
        internal int Serial;
        internal double AccumulatorMs;
        internal List<Card> Cards = new List<Card>();
        internal List<string> Bag = new List<string>();
        internal List<Unit> Troops = new List<Unit>();
        internal List<Tower> Defenses = new List<Tower>();
        internal List<Effect> VisualEffects = new List<Effect>();
        internal List<SlowZone> Zones = new List<SlowZone>();
        internal List<string> Events = new List<string>();

        private SiegeEffectPool _effectPool;
        private int _effectSequence;
        public int LatestEffectSequence => _effectSequence;
        private SiegeEffectPool EffectPool => _effectPool ?? (_effectPool = new SiegeEffectPool());

        internal void ExpireEffects() => EffectPool.Expire(VisualEffects, ElapsedMs);

        internal void EmitEffect(string kind, SiegePoint from, int untilMs, SiegePoint to = default,
            double radius = 0, int startMs = 0, string visual = null, int unitId = 0)
        {
            if (VisualEffects.Count >= SiegeEffectPool.Capacity)
            {
                var oldest = VisualEffects[0];
                VisualEffects.RemoveAt(0);
                EffectPool.Return(oldest);
            }
            var effect = EffectPool.Rent();
            effect.Kind = kind;
            effect.From = from;
            effect.To = to;
            effect.Radius = radius;
            effect.UntilMs = untilMs;
            effect.StartMs = startMs;
            effect.Visual = visual;
            effect.Sequence = ++_effectSequence;
            effect.UnitId = unitId;
            VisualEffects.Add(effect);
        }

        internal SiegeState Clone()
        {
            var copy = (SiegeState)MemberwiseClone();
            copy._effectPool = null;
            copy._handView = null; copy._unitsView = null; copy._towersView = null;
            copy._effectsView = null; copy._slowzonesView = null; copy._logView = null;
            copy.Cards = Cards.Select(x => x.Clone()).ToList();
            copy.Bag = new List<string>(Bag);
            copy.Troops = Troops.Select(x => x.Clone()).ToList();
            copy.Defenses = Defenses.Select(x => x.Clone()).ToList();
            copy.VisualEffects = VisualEffects.Select(x => x.Clone()).ToList();
            copy.Zones = Zones.Select(x => x.Clone()).ToList();
            copy.Events = new List<string>(Events);
            copy.AnimalState = AnimalState.Clone();
            copy.Stats = Stats.Clone();
            return copy;
        }

        internal void CopyTo(SiegeState target)
        {
            target.Realtime = Realtime; target.ResourceChargeMs = ResourceChargeMs;
            target.DefenderEnergy = DefenderEnergy; target.DefenderChargeMs = DefenderChargeMs;
            target.SessionId = SessionId; target.Seed = Seed; target.Profile = Profile;
            target.EncounterId = EncounterId; target.RotatingSupply = RotatingSupply;
            target.NextCardDefinitionId = NextCardDefinitionId;
            target.Phase = Phase; target.Paused = Paused; target.ElapsedMs = ElapsedMs;
            target.PlayerChargeMs = PlayerChargeMs; target.Turn = Turn; target.ResolvedTurn = ResolvedTurn;
            target.Energy = Energy; target.Progress = Progress;
            // Entity snapshots are reused. Nested animal state is small and cloned for isolation.
            AnimalState.CopyTo(target.AnimalState);
            Stats.CopyTo(target.Stats);
            CopyList(Cards, target.Cards, (source,
                dest) => { dest.Id = source.Id; dest.DefinitionId = source.DefinitionId; });
            CopyList(Troops, target.Troops, (source, dest) => source.CopyTo(dest));
            CopyList(Defenses, target.Defenses, (source, dest) => source.CopyTo(dest));
            target._effectSequence = _effectSequence;
            CopyList(VisualEffects, target.VisualEffects, (source, dest) => source.CopyTo(dest));
            CopyList(Zones, target.Zones, (source, dest) => source.CopyTo(dest));
        }

        private static void CopyList<T>(List<T> source, List<T> target, System.Action<T, T> copy) where T : new()
        {
            while (target.Count < source.Count) target.Add(new T());
            if (target.Count > source.Count) target.RemoveRange(source.Count, target.Count - source.Count);
            for (int index = 0; index < source.Count; index++) copy(source[index], target[index]);
        }

        public sealed class Card
        {
            public int Id { get; internal set; }
            public string DefinitionId { get; internal set; }

            internal Card Clone() => (Card)MemberwiseClone();
        }

        public sealed class Unit
        {
            public int Id { get; internal set; }
            public string DefinitionId { get; internal set; }
            public string LaneId { get; internal set; }
            public double Distance { get; internal set; }
            public SiegePoint? FreePosition { get; internal set; }
            public double Hp { get; internal set; }
            public int Shield { get; internal set; }
            public int Brace { get; internal set; }
            public int Reroutes { get; internal set; }
            public int TransitRemainingMs { get; internal set; }
            public SiegePoint TransitFrom { get; internal set; }
            public SiegePoint TransitTo { get; internal set; }
            public int BraceReadyMs { get; internal set; }
            public int AbilityCharges { get; internal set; }
            public int HeldUntilMs { get; internal set; }
            // Shared re-grab protection; retain the original snapshot name for existing consumers.
            public int HoldProtectedUntilMs { get; internal set; }
            public bool IsCarried { get; internal set; }
            public int CarryStartedMs { get; internal set; }
            public int CarriedUntilMs { get; internal set; }
            public double CarryFromDistance { get; internal set; }
            public double CarryToDistance { get; internal set; }
            internal int BraceRearmMs;
            internal bool BurstUsed;
            internal string BurstTowerId;
            internal int BurstHitsLeft;
            internal int BurstCooldownMs;
            internal int NextAttackMs,
                NextAuraMs;
            internal bool Arrived;

            internal void CopyTo(Unit target)
            {
                target.Id = Id;
                target.DefinitionId = DefinitionId;
                target.LaneId = LaneId;
                target.Distance = Distance;
                target.FreePosition = FreePosition;
                target.Hp = Hp;
                target.Shield = Shield;
                target.Brace = Brace;
                target.Reroutes = Reroutes;
                target.TransitRemainingMs = TransitRemainingMs;
                target.TransitFrom = TransitFrom;
                target.TransitTo = TransitTo;
                target.BraceReadyMs = BraceReadyMs;
                target.AbilityCharges = AbilityCharges;
                target.HeldUntilMs = HeldUntilMs;
                target.HoldProtectedUntilMs = HoldProtectedUntilMs;
                target.IsCarried = IsCarried;
                target.CarryStartedMs = CarryStartedMs;
                target.CarriedUntilMs = CarriedUntilMs;
                target.CarryFromDistance = CarryFromDistance;
                target.CarryToDistance = CarryToDistance;
            }

            internal Unit Clone() => (Unit)MemberwiseClone();
        }

        public sealed class Tower
        {
            public string Id { get; internal set; }
            public string DefinitionId { get; internal set; }
            public string LaneId { get; internal set; }
            public double Distance { get; internal set; }
            public SiegePoint? FreePosition { get; internal set; }
            public double Hp { get; internal set; }
            public TowerStatus Status { get; internal set; }
            public int ReadyAtMs { get; internal set; }
            public bool WindingUp { get; internal set; }
            public int WarningReadyMs { get; internal set; }
            public int SeenTurn { get; internal set; }
            public SiegePoint LockedPoint { get; internal set; }
            internal int NextShotMs, InterferenceReadyMs;
            public int NextAttackAtMs => NextShotMs;

            internal void CopyTo(Tower target)
            {
                target.Id = Id;
                target.DefinitionId = DefinitionId;
                target.LaneId = LaneId;
                target.Distance = Distance;
                target.FreePosition = FreePosition;
                target.Hp = Hp;
                target.Status = Status;
                target.ReadyAtMs = ReadyAtMs;
                target.WindingUp = WindingUp;
                target.WarningReadyMs = WarningReadyMs;
                target.SeenTurn = SeenTurn;
                target.LockedPoint = LockedPoint;
                target.NextShotMs = NextShotMs;
                target.InterferenceReadyMs = InterferenceReadyMs;
            }

            internal Tower Clone() => (Tower)MemberwiseClone();
        }

        public sealed class Animal
        {
            public string Vigilance { get; internal set; }
            public string Stamina { get; internal set; }
            public double Charge { get; internal set; }
            public int Actions { get; internal set; }
            public int BuildsLeft { get; internal set; }
            public int RepairsLeft { get; internal set; }
            public string RepairTowerId { get; internal set; }
            public int RepairReadyAtMs { get; internal set; }
            public int NextRepairMs => SkillReadyAtMs(IntentKind.Repair);
            public string GuardLane { get; internal set; }
            public int GuardUntilMs { get; internal set; }
            public int RecoveryUntilMs { get; internal set; }
            public IntentKind Intent { get; internal set; }
            public string IntentLane { get; internal set; }
            public string IntentPad { get; internal set; }
            public int SeenTurn { get; internal set; }
            public SiegePoint SweepCenter { get; internal set; }
            public double SweepRadius { get; internal set; }
            public int LeapStartMs { get; internal set; }
            public int ImpactAtMs => Cast != null && Cast.Kind == IntentKind.Sweep
                && Cast.Phase == CastPhase.Windup ? Cast.ResolveAtMs : 0;
            public int IntentUnitId { get; internal set; }
            public int HeldUnitId { get; internal set; }
            public int HoldStartedMs { get; internal set; }
            public int HoldUntilMs { get; internal set; }
            public int NextHoldMs => SkillReadyAtMs(IntentKind.Hold);
            public int CarriedUnitId => Cast != null && Cast.Kind == IntentKind.Carry
                && Cast.Phase == CastPhase.Active ? Cast.Target.UnitId : 0;
            public SkillCast Cast { get; internal set; }
            internal Dictionary<IntentKind, int> SkillCooldowns = new Dictionary<IntentKind, int>();
            internal int NextSlapMs;

            public int SkillReadyAtMs(IntentKind kind) => SkillCooldowns.TryGetValue(kind, out int ready) ? ready : 0;

            internal void CopyTo(Animal target)
            {
                target.Vigilance = Vigilance;
                target.Stamina = Stamina;
                target.Charge = Charge;
                target.Actions = Actions;
                target.BuildsLeft = BuildsLeft;
                target.RepairsLeft = RepairsLeft;
                target.RepairTowerId = RepairTowerId;
                target.RepairReadyAtMs = RepairReadyAtMs;
                target.GuardLane = GuardLane;
                target.GuardUntilMs = GuardUntilMs;
                target.RecoveryUntilMs = RecoveryUntilMs;
                target.Intent = Intent;
                target.IntentLane = IntentLane;
                target.IntentPad = IntentPad;
                target.SeenTurn = SeenTurn;
                target.SweepCenter = SweepCenter;
                target.SweepRadius = SweepRadius;
                target.LeapStartMs = LeapStartMs;
                target.IntentUnitId = IntentUnitId;
                target.HeldUnitId = HeldUnitId;
                target.HoldStartedMs = HoldStartedMs;
                target.HoldUntilMs = HoldUntilMs;
                if (Cast == null) target.Cast = null;
                else
                {
                    if (target.Cast == null) target.Cast = new SkillCast();
                    Cast.CopyTo(target.Cast);
                }
                target.SkillCooldowns.Clear();
                foreach (var pair in SkillCooldowns) target.SkillCooldowns.Add(pair.Key, pair.Value);
            }

            internal Animal Clone()
            {
                var copy = (Animal)MemberwiseClone();
                copy.Cast = Cast?.Clone();
                copy.SkillCooldowns = new Dictionary<IntentKind, int>(SkillCooldowns);
                return copy;
            }
        }

        public static class EffectKind
        {
            public const string Deploy = "deploy";
            public const string UnitLost = "unitLost";
            public const string TowerLost = "towerLost";
            public const string TowerShot = "towerShot";
            public const string UnitShot = "unitShot";
        }

        public sealed class Effect
        {
            public string Kind { get; internal set; }
            public SiegePoint From { get; internal set; }
            public SiegePoint To { get; internal set; }
            public double Radius { get; internal set; }
            public int UntilMs { get; internal set; }
            public int StartMs { get; internal set; }
            public string Visual { get; internal set; }
            public int Sequence { get; internal set; }
            public int UnitId { get; internal set; }

            internal void CopyTo(Effect target)
            {
                target.Kind = Kind;
                target.From = From;
                target.To = To;
                target.Radius = Radius;
                target.UntilMs = UntilMs;
                target.StartMs = StartMs;
                target.Visual = Visual;
                target.Sequence = Sequence;
                target.UnitId = UnitId;
            }

            internal Effect Clone() => (Effect)MemberwiseClone();
        }

        public sealed class SlowZone
        {
            public SiegePoint Center { get; internal set; }
            public double Radius { get; internal set; }
            public double Factor { get; internal set; }
            public int UntilMs { get; internal set; }
            public string Visual { get; internal set; }

            internal void CopyTo(SlowZone target)
            {
                target.Center = Center;
                target.Radius = Radius;
                target.Factor = Factor;
                target.UntilMs = UntilMs;
                target.Visual = Visual;
            }

            internal SlowZone Clone() => (SlowZone)MemberwiseClone();
        }

        public sealed class Statistics
        {
            public int Deployed { get; internal set; }
            public int Merges { get; internal set; }
            public int Towers { get; internal set; }
            public int Delivered { get; internal set; }
            public int Lost { get; internal set; }
            public int Repairs { get; internal set; }
            public int Holds { get; internal set; }
            public int HoldMisses { get; internal set; }
            public int Carries { get; internal set; }
            public int CarryMisses { get; internal set; }

            internal void CopyTo(Statistics target)
            {
                target.Deployed = Deployed;
                target.Merges = Merges;
                target.Towers = Towers;
                target.Delivered = Delivered;
                target.Lost = Lost;
                target.Repairs = Repairs;
                target.Holds = Holds;
                target.HoldMisses = HoldMisses;
                target.Carries = Carries;
                target.CarryMisses = CarryMisses;
            }

            internal Statistics Clone() => (Statistics)MemberwiseClone();
        }
    }
}
