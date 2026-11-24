namespace HomelessPetHelper.Modules.Siege
{
    public static class SiegeGeometry
    {
        public static SiegePoint Position(SiegeCatalog catalog, SiegeState.Unit unit) =>
            unit.TransitRemainingMs > 0
                ? unit.TransitFrom.Lerp(
                    unit.TransitTo,
                    1 - (double)unit.TransitRemainingMs / catalog.Rules.RerouteDurationMs
                )
                : unit.FreePosition ?? catalog.Lane(unit.LaneId).PointAt(unit.Distance);

        public static SiegePoint Position(SiegeCatalog catalog, SiegeState.Tower tower) =>
            tower.FreePosition ?? catalog.Lane(tower.LaneId).PointAt(tower.Distance);
    }
}
