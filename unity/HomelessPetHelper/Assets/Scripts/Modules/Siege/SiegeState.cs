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

            internal SkillCast Clone() => (SkillCast)MemberwiseClone();
        }

        public string SessionId { get; internal set; }
        public uint Seed { get; internal set; }
        public string Profile { get; internal set; }
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
        public IReadOnlyList<Card> Hand => Cards.AsReadOnly();
        public IReadOnlyList<Unit> Units => Troops.AsReadOnly();
        public IReadOnlyList<Tower> Towers => Defenses.AsReadOnly();
        public IReadOnlyList<Effect> Effects => VisualEffects.AsReadOnly();
        public IReadOnlyList<SlowZone> SlowZones => Zones.AsReadOnly();
        public IReadOnlyList<string> Log => Events.AsReadOnly();
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

        internal SiegeState Clone()
        {
            var copy = (SiegeState)MemberwiseClone();
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

            internal Unit Clone() => (Unit)MemberwiseClone();
        }

        public sealed class Tower
        {
            public string Id { get; internal set; }
            public string DefinitionId { get; internal set; }
            public string LaneId { get; internal set; }
            public double Distance { get; internal set; }
            public double Hp { get; internal set; }
            public TowerStatus Status { get; internal set; }
            public int ReadyAtMs { get; internal set; }
            public bool WindingUp { get; internal set; }
            public int WarningReadyMs { get; internal set; }
            public int SeenTurn { get; internal set; }
            public SiegePoint LockedPoint { get; internal set; }
            internal int NextShotMs;

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

            internal Animal Clone()
            {
                var copy = (Animal)MemberwiseClone();
                copy.Cast = Cast?.Clone();
                copy.SkillCooldowns = new Dictionary<IntentKind, int>(SkillCooldowns);
                return copy;
            }
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

            internal Effect Clone() => (Effect)MemberwiseClone();
        }

        public sealed class SlowZone
        {
            public SiegePoint Center { get; internal set; }
            public double Radius { get; internal set; }
            public double Factor { get; internal set; }
            public int UntilMs { get; internal set; }
            public string Visual { get; internal set; }

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

            internal Statistics Clone() => (Statistics)MemberwiseClone();
        }
    }
}
