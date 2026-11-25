using System;

namespace HomelessPetHelper.Modules.WorldTime
{
    /// <summary>
    /// Pure conversion from accumulated game minutes to an in-game calendar date.
    /// Rules: 24 hours per day (1440 minutes by default), 30 days per month, 12 months per year.
    /// No wall clock, time zone or system-calendar dependency; the date is derived only from persisted
    /// ElapsedMinutes, so changing the rules never mutates a save. Years and months are 1-based.
    /// </summary>
    public static class VirtualCalendar
    {
        public const int HoursPerDay = 24;
        public const int MinutesPerHour = 60;
        public const int DaysPerMonth = 30;
        public const int MonthsPerYear = 12;

        /// <summary>Converts elapsed game minutes into a 1-based calendar date at the given cadence.</summary>
        public static VirtualDate ToDate(long elapsedMinutes, int minutesPerDay)
        {
            if (minutesPerDay <= 0) throw new ArgumentOutOfRangeException(nameof(minutesPerDay));
            if (elapsedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinutes));

            long totalDays = elapsedMinutes / minutesPerDay;
            int minuteOfDay = (int)(elapsedMinutes % minutesPerDay);
            // Keep the year in long: a configurable cadence can produce a day count far beyond
            // int.MaxValue, and the calendar must still render instead of throwing.
            long year = totalDays / (DaysPerMonth * MonthsPerYear) + 1;
            int dayOfYear = (int)(totalDays % (DaysPerMonth * MonthsPerYear));
            int month = dayOfYear / DaysPerMonth + 1;
            int day = dayOfYear % DaysPerMonth + 1;

            // Clamp the clock fields when a custom cadence is shorter than a 24-hour day.
            int minutesPerHour = Math.Min(MinutesPerHour, minutesPerDay);
            int hour = minuteOfDay / minutesPerHour;
            int minute = minuteOfDay % minutesPerHour;
            return new VirtualDate(year, month, day, hour, minute);
        }
    }
}
