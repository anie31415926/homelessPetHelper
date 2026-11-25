using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game.Text;
using UnityEngine;
using UnityEngine.UI;
using UiText = UnityEngine.UI.Text;

namespace HomelessPetHelper.Game.UI
{
    /// <summary>Static shared widget. Stack owner alone decides which page receives input.</summary>
    public sealed class LevelOverlayView : MonoBehaviour
    {
        [SerializeField] private GameObject _modalRoot;
        [SerializeField] private GameObject _modalBackdrop;
        [SerializeField] private CanvasGroup _modalInput;
        [SerializeField] private GameObject _pausePage, _settingsPage, _objectivesPage, _confirmationPage;
        [SerializeField] private Button _resume, _settings, _return, _closeObjectives, _replay;
        [SerializeField] private Button _saveSettings, _cancelSettings, _resetSettings, _confirm, _cancel;
        [SerializeField] private UiText _objectivesText, _confirmationText, _settingsStatus, _notice;
        [SerializeField] private UiText _confirmationTitle, _confirmLabel, _cancelLabel, _browseLabel;
        [SerializeField] private Button _browse;
        [SerializeField] private Image _dialogPortrait;
        [SerializeField] private StationNameInput _stationInput;
        [SerializeField] private UiText _stationInputError;
        [SerializeField] private AudioSettingsView _audio;
        [SerializeField] private Toggle _pauseAtDecision;
        public bool PauseAtDecisionDraft => false;
        public void SetPlaySettings(bool pause)
        { _pauseAtDecision.SetIsOnWithoutNotify(false); _pauseAtDecision.gameObject.SetActive(false); }
        [SerializeField] private GuideMaskGraphic _guideMask;
        [SerializeField] private GameObject _guideCaption;
        [SerializeField] private UiText _guideText;
        [SerializeField] private GuideCaptionLayout _guideLayout;
        [SerializeField] private UiText[] _localizedLabels;
        [SerializeField] private string[] _labelKeys;
        [SerializeField] private GameObject _handbookPage;
        [SerializeField] private Button _openHandbook, _handbookUnits, _handbookDefense, _closeHandbook;
        [SerializeField] private RectTransform _handbookRow;
        private readonly List<RectTransform> _handbookRows = new List<RectTransform>();
        [SerializeField] private ScrollRect _handbookScroll;
        private LevelOverlaySession _session;
        private float _noticeUntil;
        private string _defaultTitle, _defaultConfirm, _defaultCancel;
        private string _stationKeyboardError;
        private TextAnchor _defaultConfirmationAlignment;
        public AudioSettingsView Audio => _audio;
        internal string StationInput
        {
            get { _stationInput.ReadKeyboard(); return _stationInput.text; }
        }

        public void Open(LevelOverlaySession session, OptionPreviewText text)
        {
            if (_session != null) throw new InvalidOperationException("Close the previous overlay context first.");
            if (_localizedLabels.Length != _labelKeys.Length)
                throw new InvalidOperationException("Overlay localization bindings do not match.");
            for (int index = 0; index < _labelKeys.Length; index++)
                _localizedLabels[index].text = _labelKeys[index] == "pause.return"
                    ? session.ReturnCaption : text.Get(_labelKeys[index]);
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _defaultTitle = _confirmationTitle.text;
            _defaultConfirm = _confirmLabel.text;
            _defaultCancel = _cancelLabel.text;
            _stationKeyboardError = text.Get("station.keyboardUnavailable");
            _defaultConfirmationAlignment = _confirmationText.alignment;
            SetConfirmation(string.Empty);
            _noticeUntil = 0;
            Refresh(null, true);
            HideGuide();
        }

        public void Refresh(LevelPopupKind? top, bool canInteract)
        {
            bool showKeyboard = top == LevelPopupKind.Confirmation && !_confirmationPage.activeInHierarchy &&
                _stationInput != null && _stationInput.gameObject.activeSelf && canInteract;
            _modalRoot.SetActive(top.HasValue);
            _modalBackdrop.SetActive(top.HasValue);
            _pausePage.SetActive(top == LevelPopupKind.Pause);
            _settingsPage.SetActive(top == LevelPopupKind.Settings);
            _objectivesPage.SetActive(top == LevelPopupKind.Objectives);
            _handbookPage.SetActive(top == LevelPopupKind.Handbook);
            _confirmationPage.SetActive(top == LevelPopupKind.Confirmation || top == LevelPopupKind.RescueResult);
            _modalInput.interactable = canInteract;
            _modalInput.blocksRaycasts = true;
            if (top.HasValue) HideGuide();
            if (showKeyboard) _stationInput.OpenKeyboard();
        }

