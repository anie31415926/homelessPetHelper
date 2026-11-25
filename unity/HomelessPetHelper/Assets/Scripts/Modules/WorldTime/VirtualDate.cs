using System;
using System.Globalization;

namespace HomelessPetHelper.Modules.WorldTime
{
    /// <summary>
    /// Immutable in-game calendar date derived from accumulated game minutes. Years and months are
    /// 1-based; hours fit the configured minutes-per-day cadence. Pure value object, no engine dependency.
    /// </summary>
    public readonly struct VirtualDate : IEquatable<VirtualDate>
    {
        public long Year { get; }
        public int Month { get; }
        public int Day { get; }
        public int Hour { get; }
        public int Minute { get; }

        public VirtualDate(long year, int month, int day, int hour, int minute)
        {
            if (year < 1) throw new ArgumentOutOfRangeException(nameof(year));
            if (month < 1 || month > VirtualCalendar.MonthsPerYear)
                throw new ArgumentOutOfRangeException(nameof(month));
            if (day < 1 || day > VirtualCalendar.DaysPerMonth)
                throw new ArgumentOutOfRangeException(nameof(day));
            // The hour is derived from a configurable minutes-per-day cadence, so it is not capped at 23.
            if (hour < 0)
                throw new ArgumentOutOfRangeException(nameof(hour));
            if (minute < 0 || minute >= VirtualCalendar.MinutesPerHour)
                throw new ArgumentOutOfRangeException(nameof(minute));
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
            Minute = minute;
        }

        /// <summary>Short, culture-stable form used by the shared presentation layer.</summary>
        public string ToClockText()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}年{1}月{2}日 {3:00}:{4:00}",
                Year, Month, Day, Hour, Minute);
        }

        public bool Equals(VirtualDate other)
        {
            return Year == other.Year && Month == other.Month && Day == other.Day
                && Hour == other.Hour && Minute == other.Minute;
        }

        public override bool Equals(object obj) => obj is VirtualDate other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Year.GetHashCode();
                hash = (hash * 397) ^ Month;
                hash = (hash * 397) ^ Day;
                hash = (hash * 397) ^ Hour;
                hash = (hash * 397) ^ Minute;
                return hash;
            }
        }

        public override string ToString() => ToClockText();
    }
}
