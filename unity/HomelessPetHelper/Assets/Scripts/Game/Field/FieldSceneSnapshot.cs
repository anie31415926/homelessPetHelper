using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Approach;

namespace HomelessPetHelper.Game.Field
{
    /// <summary>Immutable display snapshot, captured before the opening activity is closed.</summary>
    public sealed class FieldSceneSnapshot
    {
        public ApproachTemplate Template { get; }
        public IReadOnlyList<int> Boxes { get; }
        public IReadOnlyList<double> Remaining { get; }
        public int Bridge { get; }

        public FieldSceneSnapshot(ApproachSession session)
        {
            if (session == null || session.IsClosed) throw new ArgumentException("Closed field session.");
            Template = session.Template;
            var boxes = new List<int>();
            for (int cell = 0; cell < Template.CellCount; cell++)
                if (session.HasBox(cell)) boxes.Add(cell);
            Boxes = boxes.AsReadOnly();
            var work = new double[Template.Debris.Count];
            for (int i = 0; i < work.Length; i++) work[i] = session.RemainingWork(i);
            Remaining = Array.AsReadOnly(work);
            Bridge = session.BridgeCell;
        }
    }
}
