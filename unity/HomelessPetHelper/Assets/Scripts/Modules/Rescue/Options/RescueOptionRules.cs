using System;

namespace HomelessPetHelper.Modules.Rescue.Options
{
    public sealed class RescueOptionRules
    {
        public int OfferCount { get; }

        public int UseLimit { get; }

        public int LockLimit { get; }

        public RescueOptionRules(int offerCount, int useLimit, int lockLimit)
        {
            if (offerCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offerCount));
            }

            if (useLimit <= 0 || useLimit > offerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(useLimit));
            }

            if (lockLimit < 0 || lockLimit >= offerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(lockLimit), "Keep at least one refreshable position.");
            }

            if (useLimit != 1)
            {
                throw new NotSupportedException("Multi-option turn resolution must be defined before enabling it.");
            }

            OfferCount = offerCount;
            UseLimit = useLimit;
            LockLimit = lockLimit;
        }
    }
}
