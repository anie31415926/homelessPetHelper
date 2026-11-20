using System;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Approach;
using UnityEngine;
using UnityEngine.UI;
using UiText = UnityEngine.UI.Text;

namespace HomelessPetHelper.Game.Approach
{
    /// <summary>Static whiteboard, explicit callbacks and one rule owner. No scene searches or completion
    /// bypass.</summary>
    public sealed class ApproachPanelView : MonoBehaviour
    {
        [SerializeField] private ApproachBoardView _board;
        [SerializeField] private UiText _title, _instruction, _feedback, _summary, _seed;
        [SerializeField] private ApproachCommandView _move, _bridge, _litter, _weeds, _mud, _undo, _continue,
            _pause, _return;
        [SerializeField] private Color _floorColor, _wallColor, _waterColor, _bridgeColor, _boxColor,
            _litterColor, _weedsColor, _mudColor, _selectedColor, _reachableColor;
        private ApproachSession _session;
        private OptionPreviewText _text;
        private Action _completed, _returnRequested;
        private Action<bool> _suspensionChanged;
        private ApproachTool _tool;
        private int _selectedBox = -1, _renderedVersion = -1;
        private bool _paused, _backgrounded, _hasFocus = true, _dog;
        private string _feedbackKey;
        public ApproachSession Session => _session;
        public ApproachBoardView Board => _board;

        public void Bind(ApproachSession session, TextAsset text, bool dog, Action completed,
            Action returnRequested, Action<bool> suspensionChanged)
        {
            if (gameObject.activeInHierarchy) throw new InvalidOperationException("Bind a closed approach panel.");
            Unbind();
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _text = new OptionPreviewText(text);
            ValidateText(_text);
            if (_board.CellCount != session.Template.CellCount)
                throw new InvalidOperationException("Approach board/prefab version mismatch.");
            _dog = dog;
            _completed = completed ?? throw new ArgumentNullException(nameof(completed));
            _returnRequested = returnRequested ?? throw new ArgumentNullException(nameof(returnRequested));
            _suspensionChanged = suspensionChanged ?? throw new ArgumentNullException(nameof(suspensionChanged));
            _tool = session.Template.Mode == ApproachMode.Puzzle ? ApproachTool.Move : ApproachTool.Litter;
            _paused = _backgrounded = false;
            _hasFocus = true;
            _renderedVersion = -1;
            _feedbackKey = "ready";
        }

        public static void ValidateText(OptionPreviewText text)
        {
            foreach (string key in new[] { "undo", "continue", "return", "pause", "resume", "summary", "phase",
                "route.blocked", "route.ready", "ready", "paused", "selected", "changed", "cleaning", "opened",
                "undone", "cell.wall", "cell.water", "cell.bridge", "cell.box", "cell.entrance", "cell.covered",
                "cell.work", "animal.cat", "animal.dog" }) text.Get(key, 0, 0);
            foreach (ApproachMode mode in Enum.GetValues(typeof(ApproachMode)))
            { text.Get("title." + mode); text.Get("instruction." + mode); }
            foreach (ApproachTool tool in Enum.GetValues(typeof(ApproachTool))) text.Get("tool." + tool);
            foreach (var tool in new[] { ApproachTool.Litter, ApproachTool.Weeds,
                ApproachTool.Mud }) text.Get("cell." + tool);
            foreach (ApproachFailure failure in Enum.GetValues(typeof(ApproachFailure)))
                if (failure != ApproachFailure.None) text.Get("failure." + failure);
        }

        public void Unbind()
        {
            _board.CancelContact();
            _session = null;
            _completed = _returnRequested = null;
            _suspensionChanged = null;
            _selectedBox = -1;
        }

        public void ShowExternalFeedback(string text) => _feedback.text = text;

        private void OnEnable()
        {
            _board.Pressed += Press;
            _board.Released += Release;
            _move.Button.onClick.AddListener(MoveTool);
            _bridge.Button.onClick.AddListener(BridgeTool);
            _litter.Button.onClick.AddListener(LitterTool);
            _weeds.Button.onClick.AddListener(WeedsTool);
            _mud.Button.onClick.AddListener(MudTool);
            _undo.Button.onClick.AddListener(Undo);
            _continue.Button.onClick.AddListener(Continue);
            _return.Button.onClick.AddListener(Return);
            _pause.Button.onClick.AddListener(Pause);
            Render();
        }

