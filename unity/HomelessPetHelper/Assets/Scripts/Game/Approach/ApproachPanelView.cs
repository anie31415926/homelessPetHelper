using System;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game.UI;
using System.Collections.Generic;
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
        [SerializeField] private Image _locationBackground;
        [SerializeField] private OutingFieldView _scene;
        public OutingFieldView FieldScene => _scene;
        private bool SceneMode => _scene != null && _scene.Layout != null;

        public void SetLocationBackground(Sprite background)
        {
            if (_locationBackground == null || background == null)
                throw new InvalidOperationException("Missing location background binding.");
            _locationBackground.sprite = background;
        }

        [SerializeField] private ApproachBoardView _board, _sceneBoard;
        [SerializeField] private FieldPanelPlacement _sceneChrome;
        [SerializeField] private UiLayoutMetrics _layoutMetrics;
        [SerializeField] private RectTransform _actionDock;
        [SerializeField] private Slider _cleanupProgress;
        private int _dockStamp = -1;
        private float _dockWidth = -1;
        [SerializeField] private GameObject _sceneMenu;
        [SerializeField] private UiText _sceneMenuText, _sceneNotice;
        [SerializeField] private Button _sceneResume, _sceneReturn;
        [SerializeField] private Button _objectives;
        [SerializeField] private LevelOverlayView _overlays;
        public LevelOverlayView Overlays => _overlays;
        public RectTransform PauseTarget => (RectTransform)_pause.transform;
        public RectTransform ObjectivesTarget => (RectTransform)_objectives.transform;
        private ILevelOverlay _overlay;
        private GuideSession _guide;
        [SerializeField] private UiText _title, _instruction, _feedback, _summary, _seed;
        [SerializeField] private ApproachCommandView _move, _bridge, _litter, _weeds, _mud, _undo, _continue,
            _pause, _return;
        [SerializeField] private Color _floorColor, _wallColor, _waterColor, _bridgeColor, _boxColor,
            _litterColor, _weedsColor, _mudColor, _selectedColor, _reachableColor;
        [SerializeField] private Color _sceneSelectedColor = new Color32(147, 205, 173, 46);
        private ApproachSession _session;
        private OptionPreviewText _text;
        private Action _completed, _returnRequested;
        private Action<bool> _suspensionChanged;
        private ApproachTool _tool;
        private int _selectedBox = -1, _renderedVersion = -1;
        private bool _paused, _backgrounded, _hasFocus = true, _dog;
        private string _feedbackKey;
        private float _noticeUntil;
        public string FieldInstruction => _text.Get((SceneMode && _scene.Layout.IllustratedInterior
            ? "treehouse.instruction." : "field.instruction.") + _session.Template.Mode);
        public ApproachSession Session => _session;
        public event Action CheckpointRequested;
        public ApproachBoardView Board => SceneMode ? _sceneBoard : _board;

        public void BindOverlay(ILevelOverlay overlay, GuideSession guide)
        {
            if (this == null) return;
            Board.CancelContact();
            _selectedBox = -1;
            _overlay = overlay;
            _guide = guide;
            if (_session != null && !_session.IsClosed && isActiveAndEnabled) Render();
        }

        public bool ResolveGuideTarget(string action, List<RectTransform> targets)
        {
            if (action.StartsWith("approach.tool.", StringComparison.Ordinal))
            {
                string tool = action.Substring("approach.tool.".Length);
                var button = tool == "Litter" ? _litter : tool == "Weeds" ? _weeds : tool == "Mud" ? _mud : null;
                if (button == null || !GuideTargetRules.IsClickable(button.Button)) return false;
                targets.Add((RectTransform)button.transform);
                return true;
            }
            int separator = action.LastIndexOf('.');
            if (separator < 0 || !int.TryParse(action.Substring(separator + 1), out int cell) ||
                cell < 0 || cell >= Board.CellCount) return false;
            // A hole over a board that is closed, suspended or hidden would be the only opening in the mask.
            if (!CanAct || !Board.isActiveAndEnabled || !Board.Cell(cell).gameObject.activeInHierarchy)
                return false;
            if (action.StartsWith("approach.clean.", StringComparison.Ordinal) && _session.DebrisAt(cell) < 0)
                return false;
            if (action.StartsWith("approach.box.", StringComparison.Ordinal) && !_session.HasBox(cell))
                return false;
            if (action.StartsWith("approach.bridge.", StringComparison.Ordinal) && _session.BridgeCell == cell)
                return false;
            targets.Add((RectTransform)Board.Cell(cell).transform);
            return true;
        }

        public void SetOverlaySuspended(bool suspended)
        {
            if (this == null) return;
            if (_session == null || _session.IsClosed) return;
            Board.CancelContact();
            _suspensionChanged?.Invoke(suspended);
            Render();
        }

        private bool Allows(string action) => _guide == null || _guide.Allows(action);
        private void Succeeded(string action)
        {
            if (_guide != null && _guide.ReportSucceeded(action)) Board.CancelContact();
        }

        public void ConfigureField(OutingFieldLayout layout, Sprite background, bool dog, ApproachSession opening,
            SceneSpritePalette props = null, OutingArtwork artwork = null)
        {
            if (gameObject.activeInHierarchy) throw new InvalidOperationException("Configure a closed approach view.");
            _scene.Configure(layout, background, dog, opening, props: props, artwork: artwork);
            _board.gameObject.SetActive(layout == null);
            _sceneChrome.Apply(layout != null);
            _dockStamp = -1;
            if (_cleanupProgress != null) _cleanupProgress.gameObject.SetActive(false);
            if (layout == null && _actionDock != null)
            {
                _actionDock.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 336);
                _actionDock.anchoredPosition = new Vector2(0, 216);
            }
        }

        public void Bind(ApproachSession session, TextAsset text, bool dog, Action completed,
            Action returnRequested, Action<bool> suspensionChanged)
        {
            if (gameObject.activeInHierarchy) throw new InvalidOperationException("Bind a closed approach panel.");
            Unbind();
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Board.Configure(session.Template);
            _text = new OptionPreviewText(text);
            ValidateText(_text);
            if (Board.CellCount != session.Template.CellCount)
                throw new InvalidOperationException("Approach board/prefab version mismatch.");
            _dog = dog;
            _completed = completed ?? throw new ArgumentNullException(nameof(completed));
            _returnRequested = returnRequested ?? throw new ArgumentNullException(nameof(returnRequested));
            _suspensionChanged = suspensionChanged ?? throw new ArgumentNullException(nameof(suspensionChanged));
            _tool = session.Template.Mode == ApproachMode.Puzzle ? ApproachTool.Move : ApproachTool.Litter;
            _paused = _backgrounded = false;
            _hasFocus = true;
            _renderedVersion = -1;
            _feedbackKey = SceneMode && _scene.Layout.IllustratedInterior && !session.IsAnimalRevealed &&
                session.Template.Mode == ApproachMode.Cleanup ? "treehouse.clue" : "ready";
            _noticeUntil = 0;
        }

        public static void ValidateText(OptionPreviewText text)
        {
            foreach (string key in new[] { "undo", "continue", "return", "pause", "resume", "summary", "phase",
                "route.blocked", "route.ready", "ready", "paused", "selected", "changed", "cleaning", "opened",
                "undone", "cell.wall", "cell.water", "cell.bridge", "cell.box", "cell.entrance", "cell.covered",
                "cell.work", "animal.cat", "animal.dog", "field.menu", "field.instruction.Puzzle",
                "field.instruction.Cleanup"
                    }) text.Get(key, 0, 0);
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
            Board.CancelContact();
            _session = null;
            _completed = _returnRequested = null;
            _suspensionChanged = null;
            _selectedBox = -1;
        }

        public void ShowExternalFeedback(string text)
        {
            _feedback.text = text;
            if (SceneMode) { _sceneNotice.text = text; _noticeUntil = Time.unscaledTime + 3; ShowNotice(); }
        }

        private void OnEnable()
        {
            Board.Pressed += Press;
            Board.Released += Release;
            _move.Button.onClick.AddListener(MoveTool);
            _bridge.Button.onClick.AddListener(BridgeTool);
            _litter.Button.onClick.AddListener(LitterTool);
            _weeds.Button.onClick.AddListener(WeedsTool);
            _mud.Button.onClick.AddListener(MudTool);
            _undo.Button.onClick.AddListener(Undo);
            _continue.Button.onClick.AddListener(Continue);
            _return.Button.onClick.AddListener(Return);
            _pause.Button.onClick.AddListener(Pause);
            _objectives.onClick.AddListener(Objectives);
            _sceneResume.onClick.AddListener(Pause);
            _sceneReturn.onClick.AddListener(Return);
            Render();
        }

        private void OnDisable()
        {
            Board.Pressed -= Press;
            Board.Released -= Release;
            _move.Button.onClick.RemoveListener(MoveTool);
            _bridge.Button.onClick.RemoveListener(BridgeTool);
            _litter.Button.onClick.RemoveListener(LitterTool);
            _weeds.Button.onClick.RemoveListener(WeedsTool);
            _mud.Button.onClick.RemoveListener(MudTool);
            _undo.Button.onClick.RemoveListener(Undo);
            _continue.Button.onClick.RemoveListener(Continue);
            _return.Button.onClick.RemoveListener(Return);
            _pause.Button.onClick.RemoveListener(Pause);
            _objectives.onClick.RemoveListener(Objectives);
            _sceneResume.onClick.RemoveListener(Pause);
            _sceneReturn.onClick.RemoveListener(Return);
            Board.CancelContact();
            _selectedBox = -1;
        }

        private void Update()
        {
            if (_session == null) return;
            if (SceneMode) { ShowNotice(); RefreshSceneDock(); }
            if (_session.IsSuspended) Board.CancelContact();
            AdvanceContact(Math.Min(Time.deltaTime, _session.MaxStepSeconds));
            if (_session.Version != _renderedVersion) Render();
        }

        /// <summary>Samples only captured pointer contact; an injected duration still passes all rule gates.</summary>
        public void AdvanceContact(double seconds)
        {
            if (!CanAct || _session.Template.Mode != ApproachMode.Cleanup || !Board.IsHeld || Board.ContactCell < 0)
                return;
            int cell = Board.ContactCell;
            if (!Allows("approach.clean." + cell)) { Board.CancelContact(); return; }
            int before = _session.DebrisAt(cell);
            if (_session.TryClean(cell, _tool, seconds, out var failure))
            {
                SetFeedback("cleaning");
                if (before >= 0 && _session.DebrisAt(cell) < 0)
                { Succeeded("approach.clean." + cell); CheckpointRequested?.Invoke(); }
            }
            else SetFeedback("failure." + failure);
        }

        private bool CanAct => isActiveAndEnabled && _session != null && !_session.IsClosed && !_session.IsSuspended;
        private void Press(int cell)
        {
            if (!CanAct) { Board.CancelContact(); return; }
            if (_tool == ApproachTool.Move && _session.HasBox(cell))
            {
                if (!Allows("approach.box." + cell)) { Board.CancelContact(); return; }
                _selectedBox = cell;
                Succeeded("approach.box." + cell);
                SetFeedback("selected");
                Render();
            }
        }

        private void Release(int cell)
        {
            if (!CanAct) return;
            if (_tool == ApproachTool.Bridge || (SceneMode && _session.Template.Mode == ApproachMode.Puzzle &&
                System.Linq.Enumerable.Contains(_session.Template.BridgeCells, cell)))
            {
                if (!Allows("approach.bridge." + cell)) return;
                bool success = _session.TryPlaceBridge(cell, out var bridgeFailure);
                SetFeedback(success ? "changed" : "failure." + bridgeFailure);
                if (success) Succeeded("approach.bridge." + cell);
            }
            else if (_tool == ApproachTool.Move && _selectedBox >= 0 && cell != _selectedBox)
            {
                if (!Allows("approach.move." + cell)) return;
                bool success = _session.TrySlideBox(_selectedBox, cell, out var failure);
                if (success) _selectedBox = cell;
                if (success) Succeeded("approach.move." + cell);
                SetFeedback(success ? "changed" : "failure." + failure);
            }
            Render();
            CheckpointRequested?.Invoke();
        }

        private void MoveTool() => Tool(ApproachTool.Move);
        private void BridgeTool() => Tool(ApproachTool.Bridge);
        private void LitterTool() => Tool(ApproachTool.Litter);
        private void WeedsTool() => Tool(ApproachTool.Weeds);
        private void MudTool() => Tool(ApproachTool.Mud);
        private void Tool(ApproachTool tool)
        {
            if (!CanAct) return;
            if (!Allows("approach.tool." + tool)) return;
            if (_session.Template.Mode == ApproachMode.Puzzle ? tool > ApproachTool.Bridge : tool < ApproachTool.Litter)
                return;
            Board.CancelContact();
            _selectedBox = -1;
            _tool = tool;
            Succeeded("approach.tool." + tool);
            SetFeedback("ready");
            Render();
        }

        private void Undo()
        {
            if (!CanAct || !Allows("approach.undo")) return;
            Board.CancelContact();
            _selectedBox = -1;
            SetFeedback(_session.TryUndo(out var failure) ? "undone" : "failure." + failure);
            Render();
            CheckpointRequested?.Invoke();
        }

        private void Continue()
        {
            if (!CanAct || !_session.IsComplete || !Allows("approach.continue")) return;
            Board.CancelContact();
            _completed?.Invoke();
        }

        private void Return()
        {
            if (_overlay != null) { _overlay.RequestReturn(); return; }
            if (SceneMode && _session != null && _session.IsSuspended && !_backgrounded && _hasFocus)
            {
                _paused = false;
                _suspensionChanged?.Invoke(false);
                _returnRequested?.Invoke();
                return;
            }
            if (!CanAct) return;
            Board.CancelContact();
            _returnRequested?.Invoke();
        }

        private void Pause()
        {
            if (_overlay != null) { _overlay.OpenPause(); return; }
            if (_session == null || _session.IsClosed) return;
            _paused = !_session.IsSuspended;
            ApplySuspension();
        }

        private void OnApplicationPause(bool paused)
        {
            if (_overlay != null) { _overlay.SetApplicationPaused(paused); return; }
            _backgrounded = paused;
            if (paused) _paused = true;
            ApplySuspension();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (_overlay != null) { _overlay.SetApplicationFocus(focused); return; }
            _hasFocus = focused;
            if (!focused) _paused = true;
            ApplySuspension();
        }

        private void ApplySuspension()
        {
            if (!isActiveAndEnabled || _session == null) return;
            Board.CancelContact();
            _suspensionChanged?.Invoke(_paused || _backgrounded || !_hasFocus);
            Render();
        }

        private void SetFeedback(string key)
        {
            _feedbackKey = key;
            if (_text == null) return;
            string visibleKey = _session.IsSuspended ? "paused" : _session.IsComplete ? "opened" : key;
            if (SceneMode && _scene.Layout.IllustratedInterior && visibleKey == "opened")
                visibleKey = _session.Template.Mode == ApproachMode.Cleanup ? "treehouse.found" : "opened";
            string caption = _text.Get(visibleKey);
            if (_feedback.text != caption) _feedback.text = caption;
            if (SceneMode && _sceneNotice.text != caption)
            {
                _sceneNotice.text = caption;
                _noticeUntil = visibleKey == "ready" || visibleKey == "cleaning" ? 0 : Time.unscaledTime + 3;
                ShowNotice();
            }
        }

        private void Render()
        {
            if (_session == null || _text == null) return;
            _renderedVersion = _session.Version;
            bool puzzle = _session.Template.Mode == ApproachMode.Puzzle, enabled = CanAct;
            _title.text = _text.Get(SceneMode && _scene.Layout.IllustratedInterior && puzzle
                ? "treehouse.title.Puzzle" : "title." + _session.Template.Mode);
            _instruction.text = _text.Get("instruction." + _session.Template.Mode);
            _seed.text = _text.Get("phase");
            _summary.text = _text.Get("summary", _text.Get(_dog ? "animal.dog" : "animal.cat"),
                _text.Get(_session.IsComplete ? "route.ready" : "route.blocked"));
            SetFeedback(_feedbackKey);
            foreach (var label in new[] { _title, _instruction, _summary, _feedback, _seed })
                label.gameObject.SetActive(!SceneMode);
            _sceneMenu.SetActive(SceneMode && _overlay == null && _session.IsSuspended);
            _objectives.gameObject.SetActive(SceneMode && _overlay != null);
            if (!SceneMode) _sceneNotice.gameObject.SetActive(false);
            RenderTool(_move, ApproachTool.Move, puzzle, enabled);
            RenderTool(_bridge, ApproachTool.Bridge, puzzle, enabled);
            RenderTool(_litter, ApproachTool.Litter, !puzzle, enabled);
            RenderTool(_weeds, ApproachTool.Weeds, !puzzle, enabled);
            RenderTool(_mud, ApproachTool.Mud, !puzzle, enabled);
            _undo.gameObject.SetActive(puzzle);
            Label(_undo, "undo", enabled && _session.CanUndo && Allows("approach.undo"));
            Label(_continue, "continue", enabled && _session.IsComplete && Allows("approach.continue"));
            Label(_return, "return", enabled);
            Label(_pause, _session.IsSuspended ? "resume" : "pause", !_session.IsClosed);
            for (int cell = 0; cell < Board.CellCount; cell++) RenderCell(cell);
            if (SceneMode) RenderScene();
        }

        private void RenderScene()
        {
            _scene.SetPresentationPaused(_session.IsSuspended);
            _scene.Refresh(_session);
            _move.gameObject.SetActive(false);
            _bridge.gameObject.SetActive(false);
            bool cleanup = _session.Template.Mode == ApproachMode.Cleanup && !_session.IsSuspended &&
                (!_session.IsComplete || _scene.Layout.AllowsOptionalCleanup);
            bool indoor = _scene.Layout.IllustratedInterior;
            _litter.gameObject.SetActive(cleanup);
            _weeds.gameObject.SetActive(cleanup && !indoor);
            _mud.gameObject.SetActive(cleanup && !indoor);
            if (cleanup && indoor) _litter.Show(_text.Get("treehouse.tool"), false);
            _continue.gameObject.SetActive(_session.IsComplete && !_session.IsSuspended);
            _return.gameObject.SetActive(false);
            if (_overlay == null) Label(_pause, "field.menu", !_session.IsClosed);
            else
            {
                _pause.Show(_overlay.PauseCaption, !_session.IsClosed);
                _objectives.GetComponentInChildren<UiText>().text = _overlay.ObjectivesCaption;
            }
            _sceneMenuText.text = _title.text + "\n\n" + FieldInstruction +
                "\n\n" + _summary.text;
            _sceneResume.GetComponentInChildren<UiText>().text = _text.Get("resume");
            _sceneReturn.GetComponentInChildren<UiText>().text = _text.Get("return");
            _sceneResume.interactable = _sceneReturn.interactable = !_backgrounded && _hasFocus;
            _instruction.text = FieldInstruction;
            _seed.text = string.Empty;
            RefreshCleanupProgress();
            RefreshSceneDock();
            ShowNotice();
        }

        private void RefreshCleanupProgress()
        {
            if (_cleanupProgress == null) return;
            bool visible = _session.Template.Mode == ApproachMode.Cleanup && !_session.IsSuspended;
            _cleanupProgress.gameObject.SetActive(visible);
            if (!visible) return;
            double total = 0, remaining = 0;
            for (int i = 0; i < _session.Template.Debris.Count; i++)
            {
                total += _session.Template.Debris[i].WorkSeconds;
                remaining += _session.RemainingWork(i);
            }
            _cleanupProgress.SetValueWithoutNotify(total > 0 ? (float)(1 - remaining / total) : 1);
        }

        private void RefreshSceneDock()
        {
            if (_actionDock == null || _layoutMetrics == null) return;
            int stamp = 0;
            for (int i = 0; i < _actionDock.childCount; i++)
                if (_actionDock.GetChild(i).gameObject.activeSelf) stamp |= 1 << i;
            if (_cleanupProgress != null && _cleanupProgress.gameObject.activeSelf) stamp |= 1 << 16;
            if (stamp == _dockStamp && Mathf.Approximately(_dockWidth, _actionDock.rect.width)) return;
            _dockStamp = stamp; _dockWidth = _actionDock.rect.width;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_actionDock);
            float height = LayoutUtility.GetPreferredHeight(_actionDock);
            _actionDock.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            _actionDock.anchoredPosition = new Vector2(0, _layoutMetrics.margin + height / 2);
            float top = _layoutMetrics.margin + height + _layoutMetrics.gap;
            if (_cleanupProgress != null)
            {
                var bar = (RectTransform)_cleanupProgress.transform;
                bar.anchoredPosition = new Vector2(0, top + bar.rect.height / 2);
                if (_cleanupProgress.gameObject.activeSelf) top += bar.rect.height + _layoutMetrics.innerGap;
            }
            var notice = _sceneNotice.rectTransform;
            notice.anchorMin = new Vector2(.12f, 0);
            notice.anchorMax = new Vector2(.88f, 0);
            notice.sizeDelta = new Vector2(0, _layoutMetrics.captionHeight);
            notice.anchoredPosition = new Vector2(0, top + notice.rect.height / 2);
        }

        private void ShowNotice() => _sceneNotice.gameObject.SetActive(SceneMode && _session != null &&
            !_session.IsSuspended && Time.unscaledTime < _noticeUntil);

        private void RenderTool(ApproachCommandView button, ApproachTool tool, bool visible, bool enabled)
        {
            button.gameObject.SetActive(visible);
            bool guided = _guide != null && _guide.IsActive;
            Label(button, "tool." + tool, enabled && Allows("approach.tool." + tool) && (guided || _tool != tool));
        }

        private void Objectives() => _overlay?.OpenObjectives();

        private void Label(ApproachCommandView button, string key, bool enabled)
        {
            button.Show(_text.Get(key), enabled);
        }

        private void RenderCell(int cell)
        {
            if (SceneMode)
            {
                int active = _session.DebrisAt(cell);
                int remaining = active < 0 || Board.ContactCell != cell ? -1 :
                    (int)Math.Ceiling(100 * _session.RemainingWork(active) /
                        _session.Template.Debris[active].WorkSeconds);
                Board.Cell(cell).Show(cell == _selectedBox ? _sceneSelectedColor : Color.clear,
                    string.Empty, remaining, _text);
                return;
            }
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
            Board.Cell(cell).Show(color, caption, percent, _text);
        }
    }
}
