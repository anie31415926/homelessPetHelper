using System;
using HomelessPetHelper.Modules.WorldTime;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class VirtualCalendarTests
    {
        private const int Day = 1440;

        [TestCase(0L, 1, 1, 1, 0, 0)]
        [TestCase(90L, 1, 1, 1, 1, 30)]
        [TestCase(1440L, 1, 1, 2, 0, 0)]
        [TestCase(29L * Day, 1, 1, 30, 0, 0)]
        [TestCase(30L * Day, 1, 2, 1, 0, 0)]
        [TestCase(11L * 30 * Day + 29L * Day, 1, 12, 30, 0, 0)]
        [TestCase(12L * 30 * Day, 2, 1, 1, 0, 0)]
        [TestCase((365L * 12 + 5) * 30 * Day + 500L, 366, 6, 1, 8, 20)]
        public void ElapsedMinutesMapToInGameCalendar(long minutes, long year, int month, int day, int hour, int min)
        {
            Assert.That(VirtualCalendar.ToDate(minutes, Day),
                Is.EqualTo(new VirtualDate(year, month, day, hour, min)));
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VirtualCalendar.ToDate(-1, Day));
            Assert.Throws<ArgumentOutOfRangeException>(() => VirtualCalendar.ToDate(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualDate(0, 1, 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualDate(1, 13, 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualDate(1, 1, 31, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualDate(1, 1, 1, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualDate(1, 1, 1, 0, 60));
        }

        [Test]
        public void YearsBeyondIntMaxValueAreStillRendered()
        {
            long minutes = ((long)int.MaxValue + 1L) * 360L * Day;
            VirtualDate date = VirtualCalendar.ToDate(minutes, Day);
            Assert.That(date.Year, Is.GreaterThan(int.MaxValue));
            Assert.That(date.ToClockText(), Does.Contain(date.Year.ToString(
                System.Globalization.CultureInfo.InvariantCulture) + "年"));
        }

        [Test]
        public void SnapshotExposesCalendarWithoutChangingElapsedMinutes()
        {
            var clock = new ActionWorldClock(Guid.NewGuid(), Day, 30L * Day + 1500);
            VirtualDate date = clock.Current.Date;
            Assert.That(date, Is.EqualTo(new VirtualDate(1, 2, 2, 1, 0)));
            Assert.That(clock.Current.ElapsedMinutes, Is.EqualTo(30L * Day + 1500));
            Assert.That(clock.Current.DayIndex, Is.EqualTo(31));
            Assert.That(clock.Current.MinuteOfDay, Is.EqualTo(60));
        }

        [TestCase(0L, "0 分钟")]
        [TestCase(1L, "1 分钟")]
        [TestCase(59L, "59 分钟")]
        [TestCase(60L, "1 小时")]
        [TestCase(90L, "1 小时 30 分")]
        [TestCase(210L, "3 小时 30 分")]
        [TestCase(1440L, "24 小时")]
        public void DurationsUseOneSharedFormat(long minutes, string expected)
        {
            Assert.That(GameTimeText.FormatDuration(minutes), Is.EqualTo(expected));
        }

        [Test]
        public void ClockTextIsCultureInvariantAndMatchesDate()
        {
            var clock = new ActionWorldClock(Guid.NewGuid(), Day, 29L * Day + 870);
            Assert.That(GameTimeText.FormatClock(clock.Current), Is.EqualTo("1年1月30日 14:30"));
            Assert.That(clock.Current.Date.ToClockText(), Is.EqualTo(GameTimeText.FormatClock(clock.Current)));
            Assert.Throws<ArgumentOutOfRangeException>(() => GameTimeText.FormatDuration(-1));
        }
    }
}
