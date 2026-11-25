using System;
using System.Globalization;

namespace HomelessPetHelper.Modules.WorldTime
{
    /// <summary>
    /// Single presentation entry for game-world time. Callers pass time values; no UI view assembles
    /// time strings itself. Culture-invariant so a device locale cannot change saved/computed values.
    /// </summary>
    public static class GameTimeText
    {
        /// <summary>Full in-game calendar clock, e.g. "1年3月12日 14:30".</summary>
        public static string FormatClock(WorldTimeSnapshot time)
        {
            if (time == null) throw new ArgumentNullException(nameof(time));
            return time.Date.ToClockText();
        }

        /// <summary>
        /// Action duration. Minutes below an hour stay as "X 分钟"; longer spans show "X 小时 Y 分"
        /// (a zero remainder is omitted). Negative input is rejected rather than silently clamped.
        /// </summary>
        public static string FormatDuration(long minutes)
        {
            if (minutes < 0) throw new ArgumentOutOfRangeException(nameof(minutes));
            int perHour = VirtualCalendar.MinutesPerHour;
            long hours = minutes / perHour;
            int rest = (int)(minutes % perHour);
            if (hours <= 0) return string.Format(CultureInfo.InvariantCulture, "{0} 分钟", rest);
            if (rest == 0) return string.Format(CultureInfo.InvariantCulture, "{0} 小时", hours);
            return string.Format(CultureInfo.InvariantCulture, "{0} 小时 {1} 分", hours, rest);
        }
    }
}
