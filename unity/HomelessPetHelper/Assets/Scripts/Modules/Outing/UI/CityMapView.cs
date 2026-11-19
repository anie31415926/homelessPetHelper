using System;
using System.Collections.Generic;
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
        [SerializeField] private Text _feedback;
        [SerializeField] private string _backgroundId;
        [SerializeField] private CityMapPointBinding[] _pointBindings;
        [SerializeField] private Color _selectedColor = new Color32(255, 214, 122, 255);
        [SerializeField] private Color _normalColor = new Color32(255, 255, 255, 245);
        [SerializeField] private Button _primary;
        [SerializeField] private Text _primaryLabel;
        [SerializeField] private Button _cancel;
        [SerializeField] private Toggle _pause;

        private readonly Dictionary<string, CityMapPointBinding> _points =
            new Dictionary<string, CityMapPointBinding>(StringComparer.Ordinal);
        private UnityAction[] _pointClicks;
        private bool _initialized;
        private bool _listening;
        private bool _focused = true;
        private bool _applicationPaused;
        private bool _manualPause;

        public bool IsSuspended => _manualPause || !_focused || _applicationPaused;

        public event Action<string> PlaceSelected;
        public event Action PrimaryClicked;
        public event Action CancelClicked;
        public event Action<bool> PauseChanged;

        private void Awake()
        {
            if (_status == null || _heading == null || _details == null || _feedback == null ||
                string.IsNullOrWhiteSpace(_backgroundId) ||
                _pointBindings == null || _pointBindings.Length == 0 ||
                _primary == null || _primaryLabel == null || _cancel == null || _pause == null)
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
                    if (isActiveAndEnabled && point.Button.IsInteractable()) PlaceSelected?.Invoke(point.PointId);
                };
            }
            _initialized = true;
        }

        private void OnEnable()
        {
            if (!_initialized) return;
            for (int i = 0; i < _pointBindings.Length; i++)
                _pointBindings[i].Button.onClick.AddListener(_pointClicks[i]);
            _primary.onClick.AddListener(OnPrimary);
            _cancel.onClick.AddListener(OnCancel);
            _pause.onValueChanged.AddListener(OnPause);
            _listening = true;
        }

        private void OnDisable()
        {
            if (!_listening) return;
            for (int i = 0; i < _pointBindings.Length; i++)
                if (_pointBindings[i].Button != null)
                    _pointBindings[i].Button.onClick.RemoveListener(_pointClicks[i]);
            _primary.onClick.RemoveListener(OnPrimary);
            _cancel.onClick.RemoveListener(OnCancel);
            _pause.onValueChanged.RemoveListener(OnPause);
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

        public void RenderPoint(string pointId, string label, bool selected, bool interactable)
        {
            if (!_points.TryGetValue(pointId, out CityMapPointBinding point))
                throw new ArgumentException("Unbound city map point: " + pointId, nameof(pointId));
            point.Render(label, interactable, selected ? _selectedColor : _normalColor);
        }

        public void Render(string status, string heading, string details, string feedback,
            string command, bool selecting, bool paused)
        {
            _status.text = status;
            _heading.text = heading;
            _details.text = details;
            _feedback.text = feedback;
            _primaryLabel.text = command;
            _primary.interactable = !paused;
            _cancel.gameObject.SetActive(selecting);
            _cancel.interactable = !paused;
            _pause.SetIsOnWithoutNotify(paused);
        }

        private void OnPrimary() => PrimaryClicked?.Invoke();
        private void OnCancel() => CancelClicked?.Invoke();
        private void OnPause(bool value)
        {
            _manualPause = value;
            PauseChanged?.Invoke(IsSuspended);
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
