using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Outing.UI
{
    /// <summary>Saved map bindings only; the owner supplies all rules and localized copy.</summary>
    public sealed class CityMapView : MonoBehaviour
    {
        [SerializeField] private Text _status;
        [SerializeField] private Text _heading;
        [SerializeField] private Text _details;
        [SerializeField] private Text _departure;
        [SerializeField] private Text _feedback;
        [SerializeField] private string _backgroundId;
        [SerializeField] private CityMapPointBinding[] _pointBindings;
        [SerializeField] private Sprite _pointPaper;
        [SerializeField] private Sprite _selectedPointPaper;
        [SerializeField] private Sprite _stationIcon;
        [SerializeField] private Button _primary;
        [SerializeField] private Text _primaryLabel;
        [SerializeField] private Button _cancel;
        [SerializeField] private Button _closeDetails;
        [SerializeField] private Button _backdrop;
        [SerializeField] private GameObject _detailsRoot;
        [SerializeField] private ToastView _toast;
        [SerializeField] private CityMapScrollRect _mapScroll;
        [SerializeField] private ScrollRect _detailsScroll;
        [SerializeField] private GameObject _detailsOverflow;
        [SerializeField] private CityMapAtmosphere _atmosphere;
        [SerializeField] private CityMapFog _fog;
        [SerializeField] private CityMapDayNight _dayNight;

        private readonly Dictionary<string, CityMapPointBinding> _points =
            new Dictionary<string, CityMapPointBinding>(StringComparer.Ordinal);
        private UnityAction[] _pointClicks;
        private bool _initialized;
        private bool _listening;
        private bool _focused = true;
        private bool _applicationPaused;
        private enum PopupKind { Place, Station }
        private readonly PopupStack<PopupKind> _popups = new PopupStack<PopupKind>();
        private bool _selecting = true;
        private bool _inputPaused;
        private string _focusedPointId;
        private bool _resetDetails;
        private Button _secondary, _tertiary, _stationStatus;
        private Text _secondaryLabel, _tertiaryLabel, _stationStatusLabel;
        private Image _detailIcon;
        private bool _showStationStatus;

        public bool IsSuspended => !_focused || _applicationPaused;
        public bool IsDetailsOpen => _popups.Top.HasValue;
        public bool IsStationOpen => _popups.Top == PopupKind.Station;

        public event Action<string> PlaceSelected;
        public event Action PrimaryClicked;
        public event Action SecondaryClicked;
        public event Action TertiaryClicked;
        public event Action StationOpened;
        public event Action CancelClicked;
        public event Action<bool> PauseChanged;

        private void Awake()
        {
            if (_status == null || _heading == null || _details == null || _departure == null || _feedback == null ||
                string.IsNullOrWhiteSpace(_backgroundId) ||
                _pointBindings == null || _pointBindings.Length == 0 ||
                _primary == null || _primaryLabel == null || _cancel == null || _closeDetails == null ||
                _backdrop == null || _detailsRoot == null || _toast == null ||
                _mapScroll == null || _detailsScroll == null || _detailsOverflow == null ||
                _pointPaper == null || _selectedPointPaper == null)
                throw new InvalidOperationException("City map has incomplete saved bindings.");
            var buttons = new HashSet<Button>();
            _pointClicks = new UnityAction[_pointBindings.Length];
            for (int i = 0; i < _pointBindings.Length; i++)
            {
                CityMapPointBinding point = _pointBindings[i];
                if (point == null) throw new InvalidOperationException("City map contains a null point binding.");
                point.Validate();
                if (_points.ContainsKey(point.PointId) || !buttons.Add(point.Button))
                    throw new InvalidOperationException("Duplicate city map point ID or button: " + point.PointId);
                _points.Add(point.PointId, point);
                _pointClicks[i] = () =>
                {
                    if (isActiveAndEnabled && !_inputPaused && !IsDetailsOpen &&
                        point.Button.IsInteractable()) PlaceSelected?.Invoke(point.PointId);
                };
            }
            _initialized = true;
        }

        private void OnEnable()
        {
            if (!_initialized) return;
            _popups.Clear();
            RefreshPopup();
            _focusedPointId = null;
            _resetDetails = true;
            for (int i = 0; i < _pointBindings.Length; i++)
                _pointBindings[i].Button.onClick.AddListener(_pointClicks[i]);
            _primary.onClick.AddListener(OnPrimary);
            _cancel.onClick.AddListener(OnCancel);
            _closeDetails.onClick.AddListener(DismissDetails);
            _backdrop.onClick.AddListener(DismissDetails);
            _listening = true;
            PauseChanged?.Invoke(IsSuspended);
        }

        private void OnDisable()
        {
            if (!_listening) return;
            for (int i = 0; i < _pointBindings.Length; i++)
                if (_pointBindings[i].Button != null)
                    _pointBindings[i].Button.onClick.RemoveListener(_pointClicks[i]);
            _primary.onClick.RemoveListener(OnPrimary);
            _cancel.onClick.RemoveListener(OnCancel);
            _closeDetails.onClick.RemoveListener(DismissDetails);
            _backdrop.onClick.RemoveListener(DismissDetails);
            _popups.Clear();
            RefreshPopup();
            _toast.Clear();
            _listening = false;
        }

        public void ValidateDefinition(string backgroundId, IEnumerable<string> pointIds)
        {
            if (!_initialized) throw new InvalidOperationException("City map view is not initialized.");
            if (_backgroundId != backgroundId || pointIds == null ||
                !new HashSet<string>(pointIds, StringComparer.Ordinal).SetEquals(_points.Keys))
                throw new InvalidOperationException(
                    "City map definition does not match saved background/point bindings.");
        }

        public void RenderTimeOfDay(int minuteOfDay, int minutesPerDay)
        {
            if (_dayNight != null) _dayNight.RenderTime(minuteOfDay, minutesPerDay);
        }

        public void RenderPoint(string pointId, string label, string stateLabel, PointDisplayState state,
            bool selected, bool interactable)
        {
            if (!_points.TryGetValue(pointId, out CityMapPointBinding point))
                throw new ArgumentException("Unbound city map point: " + pointId, nameof(pointId));
            bool locked = state == PointDisplayState.Locked;
            bool marked = selected && !locked;
            point.Render(label, stateLabel, interactable && !locked, marked ? _selectedPointPaper : _pointPaper,
                _selectedPointPaper,
                _fog != null && locked, state == PointDisplayState.HasHelpRequest);
            if (_atmosphere != null && pointId == "market") _atmosphere.SetFlowerLocked(locked);
            if (marked && _focusedPointId != pointId)
            {
                _focusedPointId = pointId;
                _mapScroll.FocusOn((RectTransform)point.Button.transform);
            }
        }

        public void Render(string status, string heading, string details, string departure, string feedback,
            string command, bool selecting, bool paused, bool canAct)
        {
            if (_heading.text != heading || _details.text != details) _resetDetails = true;
            _status.text = status;
            _status.gameObject.SetActive(!string.IsNullOrWhiteSpace(status));
            _heading.text = heading;
            _details.text = details;
            _departure.text = departure;
            _feedback.text = feedback;
            _departure.gameObject.SetActive(!string.IsNullOrWhiteSpace(departure));
            _feedback.gameObject.SetActive(!string.IsNullOrWhiteSpace(feedback));
            _primaryLabel.text = command;
            _primary.interactable = !paused && canAct;
            if (selecting && !_selecting) CloseDetails();
            _selecting = selecting;
            _inputPaused = paused;
            _cancel.interactable = !paused;
            _detailsScroll.enabled = !paused;
            _toast.SetSuspended(paused);
            if (!selecting && !IsDetailsOpen) OpenDetails();
            RefreshPopup();
        }

        public void OpenDetails()
        {
            if (!isActiveAndEnabled || _inputPaused) return;
            _popups.TryOpen(PopupKind.Place);
            _resetDetails = true;
            _toast.Clear();
            RefreshPopup();
        }

        public void RenderExtraActions(string secondary, string tertiary, string station, bool enabled, bool canSelect)
        {
            if (_secondary == null)
            {
                _secondary = CopyAction("Explore", _primary.transform.parent, out _secondaryLabel,
                    () => SecondaryClicked?.Invoke());
                _tertiary = CopyAction("Puzzle", _primary.transform.parent, out _tertiaryLabel,
                    () => TertiaryClicked?.Invoke());
                _stationStatus = CopyAction("StationStatus", _cancel.transform.parent, out _stationStatusLabel,
                    OpenStation);
                _stationStatus.transform.SetSiblingIndex(_cancel.transform.GetSiblingIndex() + 1);
                var stationRect = (RectTransform)_stationStatus.transform;
                var returnRect = (RectTransform)_cancel.transform;
                stationRect.anchorMin = stationRect.anchorMax = Vector2.right;
                stationRect.pivot = new Vector2(1 - returnRect.pivot.x, returnRect.pivot.y);
                stationRect.anchoredPosition = new Vector2(-returnRect.anchoredPosition.x, returnRect.anchoredPosition.y);
                stationRect.sizeDelta = new Vector2(returnRect.sizeDelta.x + 48, returnRect.sizeDelta.y);
                _secondary.transform.SetAsFirstSibling();
                _tertiary.transform.SetSiblingIndex(1);
            }
            SetAction(_secondary, _secondaryLabel, secondary, enabled);
            SetAction(_tertiary, _tertiaryLabel, tertiary, enabled);
            _showStationStatus = canSelect;
            _stationStatusLabel.text = station;
            RefreshPopup();
        }

        public void RenderDetailIcon(string pointId)
        {
            Sprite icon = pointId != null && _points.TryGetValue(pointId, out var point)
                ? point.DetailArt : _stationIcon;
            if (_detailIcon == null && icon != null)
            {
                var parent = _details.transform.parent;
                int index = _details.transform.GetSiblingIndex();
                var row = new GameObject("DetailIllustrationRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(parent, false);
                row.transform.SetSiblingIndex(index);
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 24;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                var iconObject = new GameObject("DetailIllustration", typeof(RectTransform), typeof(Image),
                    typeof(LayoutElement));
                iconObject.transform.SetParent(row.transform, false);
                _detailIcon = iconObject.GetComponent<Image>();
                _detailIcon.raycastTarget = false;
                _detailIcon.preserveAspect = true;
                var size = iconObject.GetComponent<LayoutElement>();
                size.preferredWidth = size.preferredHeight = 108;
                _details.transform.SetParent(row.transform, false);
                var textSize = _details.GetComponent<LayoutElement>() ?? _details.gameObject.AddComponent<LayoutElement>();
                textSize.flexibleWidth = 1;
            }
            if (_detailIcon == null) return;
            _detailIcon.sprite = icon;
            _detailIcon.gameObject.SetActive(icon != null);
        }

        private Button CopyAction(string actionName, Transform parent, out Text label, UnityAction clicked)
        {
            var button = Instantiate(_primary, parent, false);
            button.name = actionName;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                if (isActiveAndEnabled && button.isActiveAndEnabled && button.IsInteractable()) clicked();
            });
            label = button.GetComponentInChildren<Text>(true);
            label.name = actionName + "Label";
            return button;
        }

        private static void SetAction(Button button, Text label, string title, bool enabled)
        {
            label.text = title ?? string.Empty;
            button.gameObject.SetActive(!string.IsNullOrEmpty(title));
            button.interactable = enabled;
        }

        private void OpenStation()
        {
            if (_inputPaused || !_selecting || IsDetailsOpen || !_popups.TryOpen(PopupKind.Station)) return;
            StationOpened?.Invoke();
            _resetDetails = true;
            RefreshPopup();
        }

        private void DismissDetails()
        {
            if (!_selecting || _inputPaused) return;
            CloseDetails();
        }

        public void CloseDetails()
        {
            if (_popups.Top is PopupKind kind) _popups.TryClose(kind);
            RefreshPopup();
        }

        public void ShowNotice(string message) => _toast.Show(message);

        private void RefreshPopup()
        {
            _detailsRoot.SetActive(IsDetailsOpen);
            _backdrop.gameObject.SetActive(IsDetailsOpen);
            // After arrival this panel is the only way to play an activity or return to the station.
            _closeDetails.gameObject.SetActive(_selecting);
            _closeDetails.interactable = _backdrop.interactable = _selecting && !_inputPaused;
            _cancel.gameObject.SetActive(!IsDetailsOpen);
            if (_stationStatus != null)
            {
                _stationStatus.gameObject.SetActive(_showStationStatus && !IsDetailsOpen);
                _stationStatus.interactable = !_inputPaused;
            }
            _mapScroll.enabled = !_inputPaused && !IsDetailsOpen;
        }

        private void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) OnCancel();
            if (!IsSuspended && _dayNight != null) _dayNight.Advance(Time.unscaledDeltaTime);
            if (!IsSuspended && !IsDetailsOpen)
                foreach (var point in _pointBindings) point.AdvanceBadge(Time.unscaledDeltaTime);
            if (_selecting && !_inputPaused && !IsSuspended && !IsDetailsOpen)
            {
                float elapsed = Time.unscaledDeltaTime;
                if (_atmosphere != null) _atmosphere.Advance(elapsed);
                if (_fog != null) _fog.Advance(elapsed);
            }
            if (!IsDetailsOpen) return;
            bool overflow = _detailsScroll.content.rect.height > _detailsScroll.viewport.rect.height + 1;
            if (_detailsOverflow.activeSelf != overflow) _detailsOverflow.SetActive(overflow);
            if (_resetDetails && _detailsScroll.isActiveAndEnabled)
            {
                _detailsScroll.StopMovement();
                _detailsScroll.verticalNormalizedPosition = 1;
                _resetDetails = false;
            }
        }

        private void OnPrimary()
        {
            if (isActiveAndEnabled && _primary.isActiveAndEnabled && _primary.IsInteractable())
                PrimaryClicked?.Invoke();
        }

        private void OnCancel()
        {
            if (!isActiveAndEnabled || !_cancel.IsInteractable()) return;
            if (IsDetailsOpen && _selecting) CloseDetails();
            else if (_selecting) CancelClicked?.Invoke();
            else if (_primary.IsInteractable()) PrimaryClicked?.Invoke();
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            PauseChanged?.Invoke(IsSuspended);
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            PauseChanged?.Invoke(IsSuspended);
        }
    }
}