        public void SetObjectives(string text, bool canReplay, bool showReplay = true, bool showHandbook = false)
        {
            _objectivesText.text = text;
            _replay.gameObject.SetActive(showReplay);
            _replay.interactable = canReplay;
            _openHandbook.gameObject.SetActive(showHandbook);
        }
        internal void SetHandbook(IReadOnlyList<SiegeHandbook.Entry> entries, bool defense)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (i == _handbookRows.Count)
                    _handbookRows.Add(Instantiate(_handbookRow, _handbookRow.parent, false));
                var row = _handbookRows[i];
                var entry = entries[i];
                row.GetComponentInChildren<UiText>(true).text = entry.Text;
                row.GetComponentInChildren<Modules.Siege.UI.SiegePropIcon>(true).Show(entry.Icon);
                row.GetComponentInChildren<Modules.Siege.UI.SiegeResourceGraphic>(true).gameObject.SetActive(entry.UsesSupply);
                row.gameObject.SetActive(true);
            }
            for (int i = entries.Count; i < _handbookRows.Count; i++) _handbookRows[i].gameObject.SetActive(false);
            _handbookUnits.interactable = defense;
            _handbookDefense.interactable = !defense;
            _handbookScroll.verticalNormalizedPosition = 1;
        }

        public void SetConfirmation(string text)
        {
            _confirmationText.text = text;
            _confirmationText.alignment = _defaultConfirmationAlignment;
            _confirmationTitle.text = _defaultTitle;
            _confirmLabel.text = _defaultConfirm;
            _cancelLabel.text = _defaultCancel;
            _cancel.gameObject.SetActive(true);
            _confirm.interactable = true;
            _confirm.gameObject.SetActive(true);
            _browse.gameObject.SetActive(false);
            _dialogPortrait.sprite = null;
            _dialogPortrait.gameObject.SetActive(false);
            HideStationInput();
        }

        internal void SetRescueResult(string title, string body, string returnLabel)
        {
            SetConfirmation(body);
            _confirmationTitle.text = title;
            _confirmationText.alignment = TextAnchor.MiddleCenter;
            _confirmLabel.text = returnLabel;
            _cancel.gameObject.SetActive(false);
        }

        internal void SetRescueResultReturning(bool returning) => _confirm.interactable = !returning;
        internal void SetRescueResultMessage(string message) => _confirmationText.text = message;

        internal void SetStationDialogBody(string text) => _confirmationText.text = text;

        internal void SetStationDialog(StationDialog dialog)
        {
            _confirmationTitle.text = dialog.Title;
            _confirmationText.text = dialog.Body;
            _confirmationText.alignment = dialog.CenterBody ? TextAnchor.MiddleCenter : _defaultConfirmationAlignment;
            _cancelLabel.text = dialog.CancelLabel;
            _cancel.gameObject.SetActive(!dialog.HideCancel);
            _confirmLabel.text = dialog.ConfirmLabel ?? string.Empty;
            _confirm.interactable = !dialog.Deliberate;
            _confirm.gameObject.SetActive(dialog.Accept != null || dialog.AcceptInput != null);
            _browseLabel.text = dialog.BrowseLabel ?? string.Empty;
            _browse.gameObject.SetActive(dialog.Browse != null);
            _dialogPortrait.sprite = dialog.Portrait;
            _dialogPortrait.gameObject.SetActive(dialog.Portrait != null);
            HideStationInput();
            if (dialog.AcceptInput != null)
            {
                _stationInput.SetTextWithoutNotify(dialog.InitialInput ?? string.Empty);
                _stationInput.gameObject.SetActive(true);
            }
        }

        private void HideStationInput()
        {
            if (_stationInput == null || _stationInputError == null) return;
            _stationInput.CloseKeyboard();
            _stationInput.gameObject.SetActive(false);
            _stationInput.SetTextWithoutNotify(string.Empty);
            ShowInputError(null);
        }

        internal void ShowInputError(string message)
        {
            _stationInputError.text = message ?? string.Empty;
            _stationInputError.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void ClearInputError(string _) => ShowInputError(null);
        private void ShowKeyboardUnavailable() => ShowInputError(_stationKeyboardError);
        public void SetSettingsStatus(string text)
        {
            _settingsStatus.text = text;
            _settingsStatus.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
        public void ShowGuide(string caption, IReadOnlyList<RectTransform> targets)
        {
            _guideMask.SetTargets(targets);
            _guideMask.gameObject.SetActive(true);
            _guideCaption.SetActive(true);
            _guideText.text = caption;
            _guideLayout.SetTargets(targets);
        }
        public void HideGuide()
        { _guideMask.gameObject.SetActive(false); _guideCaption.SetActive(false); }
        internal bool HasNotice => Time.unscaledTime < _noticeUntil;
        public void ShowNotice(string text)
        { _notice.text = text; _noticeUntil = Time.unscaledTime + 4; _notice.gameObject.SetActive(true); }

        public void Close()
        {
            _session = null;
            SetConfirmation(string.Empty);
            _audio.Unbind();
            _modalRoot.SetActive(false);
            _modalBackdrop.SetActive(false);
            HideGuide();
            _noticeUntil = 0;
            _notice.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _resume.onClick.AddListener(Resume);
            _settings.onClick.AddListener(Settings);
            _return.onClick.AddListener(Return);
            _closeObjectives.onClick.AddListener(CloseObjectives);
            _replay.onClick.AddListener(Replay);
            _openHandbook.onClick.AddListener(OpenHandbook);
            _handbookUnits.onClick.AddListener(HandbookUnits);
            _handbookDefense.onClick.AddListener(HandbookDefense);
            _closeHandbook.onClick.AddListener(CloseHandbook);
            _saveSettings.onClick.AddListener(SaveSettings);
            _cancelSettings.onClick.AddListener(CancelSettings);
            _resetSettings.onClick.AddListener(ResetSettings);
            _confirm.onClick.AddListener(Confirm);
            _cancel.onClick.AddListener(Cancel);
            _browse.onClick.AddListener(Browse);
            if (_stationInput != null)
            {
                _stationInput.onValueChanged.AddListener(ClearInputError);
                _stationInput.KeyboardUnavailable += ShowKeyboardUnavailable;
            }
        }

        private void OnDisable()
        {
            _resume.onClick.RemoveListener(Resume);
            _settings.onClick.RemoveListener(Settings);
            _return.onClick.RemoveListener(Return);
            _closeObjectives.onClick.RemoveListener(CloseObjectives);
            _replay.onClick.RemoveListener(Replay);
            _openHandbook.onClick.RemoveListener(OpenHandbook);
            _handbookUnits.onClick.RemoveListener(HandbookUnits);
            _handbookDefense.onClick.RemoveListener(HandbookDefense);
            _closeHandbook.onClick.RemoveListener(CloseHandbook);
            _saveSettings.onClick.RemoveListener(SaveSettings);
            _cancelSettings.onClick.RemoveListener(CancelSettings);
            _resetSettings.onClick.RemoveListener(ResetSettings);
            _confirm.onClick.RemoveListener(Confirm);
            _cancel.onClick.RemoveListener(Cancel);
            _browse.onClick.RemoveListener(Browse);
            if (_stationInput != null)
            {
                _stationInput.onValueChanged.RemoveListener(ClearInputError);
                _stationInput.KeyboardUnavailable -= ShowKeyboardUnavailable;
            }
        }

        private void LateUpdate()
        {
            _session?.RefreshGuidance();
            if (!_confirm.interactable && _session != null && _session.StationConfirmReady)
                _confirm.interactable = true;
            bool visible = Time.unscaledTime < _noticeUntil && !_modalRoot.activeSelf;
            if (_notice.gameObject.activeSelf != visible) _notice.gameObject.SetActive(visible);
            if (Input.GetKeyDown(KeyCode.Escape)) _session?.Back();
        }

        private void Resume() => _session?.Resume();
        private void Settings() => _session?.OpenSettings();
        private void Return() => _session?.RequestReturn();
        private void CloseObjectives() => _session?.CloseObjectives();
        private void OpenHandbook() => _session?.OpenHandbook();
        private void HandbookUnits() => _session?.SelectHandbookTab(false);
        private void HandbookDefense() => _session?.SelectHandbookTab(true);
        private void CloseHandbook() => _session?.CloseHandbook();
        private void Replay() => _session?.ReplayGuidance();
        private void SaveSettings() => _session?.SaveSettings();
        private void CancelSettings() => _session?.CancelSettings();
        private void ResetSettings() => _session?.RequestResetSettings();
        private void Confirm() => _session?.Confirm();
        private void Cancel() => _session?.CancelConfirmation();
        private void Browse() => _session?.BrowseStationDialog();
    }
}
