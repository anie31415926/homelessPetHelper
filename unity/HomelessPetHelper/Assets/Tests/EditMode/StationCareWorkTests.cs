using System;
using System.Linq;
using HomelessPetHelper.Editor.StationPreview;
using HomelessPetHelper.Modules.Station.Cleaning;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class StationCareWorkTests
    {
        [Test]
        public void SplittingIntoTinySessionsConservesTotalTimeAndNeverChargesTwice()
        {
            var field = Full(255);
            var meter = new CareWorkMeter(field, 20);
            long total = 0;
            for (int step = 0; step < 255; step++)
            {
                field.TryErase(0.5f, 0.5f, 1, 1);
                total += meter.PendingMinutes;
                meter.ConfirmSettlement();
                Assert.That(meter.PendingMinutes, Is.Zero);
            }

            Assert.That(total, Is.EqualTo(20));
            Assert.That(field.RemainingAmount, Is.Zero);
            var once = Full(255);
            var onceMeter = new CareWorkMeter(once, 20);
            once.ClearResidue();
            Assert.That(onceMeter.PendingMinutes, Is.EqualTo(total));
        }

        [Test]
        public void CompletionThresholdIsIndependentPerTool()
        {
            var wash = Full(100);
            var groom = Full(100);
            var washTime = new CareWorkMeter(wash, 20, 97);
            var groomTime = new CareWorkMeter(groom, 12, 100);
            wash.TryErase(0.5f, 0.5f, 1, 97);
            groom.TryErase(0.5f, 0.5f, 1, 97);
            Assert.That(washTime.PendingMinutes, Is.EqualTo(20));
            Assert.That(groomTime.PendingMinutes, Is.EqualTo(11));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CareWorkMeter(wash, 20, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CareWorkMeter(wash, 20, 101));
        }

        [Test]
        public void UnconfirmedWorkRemainsPendingAndCleanNoopCostsNothing()
        {
            var field = Full(255);
            var meter = new CareWorkMeter(field, 12);
            field.TryErase(0, 0, 0.1f, 255);
            Assert.That(meter.PendingMinutes, Is.EqualTo(3));
            Assert.That(meter.PendingMinutes, Is.EqualTo(3));
            meter.ConfirmSettlement();
            Assert.That(meter.PendingMinutes, Is.Zero);
            var clean = new CareWorkMeter(Full(0), 20);
            Assert.That(clean.PendingMinutes, Is.Zero);
        }

        [Test]
        public void GroomingOnlyChangesExposedCoatAndNeverChangesDirt()
        {
            var dirt = Full(255);
            var fur = Full(200);
            Assert.That(fur.TryErase(0.5f, 0.5f, 1, 255, dirt), Is.False);
            Assert.That(fur.RemainingAmount, Is.EqualTo(800));
            dirt.TryErase(0, 0, 0.1f, 255);
            Assert.That(dirt.HasResidueAt(0, 0), Is.False);
            Assert.That(dirt.HasResidueAt(1, 1), Is.True);
            Assert.That(dirt.HasResidueAt(float.NaN, 0), Is.False);
            Assert.That(fur.TryErase(0.5f, 0.5f, 1, 255, dirt), Is.True);
            Assert.That(fur.Sample(0), Is.Zero);
            Assert.That(fur.Sample(1), Is.EqualTo(200));
            Assert.That(dirt.RemainingAmount, Is.EqualTo(765));
            dirt.ClearResidue();
            Assert.That(fur.RemainingAmount, Is.EqualTo(600));
            Assert.That(fur.TryErase(0.5f, 0.5f, 1, 255, dirt), Is.True);
            Assert.That(fur.RemainingAmount, Is.Zero);
        }

        [Test]
        public void InvalidBlockerAndOutsideCoatHaveNoEffects()
        {
            var field = Full(255);
            var blocker = new DirtField(3, 3, new byte[9], new byte[9]);
            Assert.That(field.TryErase(0, 0, 1, 255, blocker), Is.False);
            Assert.That(field.RemainingAmount, Is.EqualTo(1020));
            Assert.That(field.ContainsCoat(float.NaN, 0), Is.False);
            Assert.That(field.ContainsCoat(2, 0), Is.False);
            var emptyMask = new DirtField(2, 2, new byte[4], new byte[4]);
            Assert.That(emptyMask.ContainsCoat(0.5f, 0.5f), Is.False);
        }

        [Test]
        public void QuietSpaceAndExplicitGameTimeRemovePressureButNeverForceTouch()
        {
            var sample = new StationContactSample(true, false, false, false);
            for (int index = 0; index < 20; index++) Assert.That(sample.Pet(), Is.EqualTo("petSpace"));
            sample.ObserveSettledTime(1000);
            Assert.That(sample.NeedsSpace, Is.True);
            sample.ArrangeQuietSpace(1000);
            sample.ObserveSettledTime(1009);
            Assert.That(sample.NeedsSpace, Is.True);
            sample.ArrangeQuietSpace(1009);
            sample.ObserveSettledTime(1010);
            Assert.That(sample.SignalKey, Is.EqualTo("contactQuiet"));
            Assert.That(sample.Pet(), Is.EqualTo("petQuiet"));
            Assert.That(sample.CanCare, Is.False);
            Assert.That(sample.HasAcceptedPet, Is.False);
        }

        [Test]
        public void AcceptedHeadTouchNeverOverridesIndependentCoatRestriction()
        {
            var sample = new StationContactSample(false, true, true, true);
            Assert.That(sample.Pet(), Is.EqualTo("petRestricted"));
            Assert.That(sample.HasAcceptedPet, Is.True);
            Assert.That(sample.CoatRestricted, Is.True);
            Assert.That(sample.CanCare, Is.False);
            sample.ArrangeQuietSpace(0);
            sample.ObserveSettledTime(100);
            Assert.That(sample.CanCare, Is.False);
        }

        [Test]
        public void PettingWillingnessIsNotCoatCarePermission()
        {
            var sample = new StationContactSample(false, true, false, false);
            Assert.That(sample.Pet(), Is.EqualTo("petted"));
            Assert.That(sample.HasAcceptedPet, Is.True);
            Assert.That(sample.CanCare, Is.False);
        }

        [TestCase(0)]
        [TestCase(1441)]
        public void InvalidDurationIsRejected(int duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CareWorkMeter(Full(255), duration));
        }

        private static DirtField Full(byte amount)
        {
            return new DirtField(2, 2, Enumerable.Repeat((byte)255, 4).ToArray(),
                Enumerable.Repeat(amount, 4).ToArray());
        }
    }
}
