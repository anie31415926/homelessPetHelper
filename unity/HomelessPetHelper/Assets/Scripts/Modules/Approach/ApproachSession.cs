using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Rescue;

namespace HomelessPetHelper.Modules.Approach
{
    /// <summary>One fixed route objective. Mutations validate before writing; reads never advance time.</summary>
    public sealed class ApproachSession : IRouteOpeningActivity
    {
        private readonly int[] _boxes;
        private readonly double[] _remaining;
        private readonly bool[] _blocked, _reachable;
        private readonly int[] _queue;
        private readonly List<PuzzleStep> _history = new List<PuzzleStep>();
        private readonly int _undoLimit;
        private int _bridge = -1;
        public Guid SessionId { get; }
        public int Seed { get; }
        public ApproachTemplate Template { get; }
        public double MaxStepSeconds { get; }
        public bool IsComplete { get; private set; }
        public bool IsClosed { get; private set; }
        public bool IsSuspended { get; private set; }
        public bool CanUndo => _history.Count > 0 && !IsClosed && !IsSuspended;
        public int Version { get; private set; }
        public int BridgeCell => _bridge;

        public ApproachSession(Guid outingId, int seed, ApproachTemplate template, ApproachCatalog catalog)
        {
            if (outingId == Guid.Empty || seed < 0) throw new ArgumentException("Invalid approach identity.");
            Template = template ?? throw new ArgumentNullException(nameof(template));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!ReferenceEquals(catalog.Get(template.Mode), template))
                throw new ArgumentException("Template belongs to a different catalog.");
            SessionId = outingId;
            Seed = seed;
            MaxStepSeconds = catalog.MaxStepSeconds;
            _undoLimit = catalog.UndoLimit;
            _boxes = new int[template.Boxes.Count];
            for (int i = 0; i < _boxes.Length; i++) _boxes[i] = template.Boxes[i];
            _remaining = new double[template.Debris.Count];
            for (int i = 0; i < _remaining.Length; i++) _remaining[i] = template.Debris[i].WorkSeconds;
            _blocked = new bool[template.CellCount];
            _reachable = new bool[template.CellCount];
            _queue = new int[template.CellCount];
            RefreshRoute();
        }

        public bool HasBox(int cell) => Array.IndexOf(_boxes, cell) >= 0;
        public bool IsReachable(int cell) => Template.Contains(cell) && _reachable[cell];
        public double RemainingWork(int index) => _remaining[index];
        public int DebrisAt(int cell)
        {
            for (int i = 0; i < _remaining.Length; i++)
                if (_remaining[i] > 0 && Template.Debris[i].Cell == cell) return i;
            return -1;
        }

        public bool CanReachDebris(int index)
        {
            if (index < 0 || index >= _remaining.Length || _remaining[index] <= 0) return false;
            int cell = Template.Debris[index].Cell, col = cell % Template.Columns;
            return IsReachable(cell) || (col > 0 && IsReachable(cell - 1)) ||
                (col + 1 < Template.Columns && IsReachable(cell + 1)) ||
                IsReachable(cell - Template.Columns) || IsReachable(cell + Template.Columns);
        }

        /// <summary>Slides the selected crate along an unobstructed row/column, as in the HTML trial.</summary>
        public bool TrySlideBox(int from, int to, out ApproachFailure failure)
        {
            if (!Require(ApproachMode.Puzzle, out failure)) return false;
            if (!Template.Contains(from) || !Template.Contains(to)) return Reject(ApproachFailure.InvalidCell,
                out failure);
            int index = Array.IndexOf(_boxes, from), columns = Template.Columns;
            if (index < 0 || from == to || (from / columns != to / columns && from % columns != to % columns))
                return Reject(ApproachFailure.Blocked, out failure);
            int step = from / columns == to / columns ? Math.Sign(to - from) : Math.Sign(to - from) * columns;
            for (int cell = from + step; ; cell += step)
            {
                if (_blocked[cell] || cell == Template.Animal || cell == Template.Entrance)
                    return Reject(ApproachFailure.Blocked, out failure);
                if (cell == to) break;
            }
            Remember();
            _boxes[index] = to;
            Changed();
            return true;
        }

