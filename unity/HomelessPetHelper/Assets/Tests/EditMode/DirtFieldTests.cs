using System;
using System.Linq;
using HomelessPetHelper.Modules.Station.Cleaning;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class DirtFieldTests
    {
        [Test]
        public void CopiesInputsAndNeverDirtiesForbiddenPixels()
        {
            byte[] mask = { 0, 255, 255, 255 };
            byte[] dirt = { 255, 100, 100, 100 };
            var field = new DirtField(2, 2, mask, dirt);
            Array.Clear(mask, 0, mask.Length);
            Array.Clear(dirt, 0, dirt.Length);
            Assert.That(field.InitialAmount, Is.EqualTo(300));
            Assert.That(field.Sample(0), Is.Zero);
            Assert.That(field.Sample(1), Is.EqualTo(100));
        }

        [Test]
        public void ErasureIsLocalBoundedAndIdempotentWhenEmpty()
        {
            var field = Full();
            Assert.That(field.TryErase(0, 0, 0.1f, 255), Is.True);
            Assert.That(field.Sample(0), Is.Zero);
            Assert.That(field.Sample(99), Is.EqualTo(255));
            long remaining = field.RemainingAmount;
            Assert.That(field.TryErase(0, 0, 0.1f, 255), Is.False);
            Assert.That(field.RemainingAmount, Is.EqualTo(remaining));
            Assert.That(field.RemovedFraction, Is.GreaterThan(0).And.LessThan(1));
            field.ClearResidue();
            field.ClearResidue();
            Assert.That(field.RemainingAmount, Is.Zero);
            Assert.That(field.RemovedFraction, Is.EqualTo(1));
        }

        [TestCase(float.NaN, 0.5f, 0.1f, 20)]
        [TestCase(0.5f, float.PositiveInfinity, 0.1f, 20)]
        [TestCase(-0.1f, 0.5f, 0.1f, 20)]
        [TestCase(1.1f, 0.5f, 0.1f, 20)]
        [TestCase(0.5f, 0.5f, float.NaN, 20)]
        [TestCase(0.5f, 0.5f, 0, 20)]
        [TestCase(0.5f, 0.5f, 1.1f, 20)]
        [TestCase(0.5f, 0.5f, 0.1f, 0)]
        [TestCase(0.5f, 0.5f, 0.1f, 256)]
        public void InvalidBrushHasNoSideEffects(float x, float y, float radius, int strength)
        {
            var field = Full();
            Assert.That(field.TryErase(x, y, radius, strength), Is.False);
            Assert.That(field.RemainingAmount, Is.EqualTo(field.InitialAmount));
        }

        [Test]
        public void CleanAnimalStaysClean()
        {
            var field = new DirtField(2, 2, new byte[4], new byte[4]);
            Assert.That(field.RemovedFraction, Is.EqualTo(1));
            Assert.That(field.TryErase(0.5f, 0.5f, 1, 255), Is.False);
            Assert.That(field.RemainingAmount, Is.Zero);
        }

        [TestCase(1, 2)]
        [TestCase(257, 2)]
        [TestCase(2, 0)]
        public void UnboundedDimensionsAreRejected(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DirtField(width, height, null, null));
        }

        [Test]
        public void InvalidBuffersAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new DirtField(2, 2, null, new byte[4]));
            Assert.Throws<ArgumentException>(() => new DirtField(2, 2, new byte[4], new byte[3]));
        }

        private static DirtField Full()
        {
            byte[] buffer = Enumerable.Repeat((byte)255, 100).ToArray();
            return new DirtField(10, 10, buffer, buffer);
        }
    }
}
