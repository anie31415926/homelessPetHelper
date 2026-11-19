using System;
using System.Collections.Generic;
using System.Linq;

namespace HomelessPetHelper.Modules.Siege
{
    public sealed class SiegeLane
    {
        public string Id { get; }
        public string NameKey { get; }
        public bool Open { get; }
        public IReadOnlyList<SiegePoint> Points { get; }
        public double Length { get; }

        internal SiegeLane(SiegeCatalogData.LaneDto data, double width, double height)
        {
            SiegeCatalog.Require(
                data.points != null && data.points.Length >= 2 && !string.IsNullOrWhiteSpace(data.nameKey),
                "route"
            );
            Id = data.id;
            NameKey = data.nameKey;
            Open = data.open;
            Points = Array.AsReadOnly(data.points.Select(x => new SiegePoint(x.x, x.y)).ToArray());
            for (int i = 0; i < Points.Count; i++)
            {
                var point = Points[i];
                SiegeCatalog.Require(
                    SiegeCatalog.Finite(point.X)
                        && SiegeCatalog.Finite(point.Y)
                        && point.X >= 0
                        && point.X <= width
                        && point.Y >= 0
                        && point.Y <= height,
                    "point bounds"
                );
                if (i == 0)
                    continue;
                double segment = point.Distance(Points[i - 1]);
                SiegeCatalog.Require(segment > 0, "zero route segment");
                Length += segment;
            }
        }

        public SiegePoint PointAt(double distance)
        {
            double remaining = Math.Max(0, distance);
            for (int i = 1; i < Points.Count; i++)
            {
                double length = Points[i - 1].Distance(Points[i]);
                if (remaining <= length)
                    return Points[i - 1].Lerp(Points[i], remaining / length);
                remaining -= length;
            }
            return Points[Points.Count - 1];
        }

        public double Project(SiegePoint point)
        {
            double bestDistance = 0,
                bestSeparation = double.PositiveInfinity,
                walked = 0;
            for (int i = 1; i < Points.Count; i++)
            {
                var a = Points[i - 1];
                var b = Points[i];
                double dx = b.X - a.X,
                    dy = b.Y - a.Y,
                    length = a.Distance(b);
                double fraction = Math.Max(
                    0,
                    Math.Min(1, ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / (length * length))
                );
                double separation = point.Distance(a.Lerp(b, fraction));
                if (separation < bestSeparation)
                {
                    bestSeparation = separation;
                    bestDistance = walked + length * fraction;
                }
                walked += length;
            }
            return bestDistance;
        }
    }
}
