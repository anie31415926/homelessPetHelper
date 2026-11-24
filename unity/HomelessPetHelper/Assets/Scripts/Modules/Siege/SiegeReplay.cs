using System;
using System.Collections.Generic;
using System.Linq;

namespace HomelessPetHelper.Modules.Siege
{
    /// <summary>Fixed-step rule inputs, independent of frame rate, UI callbacks and transient pause state.</summary>
    public sealed class SiegeReplay
    {
        // Increment when simulation semantics change. Old histories must migrate or be rejected explicitly.
        public const int SimulationVersion = 2;
        public IReadOnlyList<Step> Steps { get; }
        public double AccumulatorMs { get; }

        public SiegeReplay(IEnumerable<Step> steps, double accumulatorMs)
        {
            Steps = Array.AsReadOnly((steps ?? throw new ArgumentNullException(nameof(steps))).ToArray());
            AccumulatorMs = accumulatorMs;
        }

        public sealed class Step
        {
            public bool Realtime { get; }
            public int Ticks { get; internal set; }
            public bool PauseAtDecision { get; }
            public bool DecisionHeld { get; }
            private readonly SiegeOrder _order;
            public SiegeOrder Order => CopyOrder(_order);

            public Step(int ticks, bool pauseAtDecision, bool decisionHeld, SiegeOrder order = null,
                bool realtime = false)
            {
                Realtime = realtime;
                Ticks = ticks;
                PauseAtDecision = pauseAtDecision;
                DecisionHeld = decisionHeld;
                _order = CopyOrder(order);
            }

            internal Step Copy() => new Step(Ticks, PauseAtDecision, DecisionHeld, _order, Realtime);
        }

        internal static SiegeOrder CopyOrder(SiegeOrder order) => order == null ? null : new SiegeOrder
        {
            SessionId = order.SessionId, Turn = order.Turn, DeployCardId = order.DeployCardId,
            DeployLaneId = order.DeployLaneId, DeployPoint = order.DeployPoint,
                MergeIds = order.MergeIds == null ? null : (int[])order.MergeIds.Clone(),
            AnchorId = order.AnchorId, RerouteUnitId = order.RerouteUnitId,
            RerouteLaneId = order.RerouteLaneId, DiscardCardId = order.DiscardCardId
        };
    }
}
