using System;
using System.Collections.Generic;
using System.Linq;

namespace HomelessPetHelper.Modules.Approach
{
    /// <summary>Detached rule state; template identity and content compatibility belong to the caller.</summary>
    public sealed class ApproachCheckpoint
    {
        public IReadOnlyList<int> Boxes { get; }
        public int BridgeCell { get; }
        public IReadOnlyList<double> RemainingWork { get; }
        public IReadOnlyList<ApproachCheckpoint> Undo { get; }

        public ApproachCheckpoint(IEnumerable<int> boxes, int bridgeCell, IEnumerable<double> remainingWork,
            IEnumerable<ApproachCheckpoint> undo = null)
        {
            Boxes = Array.AsReadOnly((boxes ?? throw new ArgumentNullException(nameof(boxes))).ToArray());
            BridgeCell = bridgeCell;
            RemainingWork = Array.AsReadOnly((remainingWork ??
                throw new ArgumentNullException(nameof(remainingWork))).ToArray());
            Undo = Array.AsReadOnly(undo?.ToArray() ?? Array.Empty<ApproachCheckpoint>());
        }
    }
}
