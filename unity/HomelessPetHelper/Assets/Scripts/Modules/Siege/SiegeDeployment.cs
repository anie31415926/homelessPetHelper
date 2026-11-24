using System;

namespace HomelessPetHelper.Modules.Siege
{
    /// <summary>Shared rule-space geometry for legal deployment and defender occupancy.</summary>
    public static class SiegeDeployment
    {
        public static double Fraction(SiegeCatalog c, SiegePoint point)
        {
            var lane = c.Lanes[0];
            var start = lane.PointAt(0);
            var end = lane.PointAt(lane.Length);
            double dx = end.X - start.X, dy = end.Y - start.Y;
            return ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (dx * dx + dy * dy);
        }

        public static bool InBounds(SiegeCatalog c, SiegePoint p) => SiegeCatalog.Finite(p.X) &&
            SiegeCatalog.Finite(p.Y) && p.X >= 0 && p.Y >= 0 && p.X <= c.Width && p.Y <= c.Height;

        public static bool CanDeploy(SiegeCatalog c, SiegePoint p) => InBounds(c, p) &&
            Fraction(c, p) >= -.1 && Fraction(c, p) <= c.Rules.DeploymentFraction;

        public static SiegeLane NearestLane(SiegeCatalog c, SiegePoint p)
        {
            SiegeLane best = null;
            double separation = double.PositiveInfinity;
            for (int index = 0; index < c.Lanes.Count; index++)
            {
                var lane = c.Lanes[index];
                if (!lane.Open) continue;
                double distance = p.Distance(lane.PointAt(lane.Project(p)));
                if (distance >= separation) continue;
                best = lane; separation = distance;
            }
            return best;
        }

        public static int Cell(SiegeCatalog c, SiegePoint p)
        {
            int size = c.Rules.DefenseGridSize;
            return Math.Min(size - 1, (int)(p.Y / c.Height * size)) * size +
                Math.Min(size - 1, (int)(p.X / c.Width * size));
        }

        public static SiegePoint CellCenter(SiegeCatalog c, int cell)
        {
            int size = c.Rules.DefenseGridSize;
            return new SiegePoint((cell % size + .5) * c.Width / size,
                (cell / size + .5) * c.Height / size);
        }
    }
}
