using System;

namespace HomelessPetHelper.Modules.Station.Habitat
{
    /// <summary>Bounded ground footprints for presentation, not a physics or pathfinding engine.</summary>
    public sealed class StationHabitatArea
    {
        private readonly HabitatPoint[] _centers, _radii;
        public HabitatPoint Min { get; }
        public HabitatPoint Max { get; }
        public HabitatPoint Start { get; }
        public HabitatPoint Rest { get; }

        public StationHabitatArea(HabitatPoint min, HabitatPoint max, HabitatPoint start, HabitatPoint rest,
            HabitatPoint[] centers, HabitatPoint[] radii)
        {
            if (!Finite(min) || !Finite(max) || min.X >= max.X || min.Y >= max.Y ||
                centers == null || radii == null || centers.Length != radii.Length)
                throw new ArgumentException("Invalid habitat floor bounds.");
            Min = min; Max = max;
            _centers = (HabitatPoint[])centers.Clone();
            _radii = (HabitatPoint[])radii.Clone();
            for (int i = 0; i < centers.Length; i++)
                if (!Finite(centers[i]) || !Finite(radii[i]) || radii[i].X <= 0 || radii[i].Y <= 0)
                    throw new ArgumentException("Invalid furniture footprint.");
            if (!Contains(start) || !Contains(rest)) throw new ArgumentException("Start/rest must be clear.");
            Start = start; Rest = rest;
        }

        public bool Contains(HabitatPoint point)
        {
            if (!Finite(point) || point.X < Min.X || point.X > Max.X || point.Y < Min.Y || point.Y > Max.Y)
                return false;
            for (int i = 0; i < _centers.Length; i++)
                if (DistanceSquared(point, _centers[i], _radii[i]) <= 1) return false;
            return true;
        }

        public bool CanTraverse(HabitatPoint from, HabitatPoint to)
        {
            if (!Contains(from) || !Contains(to)) return false;
            for (int i = 0; i < _centers.Length; i++)
            {
                double x = (from.X - _centers[i].X) / _radii[i].X;
                double y = (from.Y - _centers[i].Y) / _radii[i].Y;
                double dx = (to.X - from.X) / _radii[i].X;
                double dy = (to.Y - from.Y) / _radii[i].Y;
                double length = dx * dx + dy * dy;
                double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, -(x * dx + y * dy) / length));
                if ((x + t * dx) * (x + t * dx) + (y + t * dy) * (y + t * dy) <= 1) return false;
            }
            return true;
        }

        private static double DistanceSquared(HabitatPoint point, HabitatPoint center, HabitatPoint radius)
        {
            double x = (point.X - center.X) / radius.X, y = (point.Y - center.Y) / radius.Y;
            return x * x + y * y;
        }
        private static bool Finite(HabitatPoint point) => !double.IsNaN(point.X) && !double.IsInfinity(point.X) &&
            !double.IsNaN(point.Y) && !double.IsInfinity(point.Y);
    }
}
