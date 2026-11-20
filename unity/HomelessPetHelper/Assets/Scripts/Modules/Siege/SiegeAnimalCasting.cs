using System;
using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal static class SiegeAnimalCasting
    {
        private static readonly SiegeAnimalSkill[] Skills =
        {
            new SiegeGuard(), new SiegeSweep(), new SiegeBuild(), new SiegeRepair(), new SiegeHold(),
        };

        private static SiegeAnimalSkill Skill(IntentKind kind)
        {
            foreach (var skill in Skills)
                if (skill.Kind == kind) return skill;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown animal skill.");
        }

        internal static void Plan(SiegeState s, SiegeCatalog c, IntentKind kind, SkillTarget target)
        {
            var skill = Skill(kind);
            var a = s.AnimalState;
            a.Intent = kind;
            a.IntentLane = target.LaneId;
            a.IntentPad = target.PadId;
            a.IntentUnitId = target.UnitId;
            a.SeenTurn = s.Turn + (skill.RequiresDecision ? 1 : 0);
            skill.Plan(s, c);
        }

        internal static bool TryRelease(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            var skill = Skill(a.Intent);
            if (s.Phase != BattlePhase.Running || s.Paused || a.Charge < 1 || a.Cast != null
                || s.ElapsedMs < a.RecoveryUntilMs || s.ElapsedMs < a.SkillReadyAtMs(skill.Kind)
                || (skill.RequiresDecision && s.ResolvedTurn < a.SeenTurn))
                return false;
            a.Cast = new SkillCast
            {
                Kind = skill.Kind, Phase = CastPhase.Windup,
                Target = new SkillTarget(a.IntentLane, a.IntentPad, a.IntentUnitId),
                Point = a.SweepCenter, Radius = a.SweepRadius,
                StartedMs = s.ElapsedMs, ResolveAtMs = s.ElapsedMs + skill.DelayMs(s, c),
                RecoveryMs = skill.RecoveryMs(s, c),
            };
            if (skill.StopsGuard) a.GuardUntilMs = s.ElapsedMs;
            a.Actions++;
            a.Charge = 0;
            if (a.Cast.RecoveryMs > 0 || a.Cast.ResolveAtMs > a.Cast.StartedMs)
                a.RecoveryUntilMs = a.Cast.ResolveAtMs + a.Cast.RecoveryMs;
            skill.Begin(s, c);
            Advance(s, c);
            SiegeCombat.RemoveLost(s, c);
            SiegeAnimal.ChooseNext(s, c, skill.Kind, a.IntentLane);
            return true;
        }

        internal static void Advance(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            var cast = a.Cast;
            if (cast == null || s.Phase != BattlePhase.Running || s.Paused) return;
            var skill = Skill(cast.Kind);
            if (cast.Phase == CastPhase.Windup && s.ElapsedMs >= cast.ResolveAtMs)
                Resolve(s, c, skill);
            if (cast.Phase == CastPhase.Active
                && (s.ElapsedMs >= cast.UntilMs || skill.HasEnded(s, c)))
            {
                if (cast.RecoveryMs > 0 || cast.UntilMs > cast.ResolveAtMs)
                    a.RecoveryUntilMs = s.ElapsedMs + cast.RecoveryMs;
                cast.Phase = CastPhase.Recovery;
                cast.UntilMs = s.ElapsedMs;
                skill.End(s, c);
            }
            if (cast.Phase == CastPhase.Recovery && s.ElapsedMs >= a.RecoveryUntilMs)
                a.Cast = null;
        }

        private static void Resolve(SiegeState s, SiegeCatalog c, SiegeAnimalSkill skill)
        {
            var a = s.AnimalState;
            var cast = a.Cast;
            // Change phase before invoking effects; a second Advance cannot resolve this cast twice.
            cast.Phase = CastPhase.Active;
            bool valid = skill.CanResolve(s, c);
            int duration = valid ? skill.Resolve(s, c) : 0;
            if (!valid) skill.Miss(s, c);
            int cooldown = skill.CooldownMs(s, c);
            if (cooldown > 0 && (valid || skill.CooldownOnMiss))
                a.SkillCooldowns[skill.Kind] = cast.StartedMs + cooldown;
            cast.UntilMs = s.ElapsedMs + duration;
            if (duration > 0 || cast.RecoveryMs > 0)
                a.RecoveryUntilMs = cast.UntilMs + cast.RecoveryMs;
        }

        internal static void TransferMerge(SiegeState s, Unit[] materials, Unit merged)
        {
            var a = s.AnimalState;
            if (materials.Any(x => x.Id == a.IntentUnitId))
            {
                a.IntentUnitId = merged.Id;
                a.IntentLane = merged.LaneId;
            }
            if (a.Cast != null && materials.Any(x => x.Id == a.Cast.Target.UnitId))
                a.Cast.Target = new SkillTarget(merged.LaneId, a.Cast.Target.PadId, merged.Id);
        }
    }
}
