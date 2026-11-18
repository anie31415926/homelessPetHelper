using System;

namespace HomelessPetHelper.Modules.Station.Cleaning
{
    /// <summary>Cumulative effective work, not pointer time. Retains sub-minute work across exits.</summary>
    public sealed class CareWorkMeter
    {
        private readonly DirtField _field;
        private readonly int _fullDurationMinutes;
        private readonly int _completionPercent;
        private long _settledMinutes;

        public long PendingMinutes => TotalMinutes - _settledMinutes;

        private long TotalMinutes
        {
            get
            {
                if (_field.InitialAmount == 0) return 0;
                long removed = _field.InitialAmount - _field.RemainingAmount;
                return Math.Min(_fullDurationMinutes,
                    removed * _fullDurationMinutes * 100 / (_field.InitialAmount * _completionPercent));
            }
        }

        public CareWorkMeter(DirtField field, int fullDurationMinutes, int completionPercent = 97)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            if (fullDurationMinutes < 1 || fullDurationMinutes > 1440)
            {
                throw new ArgumentOutOfRangeException(nameof(fullDurationMinutes));
            }

            if (completionPercent < 1 || completionPercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(completionPercent));
            }

            _fullDurationMinutes = fullDurationMinutes;
            _completionPercent = completionPercent;
        }

        /// <summary>Call only after the corresponding game-time delta was successfully committed.</summary>
        public void ConfirmSettlement()
        {
            _settledMinutes = TotalMinutes;
        }
    }
}
