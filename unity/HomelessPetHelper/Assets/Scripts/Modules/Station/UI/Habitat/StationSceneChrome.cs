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
        [SerializeField] private GameObject _menu;
        [SerializeField] private GameObject _footer;
        [SerializeField] private ToastView _notice;
        [SerializeField] private CanvasGroup _detailInput;
        [SerializeField] private MotionScope _motion;
        [SerializeField] private Text _careFeedback;
        [SerializeField] private LayoutElement _careStageSize;
        private bool _summary;
        private string _message;
        private bool _caring;
        public bool MenuIsOpen => _menu.activeSelf;

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
            _backdrop.gameObject.SetActive(_summary || _menu.activeSelf || _detail.gameObject.activeSelf);
            _backdrop.interactable = !_summary && !_caring;
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
            _backdrop.onClick.AddListener(DismissTop);
        }
        private void OnDisable()
        {
            Canvas.willRenderCanvases -= AlignToast;
            _menuButton.onClick.RemoveListener(ToggleMenu);
            _menuClose.onClick.RemoveListener(CloseMenu);
            _history.onClick.RemoveListener(CloseMenu);
            _backdrop.onClick.RemoveListener(DismissTop);
            CloseMenu();
            _notice.Clear();
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
        private void DismissTop()
        {
            if (_menu.activeSelf) CloseMenu();
            else if (!_summary && !_caring) _owner.DismissDetail();
        }
    }
}
