using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Game.Loading
{
    [DisallowMultipleComponent]
    public sealed class LoadingView : MonoBehaviour
    {
        [SerializeField] private Image _fill;
        [SerializeField] private UnityEngine.UI.Text _label;
        [SerializeField] private UnityEngine.UI.Text _percent;
        [SerializeField] private TextAsset _text;
        [SerializeField] private GameObject _cover;
        [SerializeField] private GameObject _previewArtwork;
        [SerializeField] private GameObject _progress;
        [SerializeField] private Button _enter;
        [SerializeField] private UnityEngine.UI.Text _enterLabel;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Button _loadSave, _newGame, _closeSlots;
        [SerializeField] private Button[] _slots;
        [SerializeField] private GameObject _savePage, _saveBackdrop;
        [SerializeField] private UnityEngine.UI.Text _saveHeading, _saveFeedback;
        private LoadingText _strings;
        private StartupSaveSlots.Info[] _slotInfo;
        private bool _choicesAvailable;
        private int _lastPercent = -1;
        public float Progress { get; private set; }
        public event Action EnterRequested;

        public event Action<bool> SaveListRequested;
        public event Action<int, bool> SlotRequested;

        private void Awake()
        {
            _loadSave.onClick.AddListener(() => SaveListRequested?.Invoke(false));
            _newGame.onClick.AddListener(() => SaveListRequested?.Invoke(true));
            _closeSlots.onClick.AddListener(CloseSlots);
            for (int i = 0; i < _slots.Length; i++)
            {
                int slot = i;
                _slots[i].onClick.AddListener(() => SlotRequested?.Invoke(slot,
                    _slotInfo[slot].Status == StartupSaveSlots.State.Empty));
            }
        }

        private void OnEnable() => _enter.onClick.AddListener(OnEnterClicked);
        private void OnDisable() => _enter.onClick.RemoveListener(OnEnterClicked);

        private void OnEnterClicked() => EnterRequested?.Invoke();

        internal void SetContinue(bool hasSave)
        {
            _choicesAvailable = true;
            _enterLabel.text = hasSave ? _strings.resume : _strings.enter;
            Label(_loadSave, _strings.load);
            Label(_newGame, _strings.create);
            _loadSave.gameObject.SetActive(!_savePage.activeSelf);
            _newGame.gameObject.SetActive(!_savePage.activeSelf);
        }

        internal void ShowSlots(StartupSaveSlots.Info[] slots, bool newOnly, bool failed = false)
        {
            _slotInfo = slots;
            HideEntry();
            _saveBackdrop.SetActive(true);
            _savePage.SetActive(true);
            _saveHeading.text = newOnly ? _strings.create : _strings.load;
            bool anyEmpty = false;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var name = string.Format(_strings.slotName, i + 1);
                anyEmpty |= slot.Status == StartupSaveSlots.State.Empty;
                var date = slot.SavedAt?.ToLocalTime().ToString(_strings.dateFormat, CultureInfo.InvariantCulture)
                    ?? _strings.unknown;
                Label(_slots[i], slot.Status == StartupSaveSlots.State.Saved
                    ? string.Format(_strings.slotSaved, name, date, slot.Rescued?.ToString() ?? _strings.unknown)
                    : string.Format(slot.Status == StartupSaveSlots.State.Empty
                        ? _strings.slotEmpty : _strings.slotUnavailable, name));
                _slots[i].interactable = slot.Status == StartupSaveSlots.State.Empty ||
                    !newOnly && slot.Status == StartupSaveSlots.State.Saved;
            }
            _saveFeedback.text = failed ? _strings.slotFailed : newOnly && !anyEmpty ? _strings.slotFull : string.Empty;
            Label(_closeSlots, _strings.back);
        }

        private void Update()
        {
            if (_savePage.activeSelf && Input.GetKeyDown(KeyCode.Escape)) CloseSlots();
        }

        private void CloseSlots()
        {
            _saveBackdrop.SetActive(false);
            _savePage.SetActive(false);
            _enter.gameObject.SetActive(true);
            _loadSave.gameObject.SetActive(_choicesAvailable);
            _newGame.gameObject.SetActive(_choicesAvailable);
        }

        private static void Label(Button button, string text) => button.GetComponentInChildren<UnityEngine.UI.Text>(true).text = text;

        public void ShowCover()
        {
            EnsureText();
            _previewArtwork.SetActive(false);
            _progress.SetActive(false);
            _cover.SetActive(true);
            _enterLabel.text = _strings.enter;
            _enter.gameObject.SetActive(true);
        }

        public void ShowLoading()
        {
            HideEntry();
            _cover.SetActive(false);
            _previewArtwork.SetActive(true);
            _progress.SetActive(true);
            SetOpacity(1f);
        }

        public void HideEntry()
        {
            _enter.gameObject.SetActive(false);
            _loadSave.gameObject.SetActive(false);
            _newGame.gameObject.SetActive(false);
            _savePage.SetActive(false);
            _saveBackdrop.SetActive(false);
        }

        public void SetOpacity(float opacity) => _canvasGroup.alpha = opacity;

        public void SetProgress(float progress)
        {
            EnsureText();
            Progress = Mathf.Clamp01(progress);
            _fill.fillAmount = Progress;
            int percent = Mathf.FloorToInt(Progress * 100f);
            if (percent != _lastPercent)
            {
                _lastPercent = percent;
                _percent.text = percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            }
            _label.text = _strings.loading;
        }

        public void ShowFailure()
        {
            EnsureText();
            HideEntry();
            SetOpacity(1f);
            _progress.SetActive(true);
            _label.text = _strings.failed;
            _percent.text = string.Empty;
            _lastPercent = -1;
        }

        private void EnsureText()
        {
            if (_strings != null) return;
            if (_fill == null || _label == null || _percent == null || _text == null)
                throw new InvalidOperationException("Incomplete Loading view bindings.");
            _strings = JsonUtility.FromJson<LoadingText>(_text.text);
            if (_strings == null || string.IsNullOrWhiteSpace(_strings.loading) ||
                string.IsNullOrWhiteSpace(_strings.failed) || string.IsNullOrWhiteSpace(_strings.enter))
                throw new InvalidOperationException("Invalid Loading localization source.");
        }

        [Serializable]
        private sealed class LoadingText
        {
            public string loading;
            public string failed;
            public string enter, resume, load, create, back;
            public string slotName, slotSaved, slotEmpty, slotUnavailable, slotFailed, slotFull, unknown, dateFormat;
        }
    }
}
