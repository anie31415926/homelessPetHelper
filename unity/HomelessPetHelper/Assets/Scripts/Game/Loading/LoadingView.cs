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
        [SerializeField] private Button[] _slots, _deleteSlots;
        [SerializeField] private Button _confirmDelete;
        [SerializeField] private UnityEngine.UI.Text _deleteWarning;
        [SerializeField] private GameObject _savePage, _saveBackdrop;
        [SerializeField] private UnityEngine.UI.Text _saveHeading, _saveFeedback, _entryFeedback;
        private LoadingText _strings;
        private StartupSaveSlots.Info[] _slotInfo;
        private bool _choicesAvailable;
        private Action _confirmDeletion;
        private int _lastPercent = -1;
        public float Progress { get; private set; }
        public event Action EnterRequested;

        public event Action<bool> SaveListRequested;
        public event Action<int, bool> SlotRequested;
        internal event Action<int> DeleteRequested;

        internal enum SlotFeedback { None, Deleted, DeleteFailed }

        private void Awake()
        {
            _loadSave.onClick.AddListener(() => SaveListRequested?.Invoke(false));
            _newGame.onClick.AddListener(() => SaveListRequested?.Invoke(true));
            _closeSlots.onClick.AddListener(CloseSlots);
            _confirmDelete.onClick.AddListener(ConfirmDelete);
            for (int i = 0; i < _slots.Length; i++)
            {
                int slot = i;
                _slots[i].onClick.AddListener(() => SlotRequested?.Invoke(slot,
                    _slotInfo[slot].Status == StartupSaveSlots.State.Empty));
                _deleteSlots[i].onClick.AddListener(() => DeleteRequested?.Invoke(slot));
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

        internal void ShowSlots(StartupSaveSlots.Info[] slots, bool newOnly, bool failed = false,
            SlotFeedback feedback = SlotFeedback.None)
        {
            _slotInfo = slots;
            HideEntry();
            _saveBackdrop.SetActive(true);
            _savePage.SetActive(true);
            _saveHeading.text = newOnly ? _strings.create : _strings.load;
            _confirmDelete.gameObject.SetActive(false);
            _deleteWarning.gameObject.SetActive(false);
            bool anyEmpty = false;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                bool deletable = !newOnly && slot.CanDelete;
                _slots[i].gameObject.SetActive(true);
                _deleteSlots[i].gameObject.SetActive(deletable);
                Label(_deleteSlots[i], _strings.delete);
                StyleDeleteMark(_deleteSlots[i]);
                SetLabelPadding(_slots[i], deletable ? 130f : 36f);
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
            _saveFeedback.text = feedback switch
            {
                SlotFeedback.Deleted => _strings.slotDeleted,
                SlotFeedback.DeleteFailed => _strings.deleteFailed,
                _ => failed ? _strings.slotFailed : newOnly && !anyEmpty ? _strings.slotFull : string.Empty
            };
            Label(_closeSlots, _strings.back);
            SetButtonSpan(_closeSlots, 0.075f, 0.925f);
        }

        internal void ShowDeleteConfirmation(int slot, Action confirmed)
        {
            _confirmDeletion = confirmed;
            _saveHeading.text = _strings.deleteTitle;
            _saveFeedback.text = string.Empty;
            _deleteWarning.text = string.Format(_strings.deleteWarning, string.Format(_strings.slotName, slot + 1));
            _deleteWarning.gameObject.SetActive(true);
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].gameObject.SetActive(false);
                _deleteSlots[i].gameObject.SetActive(false);
            }
            Label(_closeSlots, _strings.cancel);
            SetButtonSpan(_closeSlots, 0.075f, 0.48f);
            Label(_confirmDelete, _strings.deleteConfirm);
            _confirmDelete.gameObject.SetActive(true);
        }

        private void ConfirmDelete()
        {
            var confirmed = _confirmDeletion;
            _confirmDeletion = null;
            confirmed?.Invoke();
        }

        private void Update()
        {
            if (_savePage.activeSelf && Input.GetKeyDown(KeyCode.Escape)) CloseSlots();
        }

        private void CloseSlots()
        {
            if (_confirmDeletion != null)
            {
                ShowSlots(_slotInfo, false);
                return;
            }
            _saveBackdrop.SetActive(false);
            _savePage.SetActive(false);
            _enter.gameObject.SetActive(true);
            _loadSave.gameObject.SetActive(_choicesAvailable);
            _newGame.gameObject.SetActive(_choicesAvailable);
        }

        private static void Label(Button button, string text) => button.GetComponentInChildren<UnityEngine.UI.Text>(true).text = text;

        private static void SetButtonSpan(Button button, float min, float max)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(min, rect.anchorMin.y);
            rect.anchorMax = new Vector2(max, rect.anchorMax.y);
        }

        private static void SetLabelPadding(Button button, float rightPadding)
        {
            var label = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            label.alignment = TextAnchor.MiddleLeft;
            var rect = (RectTransform)label.transform;
            rect.offsetMin = new Vector2(36f, 0f);
            rect.offsetMax = new Vector2(-rightPadding, 0f);
        }

        private static void StyleDeleteMark(Button button)
        {
            var label = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = false;
            label.fontSize = 72;
            label.color = new Color(0.325f, 0.259f, 0.216f, 1f);
            var art = button.GetComponent<Image>();
            if (art != null) art.color = new Color(1f, 1f, 1f, 0f);
            var rect = (RectTransform)label.transform;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

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
            _confirmDeletion = null;
            _enter.gameObject.SetActive(false);
            _loadSave.gameObject.SetActive(false);
            _newGame.gameObject.SetActive(false);
            _savePage.SetActive(false);
            _saveBackdrop.SetActive(false);
            _entryFeedback.gameObject.SetActive(false);
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

        internal void ShowEntryFailure()
        {
            _entryFeedback.text = _strings.slotFailed;
            _entryFeedback.gameObject.SetActive(true);
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
            public string delete, deleteTitle, deleteWarning, deleteConfirm, cancel, slotDeleted, deleteFailed;
        }
    }
}
