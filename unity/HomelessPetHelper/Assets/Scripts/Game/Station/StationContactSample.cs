using System;

namespace HomelessPetHelper.Game.Station
{
    // Small deterministic fixtures, not a production relationship or medical simulation.
    public sealed class StationContactSample
    {
        public bool NeedsSpace { get; private set; }
        public bool AcceptsTouch { get; }
        public bool CoatRestricted { get; }
        public bool HasAcceptedPet { get; private set; }
        public bool QuietSpaceArranged { get; private set; }
        private long _quietSince;
        private readonly bool _acceptsCoatCare;

        public bool CanCare => _acceptsCoatCare && !NeedsSpace && !CoatRestricted;
        public string SignalKey => CoatRestricted ? "contactRestricted" : NeedsSpace ? "contactSpace" :
            AcceptsTouch ? "contactWilling" : "contactQuiet";

        public StationContactSample(bool needsSpace, bool acceptsTouch, bool coatRestricted, bool acceptsCoatCare)
        {
            NeedsSpace = needsSpace;
            AcceptsTouch = acceptsTouch;
            CoatRestricted = coatRestricted;
            _acceptsCoatCare = acceptsCoatCare;
        }

        public string Pet()
        {
            if (NeedsSpace) return "petSpace";
            if (!AcceptsTouch) return "petQuiet";
            HasAcceptedPet = true;
            return CoatRestricted ? "petRestricted" : "petted";
        }

        public void ArrangeQuietSpace(long elapsedMinutes)
        {
            if (elapsedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinutes));
            if (QuietSpaceArranged) return;
            QuietSpaceArranged = true;
            _quietSince = elapsedMinutes;
        }

        public void ObserveSettledTime(long elapsedMinutes)
        {
            if (QuietSpaceArranged && elapsedMinutes >= _quietSince && elapsedMinutes - _quietSince >= 10)
            {
                NeedsSpace = false;
            }
        }
    }
}
