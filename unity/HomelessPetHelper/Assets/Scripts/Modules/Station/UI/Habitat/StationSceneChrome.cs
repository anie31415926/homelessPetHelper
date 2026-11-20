using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    public sealed class StationSceneChrome : MonoBehaviour
    {
        [SerializeField] private RectTransform _detail;
        [SerializeField] private StationPanelView _owner;
        [SerializeField] private GameObject _careStage;
        [SerializeField] private GameObject _careCommands;
        [SerializeField] private GameObject _progress;
        [SerializeField] private Button _backdrop;
        [SerializeField] private Button _menuButton;
        [SerializeField] private Button _menuBackdrop;
        [SerializeField] private Button _history;
        [SerializeField] private GameObject _menu;
        [SerializeField] private GameObject _footer;
        [SerializeField] private CanvasGroup _toast;
        private string _message;
        private float _toastRemaining;
        private bool _suspended;
        public bool MenuIsOpen => _menu.activeSelf;

        public void SetSuspended(bool suspended) => _suspended = suspended;

        public void SetPage(bool summary, bool detail, bool caring)
        {
            _detail.anchorMin = Vector2.zero;
            _detail.anchorMax = new Vector2(1, caring ? 1 : .36f);
            _detail.offsetMin = _detail.offsetMax = Vector2.zero;
            _careStage.SetActive(caring);
            _careCommands.SetActive(!caring);
            _progress.SetActive(caring);
            _backdrop.gameObject.SetActive(detail && !summary && !caring);
            _footer.SetActive(!summary && !detail);
            if (summary || detail) _menu.SetActive(false);
        }

        public void Feedback(string message)
        {
            if (_message == message) return;
            _message = message;
            _toastRemaining = 3.2f;
            _toast.alpha = string.IsNullOrEmpty(message) ? 0 : 1;
        }

        private void OnEnable()
        {
            _menuButton.onClick.AddListener(ToggleMenu);
            _menuBackdrop.onClick.AddListener(CloseMenu);
            _history.onClick.AddListener(CloseMenu);
            _backdrop.onClick.AddListener(_owner.DismissDetail);
        }
        private void OnDisable()
        {
            _menuButton.onClick.RemoveListener(ToggleMenu);
            _menuBackdrop.onClick.RemoveListener(CloseMenu);
            _history.onClick.RemoveListener(CloseMenu);
            _backdrop.onClick.RemoveListener(_owner.DismissDetail);
            CloseMenu();
        }
        private void Update()
        {
            if (_suspended || _toastRemaining <= 0) return;
            _toastRemaining -= Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (_toastRemaining <= 0) _toast.alpha = 0;
        }
        private void ToggleMenu() => _menu.SetActive(!_menu.activeSelf);
        private void CloseMenu() => _menu.SetActive(false);
    }
}
