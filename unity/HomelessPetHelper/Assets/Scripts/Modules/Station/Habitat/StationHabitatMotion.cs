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
        public const float RiseSeconds = 1.6f;
        public static readonly HabitatPoint RestPoint = new HabitatPoint(.24, .34);
        private readonly Random _random;
        private readonly StationHabitatArea _area;
        private double _remaining = 3;
        private double _cooldown;
        private int _arrivals;
        private bool _goingToRest;
        private HabitatAction _interrupted;
        private double _interruptedRemaining;
        private double _interruptedPoseSeconds;
        private bool _interruptedRising;

        public HabitatPoint Position { get; private set; } = new HabitatPoint(.58, .21);
        public HabitatPoint Destination { get; private set; }
        public HabitatAction Action { get; private set; } = HabitatAction.Observe;
        public HabitatAction Pose => Action == HabitatAction.Attention &&
            (_interrupted == HabitatAction.Sit || _interrupted == HabitatAction.Lie || _interruptedRising) ?
            _interrupted : Action;
        public HabitatResponse Response { get; private set; }
        public bool FacingLeft { get; private set; }
        public double ActionSeconds { get; private set; }
        public double PoseSeconds => Action == HabitatAction.Attention && Pose != Action ?
            _interruptedPoseSeconds + (_interruptedRising ? 0 : ActionSeconds) : ActionSeconds;
        public bool RisingFromRest { get; private set; }

        public StationHabitatMotion(int seed, StationHabitatArea area = null)
        {
            _random = new Random(seed);
            _area = area;
            if (area != null) Position = area.Start;
            Destination = Position;
        }

        public void Tick(double seconds, bool suspended = false)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (suspended || seconds == 0) return;
            // Never catch up time spent away, even if a caller supplies a large foreground delta.
            seconds = Math.Min(seconds, .1);
            _cooldown = Math.Max(0, _cooldown - seconds);
            double previousSeconds = ActionSeconds;
            ActionSeconds += seconds;
            if (Action == HabitatAction.Walk)
            {
                // Stand up in place before translating the same skin; no seated sliding.
                double walkingSeconds = RisingFromRest ?
                    Math.Max(0, ActionSeconds - RiseSeconds) - Math.Max(0, previousSeconds - RiseSeconds) : seconds;
                if (walkingSeconds > 0) Walk(walkingSeconds);
                return;
            }
            _remaining -= seconds;
            if (_remaining > 0) return;
            if (Action == HabitatAction.Attention)
            {
                Response = HabitatResponse.None;
                SetAction(_interrupted, _interruptedRemaining);
                ActionSeconds = _interruptedPoseSeconds + (_interrupted == HabitatAction.Walk ? 0 : 1.4);
                RisingFromRest = _interruptedRising;
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
            _interruptedPoseSeconds = ActionSeconds;
            _interruptedRising = RisingFromRest;
            Response = acceptsTouch ? HabitatResponse.Willing : HabitatResponse.NeedsSpace;
            _cooldown = 3;
            SetAction(HabitatAction.Attention, 1.4);
            RisingFromRest = _interruptedRising;
            return true;
        }

        public static bool IsWalkable(HabitatPoint point) => point.X >= MinX && point.X <= MaxX &&
            point.Y >= MinY && point.Y <= MaxY;

        private void BeginWalk()
        {
            _goingToRest = _arrivals % 3 == 2;
            HabitatPoint rest = _area == null ? RestPoint : _area.Rest;
            _goingToRest &= _area == null || _area.CanTraverse(Position, rest);
            Destination = _goingToRest ? rest : ChoosePoint();
            if (Position.DistanceTo(Destination) < .001)
            {
                SetAction(HabitatAction.Observe, 2.8);
                return;
            }
            bool rising = Action == HabitatAction.Sit || Action == HabitatAction.Lie;
            if (Math.Abs(Destination.X - Position.X) > .01) FacingLeft = Destination.X < Position.X;
            SetAction(HabitatAction.Walk, 0);
            RisingFromRest = rising;
        }

        private HabitatPoint ChoosePoint()
        {
            for (int i = 0; i < 12; i++)
            {
                double minX = _area == null ? MinX : _area.Min.X, maxX = _area == null ? MaxX : _area.Max.X;
                double minY = _area == null ? MinY : _area.Min.Y, maxY = _area == null ? MaxY : _area.Max.Y;
                var point = new HabitatPoint(minX + _random.NextDouble() * (maxX - minX),
                    minY + _random.NextDouble() * (maxY - minY));
                if (Position.DistanceTo(point) >= (_area == null ? .2 : .12) &&
                    (_area == null || _area.CanTraverse(Position, point)))
                    return point;
            }
            return _area == null ? new HabitatPoint(Position.X < .5 ? MaxX : MinX, .2) : Position;
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
            RisingFromRest = false;
            ActionSeconds = 0;
            _remaining = duration;
        }
    }
}