        private void OnDisable()
        {
            _board.Pressed -= Press;
            _board.Released -= Release;
            _move.Button.onClick.RemoveListener(MoveTool);
            _bridge.Button.onClick.RemoveListener(BridgeTool);
            _litter.Button.onClick.RemoveListener(LitterTool);
            _weeds.Button.onClick.RemoveListener(WeedsTool);
            _mud.Button.onClick.RemoveListener(MudTool);
            _undo.Button.onClick.RemoveListener(Undo);
            _continue.Button.onClick.RemoveListener(Continue);
            _return.Button.onClick.RemoveListener(Return);
            _pause.Button.onClick.RemoveListener(Pause);
            _board.CancelContact();
            _selectedBox = -1;
        }

        private void Update()
        {
            if (_session == null) return;
            if (_session.IsSuspended) _board.CancelContact();
            AdvanceContact(Math.Min(Time.deltaTime, _session.MaxStepSeconds));
            if (_session.Version != _renderedVersion) Render();
        }

        /// <summary>Samples only captured pointer contact; an injected duration still passes all rule gates.</summary>
        public void AdvanceContact(double seconds)
        {
            if (!CanAct || _session.Template.Mode != ApproachMode.Cleanup || !_board.IsHeld || _board.ContactCell < 0)
                return;
            if (_session.TryClean(_board.ContactCell, _tool, seconds, out var failure)) SetFeedback("cleaning");
            else SetFeedback("failure." + failure);
        }

        private bool CanAct => isActiveAndEnabled && _session != null && !_session.IsClosed && !_session.IsSuspended;
        private void Press(int cell)
        {
            if (!CanAct) { _board.CancelContact(); return; }
            if (_tool == ApproachTool.Move && _session.HasBox(cell))
            {
                _selectedBox = cell;
                SetFeedback("selected");
                Render();
            }
        }

        private void Release(int cell)
        {
            if (!CanAct) return;
            if (_tool == ApproachTool.Move && _selectedBox >= 0 && cell != _selectedBox)
            {
                bool success = _session.TrySlideBox(_selectedBox, cell, out var failure);
                if (success) _selectedBox = cell;
                SetFeedback(success ? "changed" : "failure." + failure);
            }
            else if (_tool == ApproachTool.Bridge)
                SetFeedback(_session.TryPlaceBridge(cell, out var failure) ? "changed" : "failure." + failure);
            Render();
        }

        private void MoveTool() => Tool(ApproachTool.Move);
        private void BridgeTool() => Tool(ApproachTool.Bridge);
        private void LitterTool() => Tool(ApproachTool.Litter);
        private void WeedsTool() => Tool(ApproachTool.Weeds);
        private void MudTool() => Tool(ApproachTool.Mud);
        private void Tool(ApproachTool tool)
        {
            if (!CanAct) return;
            if (_session.Template.Mode == ApproachMode.Puzzle ? tool > ApproachTool.Bridge : tool < ApproachTool.Litter)
                return;
            _board.CancelContact();
            _selectedBox = -1;
            _tool = tool;
            SetFeedback("ready");
            Render();
        }

        private void Undo()
        {
            if (!CanAct) return;
            _board.CancelContact();
            _selectedBox = -1;
            SetFeedback(_session.TryUndo(out var failure) ? "undone" : "failure." + failure);
            Render();
        }

        private void Continue()
        {
            if (!CanAct || !_session.IsComplete) return;
            _board.CancelContact();
            _completed?.Invoke();
        }

        private void Return()
        {
            if (!CanAct) return;
            _board.CancelContact();
            _returnRequested?.Invoke();
        }

        private void Pause()
        {
            if (_session == null || _session.IsClosed) return;
            _paused = !_session.IsSuspended;
            ApplySuspension();
        }

        private void OnApplicationPause(bool paused)
        {
            _backgrounded = paused;
            if (paused) _paused = true;
            ApplySuspension();
        }

