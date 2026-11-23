using System;
using System.Linq;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Siege;

namespace HomelessPetHelper.Game.Progress
{
    internal static class FieldCheckpointCodec
    {
        internal static OpeningData Capture(ApproachSession opening)
        {
            var state = opening.CaptureCheckpoint();
            return new OpeningData
            {
                Boxes = state.Boxes.ToArray(), Bridge = state.BridgeCell, Remaining = state.RemainingWork.ToArray(),
                Undo = state.Undo.Select(step => new PuzzleUndoData
                    { Boxes = step.Boxes.ToArray(), Bridge = step.BridgeCell }).ToArray()
            };
        }

        internal static ApproachCheckpoint Restore(OpeningData data)
        {
            if (data == null || data.Boxes == null || data.Remaining == null || data.Undo == null ||
                data.Undo.Any(step => step == null || step.Boxes == null))
                throw new ArgumentException("Invalid saved opening state.");
            return new ApproachCheckpoint(data.Boxes, data.Bridge, data.Remaining,
                data.Undo.Select(step => new ApproachCheckpoint(step.Boxes, step.Bridge, data.Remaining)));
        }

        internal static BattleReplayData Capture(SiegeSession battle)
        {
            var replay = battle.CaptureReplay();
            return new BattleReplayData
            {
                SimulationVersion = SiegeReplay.SimulationVersion, AccumulatorMs = replay.AccumulatorMs,
                Steps = replay.Steps.Select(step => new ReplayStepData
                {
                    Ticks = step.Ticks, PauseAtDecision = step.PauseAtDecision, DecisionHeld = step.DecisionHeld,
                    Order = CaptureOrder(step.Order)
                }).ToArray()
            };
        }

        internal static SiegeReplay Restore(BattleReplayData data)
        {
            if (data == null || data.SimulationVersion != SiegeReplay.SimulationVersion || data.Steps == null ||
                data.Steps.Any(step => step == null)) throw new ArgumentException("Incompatible battle replay.");
            return new SiegeReplay(data.Steps.Select(step => new SiegeReplay.Step(step.Ticks,
                step.PauseAtDecision, step.DecisionHeld, RestoreOrder(step.Order))), data.AccumulatorMs);
        }

        private static OrderData CaptureOrder(SiegeOrder order) => order == null ? null : new OrderData
        {
            SessionId = order.SessionId, Turn = order.Turn, DeployCardId = order.DeployCardId,
            DeployLaneId = order.DeployLaneId, MergeIds = order.MergeIds, AnchorId = order.AnchorId,
            RerouteUnitId = order.RerouteUnitId, RerouteLaneId = order.RerouteLaneId,
            DiscardCardId = order.DiscardCardId
        };

        private static SiegeOrder RestoreOrder(OrderData order) => order == null ? null : new SiegeOrder
        {
            SessionId = order.SessionId, Turn = order.Turn, DeployCardId = order.DeployCardId,
            DeployLaneId = order.DeployLaneId, MergeIds = order.MergeIds, AnchorId = order.AnchorId,
            RerouteUnitId = order.RerouteUnitId, RerouteLaneId = order.RerouteLaneId,
            DiscardCardId = order.DiscardCardId
        };
    }
}
