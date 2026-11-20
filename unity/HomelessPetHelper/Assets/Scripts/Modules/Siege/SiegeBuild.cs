using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal sealed class SiegeBuild : SiegeAnimalSkill
    {
        internal override IntentKind Kind => IntentKind.Build;
        internal override void Plan(SiegeState s, SiegeCatalog c) => SiegeEntities.Record(s, c, "event.build");

        internal override bool CanResolve(SiegeState s, SiegeCatalog c)
        {
            var pad = c.Pads.FirstOrDefault(x => x.Id == s.AnimalState.Cast.Target.PadId);
            return pad != null && s.AnimalState.BuildsLeft > 0 && s.Defenses.All(x => x.Id != pad.Id);
        }

        internal override int Resolve(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            var pad = c.Pads.First(x => x.Id == a.Cast.Target.PadId);
            var encounter = s.EncounterId == null ? null : c.Content.Encounter(s.EncounterId);
            string typeId = encounter == null ? null : encounter.BuildTowerIds[
                (c.Rules.AnimalBuilds - a.BuildsLeft) % encounter.BuildTowerIds.Count];
            s.Defenses.Add(new Tower
            {
                Id = pad.Id, DefinitionId = typeId, LaneId = pad.LaneId,
                Distance = c.Lane(pad.LaneId).Length * pad.At,
                Hp = typeId == null ? c.Tower.Hp : c.Content.Tower(typeId).Hp,
                Status = TowerStatus.Building,
                ReadyAtMs = s.ElapsedMs + c.Rules.BuildDurationMs,
                NextShotMs = s.ElapsedMs + c.Rules.BuildDurationMs,
            });
            a.BuildsLeft--;
            return 0;
        }
    }
}
