using System;
using HomelessPetHelper.Game.Field;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class WorldWeatherScheduleTests
    {
        private const int ContentSeed = 20261118;
        private const int MinutesPerDay = 1440;
        private const int ProductionWeatherBlocks = 1000;

        [Test]
        public void SameClockAndSeedProduceTheSameWeather()
        {
            var first = WorldWeatherSchedule.Sample(ContentSeed, 1234, MinutesPerDay);
            var second = WorldWeatherSchedule.Sample(ContentSeed, 1234, MinutesPerDay);
            Assert.That(first.Kind, Is.EqualTo(second.Kind));
            Assert.That(first.Intensity, Is.EqualTo(second.Intensity));
        }

        [Test]
        public void WeatherHoldsWithinABlockAndChangesPerBlockIndex()
        {
            for (long block = 0; block < 8; block++)
            {
                var expected = WorldWeatherSchedule.Sample(ContentSeed, block);
                for (int offset = 0; offset < WorldWeatherSchedule.BlockMinutes; offset += 45)
                {
                    var sampled = WorldWeatherSchedule.Sample(ContentSeed,
                        block * WorldWeatherSchedule.BlockMinutes + offset, MinutesPerDay);
                    Assert.That(sampled.Kind, Is.EqualTo(expected.Kind));
                    Assert.That(sampled.Intensity, Is.EqualTo(expected.Intensity));
                }
            }
        }

        [Test]
        public void BlockBoundaryStartsTheNextSample()
        {
            var lastMinute = WorldWeatherSchedule.Sample(ContentSeed,
                WorldWeatherSchedule.BlockMinutes - 1, MinutesPerDay);
            var boundary = WorldWeatherSchedule.Sample(ContentSeed,
                WorldWeatherSchedule.BlockMinutes, MinutesPerDay);
            Assert.That(lastMinute.Kind, Is.EqualTo(WorldWeatherSchedule.Sample(ContentSeed, 0L).Kind));
            Assert.That(boundary.Kind, Is.EqualTo(WorldWeatherSchedule.Sample(ContentSeed, 1L).Kind));
        }

        [Test]
        public void FairWeatherIsTheCommonSkyAndEveryKindAppears()
        {
            int clear = 0, rain = 0, snow = 0, fog = 0;
            for (long block = 0; block < ProductionWeatherBlocks; block++)
            {
                var weather = WorldWeatherSchedule.Sample(ContentSeed, block);
                switch (weather.Kind)
                {
                    case OutingWeatherKind.Clear: clear++; break;
                    case OutingWeatherKind.Rain: rain++; break;
                    case OutingWeatherKind.Snow: snow++; break;
                    case OutingWeatherKind.Fog: fog++; break;
                    default: throw new AssertionException("Unknown kind " + weather.Kind);
                }
                if (weather.Kind == OutingWeatherKind.Clear)
                    Assert.That(weather.Intensity, Is.EqualTo(OutingWeatherIntensity.Light),
                        "Clear weather has no strength tier.");
            }

            Assert.That(clear, Is.GreaterThan(ProductionWeatherBlocks * 4 / 10),
                "Fair weather must be the common sky, not rain.");
            Assert.That(rain, Is.GreaterThan(0), "Rain still has to appear.");
            Assert.That(snow, Is.GreaterThan(0), "Snow still has to appear.");
            Assert.That(fog, Is.GreaterThan(0), "Fog still has to appear.");
            Assert.That(clear, Is.GreaterThan(rain));
            Assert.That(clear, Is.GreaterThan(snow));
            Assert.That(clear, Is.GreaterThan(fog));
        }

        [Test]
        public void WetWeatherUsesAllThreeStrengthTiers()
        {
            bool light = false, medium = false, heavy = false;
            for (long block = 0; block < ProductionWeatherBlocks; block++)
            {
                var weather = WorldWeatherSchedule.Sample(ContentSeed, block);
                if (weather.Kind == OutingWeatherKind.Clear) continue;
                light |= weather.Intensity == OutingWeatherIntensity.Light;
                medium |= weather.Intensity == OutingWeatherIntensity.Medium;
                heavy |= weather.Intensity == OutingWeatherIntensity.Heavy;
            }

            Assert.That(light, Is.True);
            Assert.That(medium, Is.True);
            Assert.That(heavy, Is.True);
        }

        [Test]
        public void DistantTimelineStillSamplesWithoutOverflow()
        {
            long far = long.MaxValue / 4;
            var first = WorldWeatherSchedule.Sample(ContentSeed, far, MinutesPerDay);
            var second = WorldWeatherSchedule.Sample(ContentSeed, far, MinutesPerDay);
            Assert.That(first.Kind, Is.EqualTo(second.Kind));
            Assert.That(first.Intensity, Is.EqualTo(second.Intensity));
            Assert.That(first.Kind == OutingWeatherKind.Clear || first.Kind == OutingWeatherKind.Rain ||
                first.Kind == OutingWeatherKind.Snow || first.Kind == OutingWeatherKind.Fog, Is.True);
        }

        [Test]
        public void InvalidClockInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WorldWeatherSchedule.Sample(ContentSeed, -1, MinutesPerDay));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WorldWeatherSchedule.Sample(ContentSeed, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WorldWeatherSchedule.Sample(ContentSeed, -1L));
        }
    }
}
