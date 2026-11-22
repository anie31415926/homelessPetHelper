using System;
using System.Linq;
using System.Collections.Generic;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Siege.UI
{
    /// <summary>Scene input adapter for an existing battle. Owns UI intent, never battle rules.</summary>
    public sealed class SiegeFieldView : MonoBehaviour
    {
        [SerializeField] private Text _emptyHand;
        [SerializeField] private GameObject _routeRow;
        private int _contextStamp = -1;
        [SerializeField] private SiegeBattlefield _field;
        [SerializeField] private SiegeChoiceCell _cellPrefab;
        [SerializeField] private RectTransform _hand, _context, _dragGhost;
        [SerializeField] private SiegePropIcon _dragIcon;
        [SerializeField]
        private Button _top, _mid, _bottom, _submit, _pause, _retry, _resume,
            _merge, _reroute, _clear, _discard;
        [SerializeField] private GameObject _pauseMenu;
        [SerializeField] private Text _status, _intent, _hint, _detail, _pauseInfo;
        [SerializeField] private Slider _playerBar, _animalBar;
        [SerializeField] private Button _objectives;
        [SerializeField] private MotionScope _motion;
        [SerializeField] private SiegeHandLayout _dock;
        [SerializeField] private Text _resource;
        private ILevelOverlay _overlay;
        private GuideSession _guide;
        public RectTransform PauseTarget => (RectTransform)_pause.transform;
        public RectTransform ObjectivesTarget => (RectTransform)_objectives.transform;
        private readonly SiegeFieldPlan _plan = new SiegeFieldPlan();
        private SiegeSession _session;
        private SiegeCatalog _catalog;
        private SiegeText _text;
        private SiegeCellPool _handPool;
        private Action _completed, _returnRequested;
        private Action<bool> _suspend;
        private bool _reported, _paused, _focused = true, _backgrounded;
        private string _error, _tower, _externalFeedback;
        private int _turn = -1;
        private int? _dragCard;
        private string _previewLane;
        private float _refreshIn;
        private SiegeState.BattlePhase _renderedPhase;
        private HorizontalLayoutGroup _handLayout;
        private int _handStamp;
        private float _handWidth = -1;
        public SiegeFieldPlan Plan => _plan;
        public SiegeBattlefield Field => _field;

        public void BindOverlay(ILevelOverlay overlay, GuideSession guide)
        {
            if (this == null) return;
            CancelHandInput();
            _overlay = overlay;
            _guide = guide;
            if (guide != null && guide.IsActive) _plan.Clear();
            if (isActiveAndEnabled) Render();
        }

        public bool ResolveGuideTarget(string action, List<RectTransform> targets)
        {
            if (!CanAct || _handPool == null) return false;
            if (action == "siege.card")
            {
                var state = _session.Snapshot;
                int before = targets.Count;
                for (int index = 0; index < state.Hand.Count; index++)
                    if (_catalog.Unit(state.Hand[index].DefinitionId).Cost <= state.Energy)
                        targets.Add((RectTransform)_handPool.Get(index).transform);
                return targets.Count > before;
            }
            if (action == "siege.route")
            {
                if (!_top.gameObject.activeInHierarchy) return false;
                foreach (var button in new[] { _top, _mid, _bottom })
                    targets.Add((RectTransform)button.transform);
                return true;
            }
            if (action != "siege.submit") return false;
            targets.Add((RectTransform)_submit.transform);
            return true;
        }

        public void SetOverlaySuspended(bool suspended)
        {
            if (this == null) return;
            CancelHandInput();
            _paused = suspended;
            _suspend?.Invoke(suspended);
            if (_overlay != null) _pauseMenu.SetActive(false);
            if (isActiveAndEnabled) Render();
        }

        private bool Allows(string action) => _guide == null || _guide.Allows(action);

        public void Configure(Sprite animal, float width, Vector2[] portals)
        {
            var buttons = new[] { _top, _mid, _bottom };
            int rank = 0;
            foreach (int index in Enumerable.Range(0, portals.Length).OrderBy(index => portals[index].x)
                .ThenBy(index => portals[index].y))
                buttons[index].transform.SetSiblingIndex(rank++);
            _field.ConfigureScene(animal, width);
        }

        public void Bind(SiegeSession session, TextAsset text, Action completed, Action returnRequested,
            Action<bool> suspend)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _catalog = session.Catalog;
            _text = new SiegeText(text);
            _text.Validate(_catalog);
            foreach (string key in new[] { "field.status", "field.intent", "field.plan", "field.deploy",
                "field.ready", "unity.resume", "field.pauseInfo", "field.reroute", "field.cancel",
                    "field.discard", "field.materials", "field.menu", "field.help" })
                _text.Get(key);
            _completed = completed; _returnRequested = returnRequested; _suspend = suspend;
            _plan.Clear(); _reported = _paused = _backgrounded = false; _focused = true;
            _error = _tower = _externalFeedback = null; _turn = -1;
        }

        private void OnEnable()
        {
            if (_session == null) return;
            _handLayout = _hand.GetComponent<HorizontalLayoutGroup>();
            if (_dock != null) _dock.SetCapacity(_catalog.Rules.HandSize);
            _handWidth = -1; _handStamp = 0;
            _handPool = new SiegeCellPool(_cellPrefab, _hand, _catalog.Rules.HandSize);
            _top.onClick.AddListener(Top); _mid.onClick.AddListener(Mid); _bottom.onClick.AddListener(Bottom);
            _submit.onClick.AddListener(Submit); _pause.onClick.AddListener(Pause);
            _resume.onClick.AddListener(Pause); _retry.onClick.AddListener(Return);
            _merge.onClick.AddListener(Merge); _reroute.onClick.AddListener(Reroute);
            _clear.onClick.AddListener(Clear); _discard.onClick.AddListener(Discard);
            _objectives.onClick.AddListener(Objectives);
            _field.UnitClicked += SelectUnit; _field.TowerClicked += SelectTower; _field.EmptyClicked += Empty;
            _pauseMenu.SetActive(false); _dragGhost.gameObject.SetActive(false);
            Render();
        }

        private void OnDisable()
        {
            _top.onClick.RemoveListener(Top); _mid.onClick.RemoveListener(Mid); _bottom.onClick.RemoveListener(Bottom);
            _submit.onClick.RemoveListener(Submit); _pause.onClick.RemoveListener(Pause);
            _resume.onClick.RemoveListener(Pause); _retry.onClick.RemoveListener(Return);
            _merge.onClick.RemoveListener(Merge); _reroute.onClick.RemoveListener(Reroute);
            _clear.onClick.RemoveListener(Clear); _discard.onClick.RemoveListener(Discard);
            _objectives.onClick.RemoveListener(Objectives);
            _field.UnitClicked -= SelectUnit; _field.TowerClicked -= SelectTower; _field.EmptyClicked -= Empty;
            _handPool?.Dispose(); _handPool = null;
            _dragGhost.gameObject.SetActive(false); _plan.Clear();
        }

        public void Unbind()
        {
            CancelHandInput();
            _session = null; _completed = _returnRequested = null; _suspend = null;
            _plan.Clear(); _externalFeedback = null;
        }

        private bool CanAct => _session != null && _session.Phase == SiegeState.BattlePhase.Decision &&
            !_session.Paused && !_paused;

        private void Update()
        {
            if (_session == null) return;
            _session.Advance(Math.Min(100, Time.unscaledDeltaTime * 1000));
            if (!_reported && !_paused && _session.Phase == SiegeState.BattlePhase.Won)
            { _reported = true; _completed?.Invoke(); }
            _refreshIn -= Time.unscaledDeltaTime;
            if (_session != null && (_refreshIn <= 0 || _session.Phase != _renderedPhase))
            {
                _refreshIn = .05f;
                Render();
            }
        }

        public void SetApplicationFocus(bool focused)
        {
            if (_overlay != null) { _overlay.SetApplicationFocus(focused); return; }
            _focused = focused;
            if (!focused) SetPaused(true);
            else if (_session != null) Render();
        }
        public void SetApplicationPaused(bool paused)
        {
            if (_overlay != null) { _overlay.SetApplicationPaused(paused); return; }
            _backgrounded = paused;
            if (paused) SetPaused(true);
            else if (_session != null) Render();
        }
        private void Pause()
        { if (_overlay != null) _overlay.OpenPause(); else SetPaused(!_paused); }
        private void Objectives() => _overlay?.OpenObjectives();
        private void SetPaused(bool paused)
        {
            _paused = paused || !_focused || _backgrounded;
            CancelHandInput();
            _suspend?.Invoke(_paused);
            _pauseMenu.SetActive(_paused);
            if (_session != null) Render();
        }
        private void Return()
        {
            if (_overlay != null) { _overlay.RequestReturn(); return; }
            if (!_focused || _backgrounded || _session == null) return;
            CancelHandInput();
            _suspend?.Invoke(false); _paused = false;
            _pauseMenu.SetActive(false);
            _returnRequested?.Invoke();
            if (_session != null && isActiveAndEnabled) Render();
        }
        private void Top() => Route(0);
        private void Mid() => Route(1);
        private void Bottom() => Route(2);
        private void Route(int lane)
        {
            if (!CanAct || !Allows("siege.route") || !_plan.ShowsEntrances) return;
            _plan.ChooseLane(_catalog.Lanes[lane].Id); _error = null; Render();
            _guide?.ReportSucceeded("siege.route");
            Render();
        }
        private void SelectCard(int id)
        {
            if (!CanAct || !Allows("siege.card") || !_session.Snapshot.Hand.Any(card => card.Id == id)) return;
            var state = _session.Snapshot;
            if (_guide != null && _guide.IsActive &&
                _catalog.Unit(state.Hand.First(card => card.Id == id).DefinitionId).Cost > state.Energy) return;
            _plan.SelectCard(id); _tower = _error = null; Render();
            _guide?.ReportSucceeded("siege.card");
            Render();
        }
        private void SelectUnit(int id)
        {
            if (!CanAct || !Allows("siege.unit")) return;
            _plan.Focus(id, _catalog, _session.Snapshot); _tower = _error = null; Render();
        }
        private void SelectTower(string id)
        { if (!CanAct || !Allows("siege.tower")) return; _tower = id; _plan.CancelSelection(); Render(); }
        private void Empty()
        { if (!CanAct || !Allows("siege.clear")) return; _plan.CancelSelection(); _tower = null; Render(); }
        private void Merge()
        {
            if (!CanAct || !Allows("siege.merge")) return;
            _plan.BeginMerge(_catalog, _session.Snapshot);
            _error = null;
            Render();
        }
        private void Reroute()
        { if (CanAct && Allows("siege.reroute")) { _plan.BeginReroute(_session.Snapshot); _error = null; Render(); } }
        private void Clear()
        {
            if (!CanAct || !Allows("siege.clear")) return;
            CancelHandInput();
            _plan.Clear(); _error = _tower = null; Render();
        }
        private void Submit()
        {
            if (_session == null) return;
            if (_session.Phase == SiegeState.BattlePhase.Won || _session.Phase == SiegeState.BattlePhase.Timeout)
            { Return(); return; }
            if (!CanAct || !Allows("siege.submit")) return;
            bool hasDeployment = _plan.DeployCard.HasValue && _plan.DeployLane != null;
            if (_session.TryOrder(_plan.Order(_session.Snapshot), true, out _error, out _))
            {
                _plan.Clear();
                if (hasDeployment) _guide?.ReportSucceeded("siege.submit");
            }
            Render();
        }
        private void Discard()
        {
            if (!CanAct || !_plan.SelectedCard.HasValue || !Allows("siege.discard")) return;
            var state = _session.Snapshot;
            var order = new SiegeOrder
            {
                SessionId = state.SessionId,
                Turn = state.Turn,
                DiscardCardId = _plan.SelectedCard
            };
            if (_session.TryOrder(order, true, out _error, out _)) _plan.Clear();
            Render();
        }
        public void ShowExternalFeedback(string feedback)
        {
            _externalFeedback = feedback; if (_session != null)
                Render();
        }

        private void Render()
        {
            if (_session == null || _handPool == null) return;
            var state = _session.Snapshot;
            if (_motion != null) _motion.SetScenePaused(state.Paused || _paused);
            _renderedPhase = state.Phase;
            if (_turn != state.Turn) { _turn = state.Turn; _plan.Clear(); }
            bool terminal = state.Phase == SiegeState.BattlePhase.Won || state.Phase == SiegeState.BattlePhase.Timeout;
            bool valid = _session.TryOrder(_plan.Order(state), false, out string reason, out int cost);
            _status.text = _text.Format("field.compactStatus", state.Progress, _catalog.Rules.Target,
                Math.Ceiling((_catalog.Rules.DurationMs - state.ElapsedMs) / 1000.0), state.Energy);
            if (_resource != null) _resource.text = _text.Format("field.resource", state.Energy);
            var encounter = _catalog.Content.Encounter(state.EncounterId);
            var animal = state.AnimalState;
            string intentKey = animal.Intent == SiegeState.IntentKind.Guard ? encounter.GuardNameKey :
                animal.Intent == SiegeState.IntentKind.Sweep ? encounter.SweepNameKey :
                animal.Intent == SiegeState.IntentKind.Hold ? encounter.HoldNameKey :
                animal.Intent == SiegeState.IntentKind.Carry ? encounter.CarryNameKey : "unity." +
                    animal.Intent.ToString().ToLowerInvariant();
            _intent.text = _text.Format("field.intent", _text.Get(intentKey));
            _playerBar.SetValueWithoutNotify((float)state.PlayerChargeMs / _catalog.Rules.PlayerPeriodMs);
            _animalBar.SetValueWithoutNotify((float)animal.Charge);
            _hint.text = _externalFeedback ?? (_error != null ? _text.Get(_error) :
                terminal ? _text.Get(state.Phase == SiegeState.BattlePhase.Won ? "game.won" : "game.timeout") :
                !CanAct ? _text.Get(_paused ? "unity.paused" : "unity.running") :
                _plan.HasAction ? valid ? _text.Format("field.plan", cost, state.Energy - cost) : _text.Get(reason) :
                _text.Get(_plan.ShowsEntrances ? "field.deploy" : "field.ready"));
            _hint.gameObject.SetActive(!_paused && (_externalFeedback != null || _error != null || terminal ||
                _plan.HasAction || _plan.Anchor.HasValue));
            Label(_submit, _text.Get(terminal ? state.Phase == SiegeState.BattlePhase.Won ? "game.transport" :
                "game.return" :
                _plan.HasAction ? "unity.submit" : "unity.skip"));
            _submit.interactable = !_paused && (terminal || CanAct && valid && Allows("siege.submit"));
            _pauseInfo.text = _text.Format("field.help", _status.text, _intent.text,
                _text.Get("field.pauseInfo"));
            Label(_pause, _overlay == null ? _text.Get("field.menu") : _overlay.PauseCaption);
            if (_overlay != null) Label(_objectives, _overlay.ObjectivesCaption);
            _objectives.gameObject.SetActive(_overlay != null);
            Label(_resume, _text.Get("unity.resume")); Label(_retry, _text.Get("game.return"));
            _resume.interactable = _focused && !_backgrounded;
            _retry.interactable = _focused && !_backgrounded;
            RenderHand(state); RenderPortals(); RenderContext(state);
            System.Collections.Generic.IEnumerable<int> selected = _plan.Materials.Count > 0 ? _plan.Materials :
                _plan.FocusedUnit.HasValue ? new[] { _plan.FocusedUnit.Value } : Array.Empty<int>();
            _field.Show(_catalog, state, selected, _tower, CanAct && Allows("siege.unit"));
            string ghost = _plan.DeployCard.HasValue ? _plan.Definition(SiegeOrder.NewUnit, state) : null;
            _field.ShowPlan(ghost, _plan.DeployLane, _plan.Materials.Contains(SiegeOrder.NewUnit),
                _previewLane ?? (_plan.ShowsEntrances ? _plan.DeployLane : null));
        }

        private void RenderHand(SiegeState state)
        {
            if (_emptyHand != null)
            {
                _emptyHand.text = _text.Get("field.emptyHand");
                _emptyHand.gameObject.SetActive(state.Hand.Count == 0);
            }
            int stamp = CanAct && Allows("siege.card") ? 1 : 0;
            unchecked
            {
                stamp = stamp * 31 + state.Energy;
                stamp = stamp * 31 + (_plan.SelectedCard ?? 0);
                stamp = stamp * 31 + (_plan.DeployCard ?? 0);
                foreach (var card in state.Hand) stamp = stamp * 31 + card.Id;
            }
            if (stamp == _handStamp && Mathf.Approximately(_handWidth, _hand.rect.width)) return;
            _handStamp = stamp; _handWidth = _hand.rect.width;
            float width = _dock != null ? _dock.CardWidth : Mathf.Max(0,
                (_hand.rect.width - _handLayout.spacing * (_catalog.Rules.HandSize - 1)) / _catalog.Rules.HandSize);
            for (int i = 0; i < state.Hand.Count; i++)
            {
                var card = state.Hand[i]; var definition = _catalog.Unit(card.DefinitionId); var cell =
                    _handPool.Get(i);
                cell.SetSlotWidth(width);
                cell.BindMotion(_motion);
                cell.Bind(card.Id, _text.Get(definition.NameKey),
                    _plan.SelectedCard == card.Id || _plan.DeployCard == card.Id,
                    CanAct && Allows("siege.card") &&
                    (_guide == null || !_guide.IsActive || definition.Cost <= state.Energy), SelectCard);
                cell.SetCost(definition.Cost, definition.Cost <= state.Energy, _text.Get("field.unaffordable"),
                    _plan.DeployCard == card.Id ? _text.Get("field.planned") : null);
                cell.SetArt(definition.Icon); cell.SetDragHandlers(DragCard, DropCard, CancelDrag);
            }
            _handPool.Trim(state.Hand.Count);
        }

        private void RenderPortals()
        {
            var buttons = new[] { _top, _mid, _bottom };
            bool show = CanAct && _plan.ShowsEntrances;
            _routeRow.SetActive(show);
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].gameObject.SetActive(show);
                buttons[i].interactable = CanAct && Allows("siege.route");
                Label(buttons[i], _text.Get(_catalog.Lanes[i].NameKey));
            }
            if (_dock != null) _dock.Refresh();
        }

        private void RenderContext(SiegeState state)
        {
            bool focused = CanAct && _plan.FocusedUnit.HasValue;
            _context.gameObject.SetActive(focused || CanAct && (_plan.SelectedCard.HasValue || _plan.HasAction));
            _merge.gameObject.SetActive(focused && _plan.CanMerge(_catalog, state));
            _reroute.gameObject.SetActive(focused && _plan.CanReroute(state));
            _discard.gameObject.SetActive(CanAct && _plan.SelectedCard.HasValue);
            _clear.gameObject.SetActive(CanAct && (_plan.HasAction || focused || _plan.SelectedCard.HasValue));
            Label(_merge, _text.Format("unity.mergeCost", _catalog.Rules.MergeCost));
            Label(_reroute, _text.Get("field.reroute")); Label(_clear, _text.Get("field.cancel"));
            Label(_discard, _text.Get("field.discard"));
            _merge.interactable = Allows("siege.merge");
            _reroute.interactable = Allows("siege.reroute");
            _clear.interactable = Allows("siege.clear");
            _discard.interactable = Allows("siege.discard");
            int contextStamp = (_context.gameObject.activeSelf ? 1 : 0) +
                (_merge.gameObject.activeSelf ? 2 : 0) + (_reroute.gameObject.activeSelf ? 4 : 0) +
                (_discard.gameObject.activeSelf ? 8 : 0) + (_clear.gameObject.activeSelf ? 16 : 0);
            if (_dock != null && _contextStamp != contextStamp)
            {
                _contextStamp = contextStamp;
                LayoutRebuilder.ForceRebuildLayoutImmediate(_context);
                _dock.Refresh();
            }
            _context.anchorMin = _context.anchorMax = new Vector2(.5f, _dock != null ?
                _dock.ContextCenter / ((RectTransform)_context.parent).rect.height : .08f);
            _context.anchoredPosition = Vector2.zero;
            if (_tower != null)
            {
                var tower = state.Towers.FirstOrDefault(item => item.Id == _tower);
                var defense = tower == null ? null : _catalog.Defense(tower);
                _detail.text = defense == null ? string.Empty : _text.Format("unity.towerType",
                    _text.Get(defense.NameKey), Math.Max(0, tower.Hp), defense.Hp, defense.Range);
            }
            else _detail.text = _plan.Anchor.HasValue ? _text.Format("field.materials", _plan.Materials.Count) :
                string.Empty;
        }

        private void DragCard(int id, Vector2 position)
        {
            if (!CanAct || !Allows("siege.card") || _dragCard.HasValue && _dragCard != id) return;
            var card = _session.Snapshot.Hand.FirstOrDefault(item => item.Id == id);
            if (card == null) return;
            if (_guide != null && _guide.IsActive &&
                _catalog.Unit(card.DefinitionId).Cost > _session.Snapshot.Energy) return;
            _dragCard = id;
            _dragGhost.gameObject.SetActive(true); _dragIcon.Show(_catalog.Unit(card.DefinitionId).Icon);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_dragGhost.parent,
                position, null, out var local)) _dragGhost.anchoredPosition = local + Vector2.up * 90;
            if (_plan.SelectedCard != id) _plan.SelectCard(id);
            _guide?.ReportSucceeded("siege.card");
            _previewLane = HitLane(position);
            Render();
        }

        private string HitLane(Vector2 position)
        {
            var buttons = new[] { _top, _mid, _bottom };
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)buttons[i].transform, position, null)) return _catalog.Lanes[i].Id;
            return null;
        }

        private void CancelDrag()
        { _dragCard = null; _previewLane = null; _dragGhost.gameObject.SetActive(false); }

        private void CancelHandInput()
        {
            _handPool?.CancelContacts();
            CancelDrag();
        }

        private void DropCard(int id, Vector2 position)
        {
            if (_dragCard != id) return;
            string lane = HitLane(position);
            CancelDrag();
            if (!CanAct || !Allows("siege.route")) return;
            if (lane != null)
            {
                if (_plan.SelectedCard != id) _plan.SelectCard(id);
                _plan.ChooseLane(lane);
                _guide?.ReportSucceeded("siege.route");
            }
            else _plan.CancelSelection();
            Render();
        }

        private static void Label(Button button, string value) => button.GetComponentInChildren<Text>().text = value;
    }
}
