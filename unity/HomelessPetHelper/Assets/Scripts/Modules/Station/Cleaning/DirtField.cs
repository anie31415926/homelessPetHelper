using System;

namespace HomelessPetHelper.Modules.Station.Cleaning
{
    /// <summary>Owned, bounded dirt samples. Coordinates use a bottom-left origin, independent of the UI.</summary>
    public sealed class DirtField
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

        public byte Sample(int index)
        {
            return _samples[index];
        }

        /// <summary>Invalid brushes are rejected without mutation. A brush can only remove existing dirt.</summary>
        public bool TryErase(float x, float y, float radius, int strength, DirtField blocker = null)
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
    }
}
