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
        /// <summary>拖拽残影在手指上方浮起的 UI 像素，避免手指遮住道具。</summary>
        private const float DragLift = 90f;

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
        [SerializeField] private GameObject _pauseMenu, _detailBacking;
        [SerializeField] private Text _status, _intent, _hint, _detail, _pauseInfo;
        [SerializeField] private Slider _playerBar, _animalBar, _resourceBar;
        [SerializeField] private Button _objectives;
        [SerializeField] private MotionScope _motion;
        [SerializeField] private SiegeHandLayout _dock;
        [SerializeField] private Text _resource, _dockResource;
        [SerializeField] private GameObject _resourceIcon;
        private ILevelOverlay _overlay;
        private GuideSession _guide;
        public RectTransform PauseTarget => (RectTransform)_pause.transform;
        public RectTransform ObjectivesTarget => (RectTransform)_objectives.transform;
        private readonly SiegeFieldPlan _plan = new SiegeFieldPlan();
        private readonly SiegeState _presentation = new SiegeState();
        private SiegeSession _session;
        private SiegeCatalog _catalog;
        private SiegeText _text;
        private SiegeCellPool _handPool;
        private Action _completed, _returnRequested;
        private bool _rescueResultPresented;
        internal void ShowRescueResult()
        {
            _rescueResultPresented = true;
            if (isActiveAndEnabled && _session != null) Render();
        }
        private Action<bool> _suspend;
        private bool _reported, _paused, _focused = true, _backgrounded;
        private string _error, _tower, _externalFeedback;
        private int _turn = -1;
        private int? _dragCard;
        private string _previewLane;
        private Button[] _routeButtons;
        private readonly Dictionary<Button, Text> _labels = new Dictionary<Button, Text>();
        private readonly List<int> _selected = new List<int>(3);
        private int _hudSeconds = -1, _hudEnergy = -1, _hudDefenderEnergy = -1;
        private double _hudProgress = -1;
        private string _hudIntent, _nextCardId, _nextCardHint;
        private Vector2[] _portals;
        private float _refreshIn;
        private SiegeState.BattlePhase _renderedPhase;
        private HorizontalLayoutGroup _handLayout;
        private int _handStamp;
        private bool _compact;
        private string _cardDetail;
        private float _detailUntil, _shownDetailWidth = -1;
        private string _shownDetail;
        private float _handWidth = -1;
        public SiegeFieldPlan Plan => _plan;
        public SiegeBattlefield Field => _field;
        public void SetPresentationAudio(Action<string> play) => _field.SetPresentationAudio(play);

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
            if (_session.Realtime && action == "siege.route")
            { targets.Add(_field.rectTransform); return true; }
            if (action == "siege.card")
            {
                var state = _session.Snapshot;
                int before = targets.Count;
                for (int index = 0; index < state.Hand.Count; index++)
                {
                    var cell = _handPool.Get(index);
                    if (cell.IsClickable && _catalog.Unit(state.Hand[index].DefinitionId).Cost <= state.Energy)
                        targets.Add((RectTransform)cell.transform);
                }
                return targets.Count > before;
            }
            if (action == "siege.route")
            {
                // All three lanes share one visibility/interactable state; a hole over any dead lane
                // would be the only opening and trap the player behind the mask.
                if (!GuideTargetRules.IsClickable(_top) || !GuideTargetRules.IsClickable(_mid) ||
                    !GuideTargetRules.IsClickable(_bottom)) return false;
                foreach (var button in new[] { _top, _mid, _bottom })
                    targets.Add((RectTransform)button.transform);
                return true;
            }
            if (action != "siege.submit") return false;
            if (!GuideTargetRules.IsClickable(_submit)) return false;
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

        public void Configure(Sprite animal, float width, Vector2[] portals) =>
            Configure(animal, width, portals, false, null);

        public void Configure(Sprite animal, float width, Vector2[] portals,
            bool animalOnSeparateFloor, HomelessPetHelper.Framework.UI.SceneSpritePalette props)
        {
            _portals = (Vector2[])portals.Clone();
            var buttons = new[] { _top, _mid, _bottom };
            int rank = 0;
            foreach (int index in Enumerable.Range(0, portals.Length).OrderBy(index => portals[index].x)
                .ThenBy(index => portals[index].y))
                buttons[index].transform.SetSiblingIndex(rank++);
            _field.ConfigureScene(animal, width, animalOnSeparateFloor, props);
            _dragIcon?.SetPalette(props);
        }

        public void Bind(SiegeSession session, TextAsset text, Action completed, Action returnRequested,
            Action<bool> suspend)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _field.ResetPresentation();
            _catalog = session.Catalog;
            _compact = session.Realtime && _dockResource != null && _resourceBar != null;
            bool dockBalance = _compact || _catalog.HasDeploymentZone && _dockResource != null;
            if (_resource != null) _resource.gameObject.SetActive(!dockBalance);
            if (_resourceIcon != null) _resourceIcon.SetActive(!dockBalance);
            if (_dock != null)
            {
                _dock.SetCompact(_compact);
                _dock.SetResourceAtHand(dockBalance);
            }
            _cardDetail = _shownDetail = null; _detailUntil = 0; _shownDetailWidth = -1;
            if (_resourceBar != null) _resourceBar.gameObject.SetActive(_compact);
            _text = new SiegeText(text);
            _text.Validate(_catalog);
            foreach (string key in new[] { "field.status", "field.intent", "field.plan", "field.deploy",
                "field.ready", "unity.resume", "field.pauseInfo", "field.reroute", "field.cancel",
                    "field.discard", "field.materials", "field.menu", "field.help" })
                _text.Get(key);
            _completed = completed; _returnRequested = returnRequested; _suspend = suspend;
            _rescueResultPresented = false;
            _plan.Clear(); _reported = _paused = _backgrounded = false; _focused = true;
            _error = _tower = _externalFeedback = null; _turn = -1;
            _nextCardId = _nextCardHint = null;
        }

        private void OnEnable()
        {
            if (_session == null) return;
            _handLayout = _hand.GetComponent<HorizontalLayoutGroup>();
            if (_dock != null) _dock.SetCapacity(_catalog.Rules.HandSize);
            _handWidth = -1; _handStamp = 0;
            _routeButtons = new[] { _top, _mid, _bottom };
            _hudSeconds = _hudEnergy = _hudDefenderEnergy = -1; _hudProgress = -1; _hudIntent = null;
            _handPool = new SiegeCellPool(_cellPrefab, _hand, _catalog.Rules.HandSize);
            _top.onClick.AddListener(Top); _mid.onClick.AddListener(Mid); _bottom.onClick.AddListener(Bottom);
            _submit.onClick.AddListener(Submit); _pause.onClick.AddListener(Pause);
            _resume.onClick.AddListener(Pause); _retry.onClick.AddListener(Return);
            _merge.onClick.AddListener(Merge); _reroute.onClick.AddListener(Reroute);
            _clear.onClick.AddListener(Clear); _discard.onClick.AddListener(Discard);
            _objectives.onClick.AddListener(Objectives);
            _field.LaneClicked += SelectLane;
            _field.DeploymentClicked += DeployAt;
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
            _field.LaneClicked -= SelectLane;
            _field.DeploymentClicked -= DeployAt;
            _field.UnitClicked -= SelectUnit; _field.TowerClicked -= SelectTower; _field.EmptyClicked -= Empty;
            _handPool?.Dispose(); _handPool = null;
            _field.ResetPresentation();
            _dragGhost.gameObject.SetActive(false); _plan.Clear();
        }

        public void Unbind()
        {
            CancelHandInput();
            _session = null; _completed = _returnRequested = null; _suspend = null;
            _plan.Clear(); _externalFeedback = null;
        }

        private bool CanAct => _session != null && (_session.Realtime ?
            _session.Phase == SiegeState.BattlePhase.Running : _session.Phase == SiegeState.BattlePhase.Decision) &&
            !_session.Paused && !_paused;

        private void Update()
        {
            if (_session == null) return;
            _session.SetDecisionHold(_guide != null && _guide.IsActive);
            _session.Advance(Math.Min(100, Time.unscaledDeltaTime * 1000));
            if (!_paused && !_backgrounded && _focused)
                _field.AdvanceTerminalEffects(Time.unscaledDeltaTime * 1000);
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
            if (lane < _catalog.Lanes.Count && _catalog.Lanes[lane].Open) SelectLane(_catalog.Lanes[lane].Id);
        }
        private void SelectLane(string lane)
        {
            if (!CanAct || !Allows("siege.route") || !_plan.ShowsEntrances) return;
            _plan.ChooseLane(lane); _error = null; Render();
            _guide?.ReportSucceeded("siege.route");
            if (_session.Realtime && _plan.RerouteLane != null) Submit();
            else Render();
        }
        private void DeployAt(SiegePoint point)
        {
            if (!CanAct || !Allows("siege.route") || !_plan.SelectedCard.HasValue) return;
            var lane = SiegeDeployment.NearestLane(_catalog, point);
            if (lane == null) return;
            _plan.ChoosePoint(point, lane.Id);
            bool success = _session.TryOrder(_plan.Order(_presentation), true, out _error, out _);
            _plan.Clear();
            if (success) _guide?.ReportSucceeded("siege.route");
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
            _plan.Focus(id, _catalog, _session.Snapshot); _tower = _error = null;
            if (_session.Realtime && _plan.Anchor.HasValue && _plan.Materials.Count == _plan.RequiredMaterials)
                Submit();
            else Render();
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
            if (_session == null || _rescueResultPresented) return;
            if (_session.Phase == SiegeState.BattlePhase.Won || _session.Phase == SiegeState.BattlePhase.Timeout)
            { Return(); return; }
            if (!CanAct || !_session.Realtime && !Allows("siege.submit")) return;
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
            _session.CopyPresentationTo(_presentation);
            var state = _presentation;
            if (_motion != null) _motion.SetScenePaused(state.Paused || _paused);
            _renderedPhase = state.Phase;
            if (_turn != state.Turn) { _turn = state.Turn; _plan.Clear(); }
            bool terminal = state.Phase == SiegeState.BattlePhase.Won || state.Phase == SiegeState.BattlePhase.Timeout;
            string reason = null; int cost = 0;
            bool choosingMaterials = _plan.Anchor.HasValue && _plan.Materials.Count < _plan.RequiredMaterials;
            bool valid = !choosingMaterials && (!_plan.HasAction ||
                _session.TryOrder(_plan.Order(state), false, out reason, out cost));
            int seconds = (int)Math.Ceiling((_catalog.Rules.DurationMs - state.ElapsedMs) / 1000.0);
            bool hudChanged = seconds != _hudSeconds || state.Energy != _hudEnergy ||
                state.DefenderEnergy != _hudDefenderEnergy || state.Progress != _hudProgress;
            if (hudChanged)
            {
                _hudSeconds = seconds; _hudEnergy = state.Energy; _hudDefenderEnergy = state.DefenderEnergy;
                _hudProgress = state.Progress;
                _status.text = (_compact || _catalog.HasDeploymentZone) ? seconds + _text.Get("seconds") :
                    _text.Format("field.compactStatus", state.Progress, _catalog.Rules.Target, seconds, state.Energy);
                var balance = (_compact || _catalog.HasDeploymentZone) && _dockResource != null ? _dockResource : _resource;
                if (balance != null) balance.text = _text.Format(
                    state.Realtime ? "field.realtimeResource" : "field.resource", state.Energy,
                    _catalog.Rules.EnergyCap, state.DefenderEnergy, _catalog.Rules.DefenderEnergyCap);
            }
            var encounter = _catalog.Content.Encounter(state.EncounterId);
            var animal = state.AnimalState;
            string intentKey = animal.Intent == SiegeState.IntentKind.Guard ? encounter.GuardNameKey :
                animal.Intent == SiegeState.IntentKind.Sweep ? encounter.SweepNameKey :
                animal.Intent == SiegeState.IntentKind.Hold ? encounter.HoldNameKey :
                animal.Intent == SiegeState.IntentKind.Carry ? encounter.CarryNameKey : "unity." +
                    animal.Intent.ToString().ToLowerInvariant();
            bool intentChanged = _hudIntent != intentKey;
            if (intentChanged)
            { _hudIntent = intentKey; _intent.text = _text.Format("field.intent", _text.Get(intentKey)); }
            _playerBar.SetValueWithoutNotify((float)state.PlayerChargeMs / (state.Realtime
                ? _catalog.Rules.ResourcePeriodMs : _catalog.Rules.PlayerPeriodMs));
            _animalBar.SetValueWithoutNotify((float)animal.Charge);
            if (_compact)
            {
                float available = state.Energy + (float)state.ResourceChargeMs / _catalog.Rules.ResourcePeriodMs;
                _resourceBar.SetValueWithoutNotify(Mathf.Clamp01(available / _catalog.Rules.EnergyCap));
            }
            if (_nextCardId != state.NextCardDefinitionId)
            {
                _nextCardId = state.NextCardDefinitionId;
                var nextCard = _catalog.Unit(_nextCardId);
                _nextCardHint = nextCard == null ? null :
                    _text.Format("field.nextCard", _text.Get(nextCard.NameKey), nextCard.Cost);
            }
            bool selectedUnaffordable = _compact && SelectedUnaffordable(state);
            _hint.text = _externalFeedback ?? (_error != null ? _text.Get(_error) :
                terminal ? _text.Get(state.Phase == SiegeState.BattlePhase.Won ? "game.won" : "game.timeout") :
                !CanAct ? _text.Get(_paused ? "unity.paused" : "unity.running") :
                choosingMaterials ? _text.Format("field.materials",
                    _plan.Materials.Count, _plan.RequiredMaterials) :
                _plan.HasAction ? valid ? _text.Format("field.plan", cost, state.Energy - cost) : _text.Get(reason) :
                selectedUnaffordable ? _text.Get("field.unaffordable") :
                _nextCardHint ?? _text.Get(_plan.ShowsEntrances ? "field.deploy" : "field.ready"));
            _hint.gameObject.SetActive(!_rescueResultPresented && !_paused &&
                (_externalFeedback != null || _error != null || terminal || _plan.HasAction ||
                    _plan.Anchor.HasValue || selectedUnaffordable || !_compact && _nextCardHint != null));
            Label(_submit, _text.Get(terminal ? state.Phase == SiegeState.BattlePhase.Won ? "game.transport" :
                "game.return" :
                _plan.HasAction ? "unity.submit" : "unity.skip"));
            _submit.gameObject.SetActive(!_rescueResultPresented && (terminal || !state.Realtime));
            _submit.interactable = !_paused && (terminal || CanAct && valid && Allows("siege.submit"));
            if (hudChanged || intentChanged)
                _pauseInfo.text = state.Realtime ? _status.text + "\n" + _intent.text + "\n\n" +
                    _text.Get("field.pauseInfo") : _text.Format("field.help", _status.text, _intent.text,
                    _text.Get("field.pauseInfo"));
            Label(_pause, _overlay == null ? _text.Get("field.menu") : _overlay.PauseCaption);
            if (_overlay != null) Label(_objectives, _overlay.ObjectivesCaption);
            _objectives.gameObject.SetActive(_overlay != null);
            Label(_resume, _text.Get("unity.resume")); Label(_retry, _text.Get("game.return"));
            _resume.interactable = _focused && !_backgrounded;
            _retry.interactable = _focused && !_backgrounded;
            RenderHand(state); RenderPortals(); RenderContext(state);
            _selected.Clear();
            if (_plan.Materials.Count > 0) _selected.AddRange(_plan.Materials);
            else if (_plan.FocusedUnit.HasValue) _selected.Add(_plan.FocusedUnit.Value);
            _field.Show(_catalog, state, _selected, _tower,
                CanAct && (Allows("siege.unit") || Allows("siege.route")));
            string ghost = _plan.DeployCard.HasValue ? _plan.Definition(SiegeOrder.NewUnit, state) : null;
            _field.ShowPlan(ghost, _plan.DeployLane, _plan.Materials.Contains(SiegeOrder.NewUnit),
                _previewLane ?? (_plan.ShowsEntrances ? _plan.DeployLane : null), _plan.ShowsEntrances,
                state.Realtime && _plan.SelectedCard.HasValue);
        }

        private bool SelectedUnaffordable(SiegeState state)
        {
            if (!_plan.SelectedCard.HasValue) return false;
            foreach (var card in state.Hand)
                if (card.Id == _plan.SelectedCard.Value)
                    return _catalog.Unit(card.DefinitionId).Cost > state.Energy;
            return false;
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
                if (_compact) cell.SetCompact(InspectCard);
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
            var buttons = _routeButtons;
            bool show = CanAct && _plan.ShowsEntrances;
            _routeRow.SetActive(show && !_session.Realtime);
            for (int i = 0; i < buttons.Length; i++)
            {
                bool exists = i < _catalog.Lanes.Count;
                bool open = exists && _catalog.Lanes[i].Open;
                buttons[i].gameObject.SetActive(show && exists && (!_catalog.HasDeploymentZone || open));
                if (!exists) continue;
                buttons[i].interactable = CanAct && open && Allows("siege.route");
                Label(buttons[i], _text.Get(_catalog.Lanes[i].NameKey));
                var rect = (RectTransform)buttons[i].transform;
                rect.anchorMin = rect.anchorMax = new Vector2(_portals[i].x / (float)_catalog.Width,
                    1 - _portals[i].y / (float)_catalog.Height);
                rect.anchoredPosition = Vector2.zero;
            }
            if (_dock != null) _dock.Refresh();
        }

        private void RenderContext(SiegeState state)
        {
            bool focused = CanAct && _plan.FocusedUnit.HasValue;
            _context.gameObject.SetActive(focused || CanAct && (_plan.SelectedCard.HasValue || _plan.HasAction));
            _merge.gameObject.SetActive(focused && _plan.CanMerge(_catalog, state));
            _reroute.gameObject.SetActive(focused && _plan.CanReroute(state));
            _discard.gameObject.SetActive(CanAct && !_session.Realtime && _plan.SelectedCard.HasValue);
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
            if (_dock == null)
            {
                _context.anchorMin = _context.anchorMax = new Vector2(.5f, .08f);
                _context.anchoredPosition = Vector2.zero;
            }
            if (_tower != null)
            {
                var tower = state.Towers.FirstOrDefault(item => item.Id == _tower);
                var defense = tower == null ? null : _catalog.Defense(tower);
                _detail.text = defense == null ? string.Empty : _text.Format("unity.towerType",
                    _text.Get(defense.NameKey), Math.Max(0, tower.Hp), defense.Hp, defense.Range);
            }
            else if (_cardDetail != null && Time.unscaledTime < _detailUntil) _detail.text = _cardDetail;
            else _detail.text = _plan.Anchor.HasValue ?
                _text.Format("field.materials", _plan.Materials.Count, _plan.RequiredMaterials) :
                string.Empty;
            RefreshDetailBacking();
        }

        private void RefreshDetailBacking()
        {
            if (_detailBacking == null) return;
            var rect = _detail.rectTransform;
            if (_shownDetail == _detail.text && Mathf.Approximately(_shownDetailWidth, rect.rect.width)) return;
            _shownDetail = _detail.text;
            _shownDetailWidth = rect.rect.width;
            bool visible = !string.IsNullOrEmpty(_shownDetail);
            _detailBacking.SetActive(visible);
            if (!visible) return;
            float height = Mathf.Max(96, _detail.preferredHeight + 12);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            ((RectTransform)_detailBacking.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                height + 24);
        }

        private void InspectCard(int id)
        {
            if (!CanAct || !Allows("siege.card")) return;
            var card = _presentation.Hand.FirstOrDefault(item => item.Id == id);
            if (card == null) return;
            var unit = _catalog.Unit(card.DefinitionId);
            _cardDetail = _text.Get(unit.NameKey) + "\n" + _text.Get(unit.DetailKey);
            _detailUntil = Time.unscaledTime + 4;
            Render();
        }

        private void DragCard(int id, Vector2 position)
        {
            if (!CanAct || !Allows("siege.card") || _dragCard.HasValue && _dragCard != id) return;
            var card = _presentation.Hand.FirstOrDefault(item => item.Id == id);
            if (card == null) return;
            if (_guide != null && _guide.IsActive &&
                _catalog.Unit(card.DefinitionId).Cost > _session.Snapshot.Energy) return;
            _dragCard = id;
            _dragGhost.gameObject.SetActive(true); _dragIcon.Show(_catalog.Unit(card.DefinitionId).Icon);
            var ownerCanvas = GetComponentInParent<Canvas>();
            var camera = ownerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ownerCanvas.worldCamera;
            if (TryResolveGhostAnchoredPosition((RectTransform)_dragGhost.parent, _dragGhost, position,
                camera, DragLift, out var anchored)) _dragGhost.anchoredPosition = anchored;
            if (_plan.SelectedCard != id) _plan.SelectCard(id);
            _guide?.ReportSucceeded("siege.card");
            _previewLane = _session.Realtime ? null : HitLane(position);
            Render();
        }

        private string HitLane(Vector2 position)
        {
            var ownerCanvas = GetComponentInParent<Canvas>();
            return _field.PickLane(position, ownerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null :
                ownerCanvas.worldCamera);
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
            if (_session.Realtime)
            {
                var owner = GetComponentInParent<Canvas>();
                bool inside = _field.PickDeployment(position, owner.renderMode == RenderMode.ScreenSpaceOverlay ?
                    null : owner.worldCamera, out var point);
                CancelDrag();
                if (inside) DeployAt(point);
                else { _plan.CancelSelection(); _error = "field.dropOutside"; Render(); }
                return;
            }
            string lane = HitLane(position);
            CancelDrag();
            if (!CanAct || !Allows("siege.route")) return;
            if (lane != null)
            {
                _error = _tower = null;
                if (_plan.SelectedCard != id) _plan.SelectCard(id);
                _plan.ChooseLane(lane);
                _guide?.ReportSucceeded("siege.route");
            }
            else { _plan.CancelSelection(); _error = "field.dropOutside"; }
            Render();
        }

        /// <summary>
        /// 把屏幕点换算成残影的 anchoredPosition。
        /// <para>
        /// ScreenPointToLocalPointInRectangle 返回的是以父节点 pivot 为原点的局部坐标，
        /// 而 anchoredPosition 是相对该节点 anchor 点的偏移。两者直接混用会相差
        /// 「父节点 pivot → anchor 点」这一向量：本 prefab 的残影 anchor 为 (0,0)、父节点
        /// pivot 为 (0.5,0.5)，1080×2400 下偏差约 (540,1110)，残影会甩到左下角。
        /// </para>
        /// 仅适用于 anchorMin == anchorMax 的定尺寸节点（拉伸节点需另算 sizeDelta）。
        /// </summary>
        public static bool TryResolveGhostAnchoredPosition(RectTransform container, RectTransform ghost,
            Vector2 screenPosition, Camera camera, float lift, out Vector2 anchoredPosition)
        {
            anchoredPosition = default;
            if (container == null || ghost == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screenPosition, camera,
                    out var local)) return false;
            anchoredPosition = ToAnchoredPosition(local, container.rect, ghost.anchorMin, lift);
            return true;
        }

        /// <summary>
        /// 把容器局部坐标（原点在容器 pivot）换算成 anchor 为 <paramref name="anchor"/> 的节点的
        /// anchoredPosition。anchorMin != anchorMax 的拉伸节点不适用。
        /// </summary>
        public static Vector2 ToAnchoredPosition(Vector2 localPoint, Rect containerRect, Vector2 anchor,
            float lift)
        {
            Vector2 anchorReference = containerRect.min + Vector2.Scale(anchor, containerRect.size);
            return localPoint - anchorReference + Vector2.up * lift;
        }

        private void Label(Button button, string value)
        {
            if (!_labels.TryGetValue(button, out var label))
            { label = button.GetComponentInChildren<Text>(); _labels.Add(button, label); }
            if (label.text != value) label.text = value;
        }
    }
}
