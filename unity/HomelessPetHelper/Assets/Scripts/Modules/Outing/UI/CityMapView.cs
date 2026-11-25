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

        private readonly Dictionary<string, CityMapPointBinding> _points =
            new Dictionary<string, CityMapPointBinding>(StringComparer.Ordinal);
        private UnityAction[] _pointClicks;
        private bool _initialized;
        private bool _listening;
        private bool _focused = true;
        private bool _applicationPaused;
        private enum PopupKind { Place }
        private readonly PopupStack<PopupKind> _popups = new PopupStack<PopupKind>();
        private bool _selecting = true;
        private bool _inputPaused;
        private string _focusedPointId;
        private bool _resetDetails;

        public bool IsSuspended => !_focused || _applicationPaused;
        public bool IsDetailsOpen => _popups.Top.HasValue;

        public event Action<string> PlaceSelected;
        public event Action PrimaryClicked;
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
            _closeDetails.onClick.AddListener(CloseDetails);
            _backdrop.onClick.AddListener(CloseDetails);
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
            _closeDetails.onClick.RemoveListener(CloseDetails);
            _backdrop.onClick.RemoveListener(CloseDetails);
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

        public void RenderPoint(string pointId, string label, string stateLabel, PointDisplayState state,
            bool selected, bool interactable)
        {
            if (!_points.TryGetValue(pointId, out CityMapPointBinding point))
                throw new ArgumentException("Unbound city map point: " + pointId, nameof(pointId));
            bool locked = state == PointDisplayState.Locked;
            bool marked = selected && !locked;
            point.Render(label, stateLabel, interactable && !locked, marked ? _selectedPointPaper : _pointPaper,
                _fog != null && locked);
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
            _closeDetails.interactable = !paused;
            _backdrop.interactable = !paused;
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

        public void CloseDetails()
        {
            _popups.TryClose(PopupKind.Place);
            RefreshPopup();
        }

        public void ShowNotice(string message) => _toast.Show(message);

        private void RefreshPopup()
        {
            _detailsRoot.SetActive(IsDetailsOpen);
            _backdrop.gameObject.SetActive(IsDetailsOpen);
            _cancel.gameObject.SetActive(!IsDetailsOpen);
            _mapScroll.enabled = !_inputPaused && !IsDetailsOpen;
        }

        private void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) OnCancel();
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
