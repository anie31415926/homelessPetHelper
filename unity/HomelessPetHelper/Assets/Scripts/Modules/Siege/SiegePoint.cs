using System;

namespace HomelessPetHelper.Modules.Siege
{
    public readonly struct SiegePoint
    {
        public double X { get; }
        public double Y { get; }

        public SiegePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double Distance(SiegePoint other) =>
            Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));

        public SiegePoint Lerp(SiegePoint other, double fraction) =>
            new SiegePoint(X + (other.X - X) * fraction, Y + (other.Y - Y) * fraction);
    }
}