        private void OnApplicationFocus(bool focused)
        {
            _hasFocus = focused;
            if (!focused) _paused = true;
            ApplySuspension();
        }

        private void ApplySuspension()
        {
            if (!isActiveAndEnabled || _session == null) return;
            _board.CancelContact();
            _suspensionChanged?.Invoke(_paused || _backgrounded || !_hasFocus);
            Render();
        }

        private void SetFeedback(string key)
        {
            _feedbackKey = key;
            if (_text == null) return;
            string visibleKey = _session.IsSuspended ? "paused" : _session.IsComplete ? "opened" : key;
            string caption = _text.Get(visibleKey);
            if (_feedback.text != caption) _feedback.text = caption;
        }

        private void Render()
        {
            if (_session == null || _text == null) return;
            _renderedVersion = _session.Version;
            bool puzzle = _session.Template.Mode == ApproachMode.Puzzle, enabled = CanAct;
            _title.text = _text.Get("title." + _session.Template.Mode);
            _instruction.text = _text.Get("instruction." + _session.Template.Mode);
            _seed.text = _text.Get("phase");
            _summary.text = _text.Get("summary", _text.Get(_dog ? "animal.dog" : "animal.cat"),
                _text.Get(_session.IsComplete ? "route.ready" : "route.blocked"));
            SetFeedback(_feedbackKey);
            RenderTool(_move, ApproachTool.Move, puzzle, enabled);
            RenderTool(_bridge, ApproachTool.Bridge, puzzle, enabled);
            RenderTool(_litter, ApproachTool.Litter, !puzzle, enabled);
            RenderTool(_weeds, ApproachTool.Weeds, !puzzle, enabled);
            RenderTool(_mud, ApproachTool.Mud, !puzzle, enabled);
            _undo.gameObject.SetActive(puzzle);
            Label(_undo, "undo", enabled && _session.CanUndo);
            Label(_continue, "continue", enabled && _session.IsComplete);
            Label(_return, "return", enabled);
            Label(_pause, _session.IsSuspended ? "resume" : "pause", !_session.IsClosed);
            for (int cell = 0; cell < _board.CellCount; cell++) RenderCell(cell);
        }

        private void RenderTool(ApproachCommandView button, ApproachTool tool, bool visible, bool enabled)
        {
            button.gameObject.SetActive(visible);
            Label(button, "tool." + tool, enabled && _tool != tool);
        }

        private void Label(ApproachCommandView button, string key, bool enabled)
        {
            button.Show(_text.Get(key), enabled);
        }

        private void RenderCell(int cell)
        {
            char terrain = _session.Template.Terrain(cell);
            Color color = terrain == '#' ? _wallColor : terrain == '~' ? _waterColor :
                _session.IsReachable(cell) ? _reachableColor : _floorColor;
            string caption = terrain == '#' ? _text.Get("cell.wall") : terrain == '~' ? _text.Get("cell.water") :
                string.Empty;
            int percent = -1;
            if (_session.BridgeCell == cell) { color = _bridgeColor; caption = _text.Get("cell.bridge"); }
            if (cell == _session.Template.Animal) caption = _text.Get(_dog ? "animal.dog" : "animal.cat");
            if (cell == _session.Template.Entrance) caption = _text.Get("cell.entrance");
            if (_session.HasBox(cell)) { color = _boxColor; caption = _text.Get("cell.box"); }
            int debris = _session.DebrisAt(cell);
            if (debris >= 0)
            {
                var item = _session.Template.Debris[debris];
                color = item.Tool == ApproachTool.Litter ? _litterColor :
                    item.Tool == ApproachTool.Weeds ? _weedsColor : _mudColor;
                string kind = _text.Get("cell." + item.Tool);
                caption = cell == _session.Template.Animal ? _text.Get("cell.covered", caption, kind) : kind;
                percent = (int)Math.Ceiling(100 * _session.RemainingWork(debris) / item.WorkSeconds);
            }
            if (cell == _selectedBox) color = _selectedColor;
            _board.Cell(cell).Show(color, caption, percent, _text);
        }
    }
}
