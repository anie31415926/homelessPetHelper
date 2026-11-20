using System;

namespace HomelessPetHelper.Modules.Station.Habitat
{
    public enum HabitatAction { Observe, Walk, Sit, Lie, Attention }
    public enum HabitatResponse { None, Willing, NeedsSpace }

    public readonly struct HabitatPoint
    {
        public double X { get; }
        public double Y { get; }
        public HabitatPoint(double x, double y) { X = x; Y = y; }
        public double DistanceTo(HabitatPoint other) =>
            Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
    }

    /// <summary>Presentation seconds only. Has no animal, care or world-clock dependency.</summary>
    public sealed class StationHabitatMotion
    {
        public const double MinX = .18, MaxX = .82, MinY = .13, MaxY = .38;
        public static readonly HabitatPoint RestPoint = new HabitatPoint(.24, .34);
        private readonly Random _random;
        private double _remaining = 3;
        private double _cooldown;
        private int _arrivals;
        private bool _goingToRest;
        private HabitatAction _interrupted;
        private double _interruptedRemaining;

        public HabitatPoint Position { get; private set; } = new HabitatPoint(.58, .21);
        public HabitatPoint Destination { get; private set; }
        public HabitatAction Action { get; private set; } = HabitatAction.Observe;
        public HabitatAction Pose => Action == HabitatAction.Attention &&
            (_interrupted == HabitatAction.Sit || _interrupted == HabitatAction.Lie) ? _interrupted : Action;
        public HabitatResponse Response { get; private set; }
        public bool FacingLeft { get; private set; }
        public double ActionSeconds { get; private set; }

        public StationHabitatMotion(int seed) { _random = new Random(seed); Destination = Position; }

        public void Tick(double seconds, bool suspended = false)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (suspended || seconds == 0) return;
            // Never catch up time spent away, even if a caller supplies a large foreground delta.
            seconds = Math.Min(seconds, .1);
            _cooldown = Math.Max(0, _cooldown - seconds);
            ActionSeconds += seconds;
            if (Action == HabitatAction.Walk) { Walk(seconds); return; }
            _remaining -= seconds;
            if (_remaining > 0) return;
            if (Action == HabitatAction.Attention)
            {
                Response = HabitatResponse.None;
                SetAction(_interrupted, _interruptedRemaining);
                return;
            }
            if (Action == HabitatAction.Observe && _arrivals % 3 == 1)
                SetAction(HabitatAction.Sit, 5 + _random.NextDouble() * 3);
            else BeginWalk();
        }

        public bool TryGetAttention(bool acceptsTouch)
        {
            if (_cooldown > 0 || Action == HabitatAction.Attention) return false;
            _interrupted = Action;
            _interruptedRemaining = _remaining;
            Response = acceptsTouch ? HabitatResponse.Willing : HabitatResponse.NeedsSpace;
            _cooldown = 3;
            SetAction(HabitatAction.Attention, 1.4);
            return true;
        }

        public static bool IsWalkable(HabitatPoint point) => point.X >= MinX && point.X <= MaxX &&
            point.Y >= MinY && point.Y <= MaxY;

        private void BeginWalk()
        {
            _goingToRest = _arrivals % 3 == 2;
            Destination = _goingToRest ? RestPoint : ChoosePoint();
            if (Math.Abs(Destination.X - Position.X) > .01) FacingLeft = Destination.X < Position.X;
            SetAction(HabitatAction.Walk, 0);
        }

        private HabitatPoint ChoosePoint()
        {
            for (int i = 0; i < 12; i++)
            {
                var point = new HabitatPoint(MinX + _random.NextDouble() * (MaxX - MinX),
                    MinY + _random.NextDouble() * (MaxY - MinY));
                if (Position.DistanceTo(point) >= .2) return point;
            }
            return new HabitatPoint(Position.X < .5 ? MaxX : MinX, .2);
        }

        private void Walk(double seconds)
        {
            double distance = Position.DistanceTo(Destination);
            double step = .11 * seconds;
            if (distance <= step)
            {
                Position = Destination;
                _arrivals++;
                SetAction(_goingToRest ? HabitatAction.Lie : HabitatAction.Observe,
                    _goingToRest ? 10 + _random.NextDouble() * 4 : 2.8 + _random.NextDouble() * 2);
                return;
            }
            double t = step / distance;
            Position = new HabitatPoint(Position.X + (Destination.X - Position.X) * t,
                Position.Y + (Destination.Y - Position.Y) * t);
        }

        private void SetAction(HabitatAction action, double duration)
        {
            Action = action;
            ActionSeconds = 0;
            _remaining = duration;
        }
    }
}