        public bool TryPlaceBridge(int cell, out ApproachFailure failure)
        {
            if (!Require(ApproachMode.Puzzle, out failure)) return false;
            if (!Template.Contains(cell)) return Reject(ApproachFailure.InvalidCell, out failure);
            bool allowed = false;
            foreach (int site in Template.BridgeCells) if (cell == site) allowed = true;
            if (!allowed || cell == _bridge) return Reject(ApproachFailure.Blocked, out failure);
            // Moving the single plank must never strand a crate on the previous crossing.
            if (_bridge >= 0 && HasBox(_bridge)) return Reject(ApproachFailure.Blocked, out failure);
            Remember();
            _bridge = cell;
            Changed();
            return true;
        }

        public bool TryUndo(out ApproachFailure failure)
        {
            if (!Require(ApproachMode.Puzzle, out failure)) return false;
            if (_history.Count == 0) return Reject(ApproachFailure.NoHistory, out failure);
            PuzzleStep previous = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            Array.Copy(previous.Boxes, _boxes, _boxes.Length);
            _bridge = previous.Bridge;
            Changed();
            return true;
        }

        /// <summary>Seconds of effective brush contact, capped by the catalog per sample. No world-time
        /// writes.</summary>
        public bool TryClean(int cell, ApproachTool tool, double seconds, out ApproachFailure failure)
        {
            if (!Require(ApproachMode.Cleanup, out failure)) return false;
            if (!Template.Contains(cell)) return Reject(ApproachFailure.InvalidCell, out failure);
            if (!ApproachCatalog.FinitePositive(seconds) || seconds > MaxStepSeconds)
                return Reject(ApproachFailure.InvalidDuration, out failure);
            int index = DebrisAt(cell);
            if (index < 0) return Reject(ApproachFailure.NoWork, out failure);
            if (Template.Debris[index].Tool != tool) return Reject(ApproachFailure.WrongTool, out failure);
            if (!CanReachDebris(index)) return Reject(ApproachFailure.OutOfReach, out failure);
            _remaining[index] = Math.Max(0, _remaining[index] - seconds);
            Changed();
            return true;
        }

        public void SetSuspended(bool suspended)
        {
            if (IsClosed || IsSuspended == suspended) return;
            IsSuspended = suspended;
            Version++;
        }

        public void Dispose()
        {
            if (IsClosed) return;
            IsClosed = true;
            _history.Clear();
            Version++;
        }

        private bool Require(ApproachMode mode, out ApproachFailure failure)
        {
            failure = IsClosed ? ApproachFailure.Closed : IsSuspended ? ApproachFailure.Suspended :
                Template.Mode != mode ? ApproachFailure.WrongMode : ApproachFailure.None;
            return failure == ApproachFailure.None;
        }

        private static bool Reject(ApproachFailure reason, out ApproachFailure failure)
        {
            failure = reason;
            return false;
        }

        private void Remember()
        {
            if (_history.Count == _undoLimit) _history.RemoveAt(0);
            _history.Add(new PuzzleStep((int[])_boxes.Clone(), _bridge));
        }

        private void Changed()
        {
            Version++;
            RefreshRoute();
        }

        private void RefreshRoute()
        {
            for (int cell = 0; cell < _blocked.Length; cell++)
                _blocked[cell] = Template.Terrain(cell) == '#' ||
                    (Template.Terrain(cell) == '~' && _bridge != cell);
            foreach (int cell in _boxes) _blocked[cell] = true;
            for (int i = 0; i < _remaining.Length; i++)
                if (_remaining[i] > 0 && Template.Debris[i].Tool != ApproachTool.Mud)
                    _blocked[Template.Debris[i].Cell] = true;
            Array.Clear(_reachable, 0, _reachable.Length);
            int read = 0, write = 0;
            Visit(Template.Entrance, ref write);
            while (read < write)
            {
                int cell = _queue[read++], col = cell % Template.Columns;
                if (col > 0) Visit(cell - 1, ref write);
                if (col + 1 < Template.Columns) Visit(cell + 1, ref write);
                Visit(cell - Template.Columns, ref write);
                Visit(cell + Template.Columns, ref write);
            }
            IsComplete = _reachable[Template.Animal];
            foreach (int cell in Template.RequiredClearCells)
                if (_blocked[cell]) { IsComplete = false; break; }
        }

        private void Visit(int cell, ref int write)
        {
            if (!Template.Contains(cell) || _reachable[cell] || _blocked[cell]) return;
            _reachable[cell] = true;
            _queue[write++] = cell;
        }

        private readonly struct PuzzleStep
        {
            public int[] Boxes { get; }
            public int Bridge { get; }
            public PuzzleStep(int[] boxes, int bridge) { Boxes = boxes; Bridge = bridge; }
        }
    }
}
