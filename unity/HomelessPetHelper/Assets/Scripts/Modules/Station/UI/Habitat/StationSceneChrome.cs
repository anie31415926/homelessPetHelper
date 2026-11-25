using UnityEngine;
using UnityEngine.UI;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    public sealed class StationSceneChrome : MonoBehaviour
    {
        [SerializeField] private RectTransform _detail;
        [SerializeField] private RectTransform _content;
        [SerializeField] private UiLayoutMetrics _metrics;
        [SerializeField] private StationPanelView _owner;
        [SerializeField] private GameObject _careStage;
        [SerializeField] private GameObject _careCommands;
        [SerializeField] private GameObject _progress;
        [SerializeField] private Button _backdrop;
        [SerializeField] private Button _menuButton;
        [SerializeField] private Button _menuClose;
        [SerializeField] private Button _history;
        [SerializeField] private Button _settings, _economy;
        [SerializeField] private Button _memorial;
        [SerializeField] private GameObject _menu;
        [SerializeField] private GameObject _footer;
        [SerializeField] private ToastView _notice;
        [SerializeField] private CanvasGroup _detailInput;
        [SerializeField] private MotionScope _motion;
        [SerializeField] private Text _careFeedback;
        [SerializeField] private LayoutElement _careStageSize;
        [SerializeField] private GameObject _blockingError;
        [SerializeField] private Text _errorTitle, _errorMessage;
        private bool _overlayCovered, _restoreMenu;
        private bool _summary;
        private string _message;
        private bool _caring;
        public bool MenuIsOpen => _menu.activeSelf;
        public RectTransform MenuTarget => (RectTransform)_menuButton.transform;

        public void SetSettingsAvailable(bool available)
        { if (_settings != null) _settings.gameObject.SetActive(available); }

        public void SetEconomyLabel(string label)
        {
            if (_economy == null) return;
            _economy.GetComponentInChildren<Text>(true).text = label;
            _economy.gameObject.SetActive(true);
        }

        public void SetMemorialLabel(string label)
        {
            if (_memorial == null) return;
            _memorial.GetComponentInChildren<Text>(true).text = label;
            _memorial.gameObject.SetActive(true);
        }

        public void SetOverlayCovered(bool covered)
        {
            if (_overlayCovered == covered) return;
            _overlayCovered = covered;
            _detailInput.alpha = covered ? 0 : 1;
            if (covered) { _restoreMenu = _menu.activeSelf; _menu.SetActive(false); _notice.Clear(); }
            else { _menu.SetActive(_restoreMenu); _restoreMenu = false; }
            RefreshBackdrop();
        }

        public void ShowBlockingError(string title, string message)
        {
            _menu.SetActive(false);
            _restoreMenu = false;
            _notice.Clear();
            _errorTitle.text = title;
            _errorMessage.text = message;
            _blockingError.SetActive(true);
            _menuButton.interactable = false;
            RefreshBackdrop();
        }

        public void SetSuspended(bool suspended)
        {
            _detailInput.interactable = !suspended;
            _motion.SetScenePaused(suspended);
            _notice.SetSuspended(suspended);
        }

        public void SetPage(bool summary, bool detail, bool caring)
        {
            _caring = caring;
            _summary = summary;
            _careStage.SetActive(caring);
            _careCommands.SetActive(!caring);
            _progress.SetActive(caring);
            _careFeedback.gameObject.SetActive(caring && !string.IsNullOrEmpty(_message));
            _footer.SetActive(!summary && !detail);
            if (summary || detail) _menu.SetActive(false);
            if (summary || caring) _notice.Clear();
            RefreshBackdrop();
        }

        private void RefreshBackdrop()
        {
            bool error = _blockingError != null && _blockingError.activeSelf;
            _backdrop.gameObject.SetActive(!_overlayCovered &&
                (_summary || _menu.activeSelf || _detail.gameObject.activeSelf || error));
            _backdrop.interactable = !_summary && !_caring && !error;
        }

        private void LateUpdate()
        {
            if (!_caring) return;
            float feedback = _careFeedback.gameObject.activeSelf ? _careFeedback.preferredHeight + _metrics.gap : 0;
            float available = ((RectTransform)_detail.parent).rect.height * _metrics.popupHeightRatio -
                _metrics.margin * 2 - _metrics.touchSize * 2 - _metrics.gap * 6 - _metrics.bodySize - feedback;
            float height = Mathf.Clamp(available, _metrics.touchSize * 2, 720);
            if (!Mathf.Approximately(_careStageSize.preferredHeight, height))
                _careStageSize.minHeight = _careStageSize.preferredHeight = height;
        }

        private void AlignToast()
        {
            if (!_notice.IsShowing) return;
            var parent = (RectTransform)_notice.transform.parent;
            float bottom = parent.rect.height - _metrics.margin - _metrics.touchSize -
                _metrics.gap - _notice.Height;
            if (_detail.gameObject.activeSelf)
            {
                Vector3 detailTop = _detail.TransformPoint(new Vector3(0, _detail.rect.yMax));
                bottom = parent.InverseTransformPoint(detailTop).y - parent.rect.yMin + _metrics.gap;
                if (bottom + _notice.Height > parent.rect.height - _metrics.margin)
                {
                    Vector3 detailBottom = _detail.TransformPoint(new Vector3(0, _detail.rect.yMin));
                    bottom = parent.InverseTransformPoint(detailBottom).y - parent.rect.yMin -
                        _metrics.gap - _notice.Height;
                }
            }
            _notice.SetBottomInset(bottom);
        }

        public void Feedback(string message, bool repeat = false)
        {
            if (_message == message && !repeat) return;
            _message = message;
            _careFeedback.text = message;
            _careFeedback.gameObject.SetActive(_caring && !string.IsNullOrEmpty(message));
            if (!_caring && !_summary) _notice.Show(message);
        }

        private void OnEnable()
        {
            Canvas.willRenderCanvases += AlignToast;
            _menuButton.onClick.AddListener(ToggleMenu);
            _menuClose.onClick.AddListener(CloseMenu);
            _history.onClick.AddListener(CloseMenu);
            _settings.onClick.AddListener(_owner.RequestSettings);
            if (_economy != null) _economy.onClick.AddListener(_owner.RequestEconomy);
            if (_memorial != null) _memorial.onClick.AddListener(OpenMemorial);
            _backdrop.onClick.AddListener(DismissTop);
        }
        private void OnDisable()
        {
            Canvas.willRenderCanvases -= AlignToast;
            _menuButton.onClick.RemoveListener(ToggleMenu);
            _menuClose.onClick.RemoveListener(CloseMenu);
            _history.onClick.RemoveListener(CloseMenu);
            _settings.onClick.RemoveListener(_owner.RequestSettings);
            if (_economy != null) _economy.onClick.RemoveListener(_owner.RequestEconomy);
            if (_memorial != null) _memorial.onClick.RemoveListener(OpenMemorial);
            _backdrop.onClick.RemoveListener(DismissTop);
            CloseMenu();
            _notice.Clear();
            _blockingError.SetActive(false);
            _menuButton.interactable = true;
            _overlayCovered = _restoreMenu = false;
            _detailInput.alpha = 1;
        }
        private void ToggleMenu()
        {
            _notice.Clear();
            _menu.SetActive(!_menu.activeSelf);
            RefreshBackdrop();
        }
        private void CloseMenu()
        {
            _menu.SetActive(false);
            RefreshBackdrop();
        }
        private void OpenMemorial()
        {
            CloseMenu();
            _owner.RequestMemorial();
        }
        private void DismissTop()
        {
            if (_menu.activeSelf) CloseMenu();
            else if (!_summary && !_caring) _owner.DismissDetail();
        }
    }
}
