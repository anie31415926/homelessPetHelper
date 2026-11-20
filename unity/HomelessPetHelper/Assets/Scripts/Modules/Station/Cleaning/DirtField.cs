using System;

namespace HomelessPetHelper.Modules.Station.Cleaning
{
    /// <summary>Owned, bounded dirt samples. Coordinates use a bottom-left origin, independent of the UI.</summary>
    public sealed class DirtField : IReadOnlyDirtField
    {
        private readonly byte[] _samples;
        private readonly byte[] _allowedMask;

        public int Width { get; }
        public int Height { get; }
        public long InitialAmount { get; }
        public long RemainingAmount { get; private set; }
        public double RemovedFraction => InitialAmount == 0 ? 1 : 1 - (double)RemainingAmount / InitialAmount;

        public DirtField(int width, int height, byte[] allowedMask, byte[] dirt)
        {
            if (width < 2 || height < 2 || width > 256 || height > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (allowedMask == null || dirt == null || allowedMask.Length != width * height ||
                dirt.Length != width * height)
            {
                throw new ArgumentException("Mask and dirt must match the bounded field size.");
            }

            Width = width;
            Height = height;
            _samples = new byte[dirt.Length];
            _allowedMask = (byte[])allowedMask.Clone();
            for (int index = 0; index < dirt.Length; index++)
            {
                _samples[index] = allowedMask[index] == 0 ? (byte)0 : dirt[index];
                RemainingAmount += _samples[index];
            }

            InitialAmount = RemainingAmount;
        }

        /// <summary>Restore raw samples without resetting the original amount used for work accounting.</summary>
        public static DirtField Restore(int width, int height, byte[] mask, byte[] samples, long initialAmount)
        {
            var field = new DirtField(width, height, mask, samples);
            long maximum = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i] == 0 && samples[i] != 0) throw new ArgumentException("Samples outside coat.");
                if (mask[i] != 0) maximum += 255;
            }
            if (initialAmount < field.RemainingAmount || initialAmount > maximum)
                throw new ArgumentException("Invalid original field amount.");
            return new DirtField(field, initialAmount);
        }

        public byte[] CopyMask() => (byte[])_allowedMask.Clone();
        public byte[] CopySamples() => (byte[])_samples.Clone();

        private DirtField(DirtField source, long? initialAmount = null)
        {
            Width = source.Width;
            Height = source.Height;
            InitialAmount = initialAmount ?? source.InitialAmount;
            RemainingAmount = source.RemainingAmount;
            _samples = (byte[])source._samples.Clone();
            _allowedMask = (byte[])source._allowedMask.Clone();
        }

        internal DirtField Copy() => new DirtField(this);
        internal IReadOnlyDirtField AsReadOnly() => new ReadOnlyView(this);

        public byte Sample(int index)
        {
            return _samples[index];
        }

        /// <summary>Invalid brushes are rejected without mutation. A brush can only remove existing dirt.</summary>
        public bool TryErase(float x, float y, float radius, int strength, IReadOnlyDirtField blocker = null)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(radius) || x < 0 || x > 1 || y < 0 || y > 1 ||
                radius <= 0 || radius > 1 || strength < 1 || strength > 255)
            {
                return false;
            }

            if (blocker != null && (blocker.Width != Width || blocker.Height != Height))
            {
                return false;
            }

            int left = Math.Max(0, (int)Math.Floor((x - radius) * (Width - 1)));
            int right = Math.Min(Width - 1, (int)Math.Ceiling((x + radius) * (Width - 1)));
            int bottom = Math.Max(0, (int)Math.Floor((y - radius) * (Height - 1)));
            int top = Math.Min(Height - 1, (int)Math.Ceiling((y + radius) * (Height - 1)));
            bool changed = false;
            for (int row = bottom; row <= top; row++)
            {
                for (int column = left; column <= right; column++)
                {
                    float dx = (float)column / (Width - 1) - x;
                    float dy = (float)row / (Height - 1) - y;
                    if (dx * dx + dy * dy > radius * radius)
                    {
                        continue;
                    }

                    int index = row * Width + column;
                    if (blocker != null && blocker.Sample(index) > 0)
                    {
                        continue;
                    }

                    int removed = Math.Min(strength, _samples[index]);
                    _samples[index] -= (byte)removed;
                    RemainingAmount -= removed;
                    changed |= removed > 0;
                }
            }

            return changed;
        }

        public bool ContainsCoat(float x, float y)
        {
            if (!IsFinite(x) || !IsFinite(y) || x < 0 || x > 1 || y < 0 || y > 1)
            {
                return false;
            }

            int column = (int)Math.Round(x * (Width - 1));
            int row = (int)Math.Round(y * (Height - 1));
            return _allowedMask[row * Width + column] != 0;
        }

        public bool HasResidueAt(float x, float y)
        {
            if (!ContainsCoat(x, y)) return false;
            int column = (int)Math.Round(x * (Width - 1));
            int row = (int)Math.Round(y * (Height - 1));
            return _samples[row * Width + column] > 0;
        }

        public void ClearResidue()
        {
            Array.Clear(_samples, 0, _samples.Length);
            RemainingAmount = 0;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class ReadOnlyView : IReadOnlyDirtField
        {
            private readonly DirtField _owner;

            internal ReadOnlyView(DirtField owner) => _owner = owner;
            public int Width => _owner.Width;
            public int Height => _owner.Height;
            public long InitialAmount => _owner.InitialAmount;
            public long RemainingAmount => _owner.RemainingAmount;
            public double RemovedFraction => _owner.RemovedFraction;
            public byte Sample(int index) => _owner.Sample(index);
            public bool ContainsCoat(float x, float y) => _owner.ContainsCoat(x, y);
            public bool HasResidueAt(float x, float y) => _owner.HasResidueAt(x, y);
        }
    }
}
