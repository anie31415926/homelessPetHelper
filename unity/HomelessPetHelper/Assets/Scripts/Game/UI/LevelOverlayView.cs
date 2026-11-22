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
        [SerializeField] private AudioSettingsView _audio;
        [SerializeField] private GuideMaskGraphic _guideMask;
        [SerializeField] private GameObject _guideCaption;
        [SerializeField] private UiText _guideText;
        [SerializeField] private UiText[] _localizedLabels;
        [SerializeField] private string[] _labelKeys;
        private LevelOverlaySession _session;
        private float _noticeUntil;
        public AudioSettingsView Audio => _audio;

        public void Open(LevelOverlaySession session, OptionPreviewText text)
        {
            if (_session != null) throw new InvalidOperationException("Close the previous overlay context first.");
            if (_localizedLabels.Length != _labelKeys.Length)
                throw new InvalidOperationException("Overlay localization bindings do not match.");
            for (int index = 0; index < _labelKeys.Length; index++)
                _localizedLabels[index].text = text.Get(_labelKeys[index]);
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _noticeUntil = 0;
            Refresh(null, true);
            HideGuide();
        }

        public void Refresh(LevelPopupKind? top, bool canInteract)
        {
            _modalRoot.SetActive(top.HasValue);
            _modalBackdrop.SetActive(top.HasValue);
            _pausePage.SetActive(top == LevelPopupKind.Pause);
            _settingsPage.SetActive(top == LevelPopupKind.Settings);
            _objectivesPage.SetActive(top == LevelPopupKind.Objectives);
            _confirmationPage.SetActive(top == LevelPopupKind.Confirmation);
            _modalInput.interactable = canInteract;
            _modalInput.blocksRaycasts = true;
            if (top.HasValue) HideGuide();
        }

        public void SetObjectives(string text, bool canReplay)
        { _objectivesText.text = text; _replay.interactable = canReplay; }
        public void SetConfirmation(string text) => _confirmationText.text = text;
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
        }
        public void HideGuide()
        { _guideMask.gameObject.SetActive(false); _guideCaption.SetActive(false); }
        public void ShowNotice(string text)
        { _notice.text = text; _noticeUntil = Time.unscaledTime + 4; _notice.gameObject.SetActive(true); }

        public void Close()
        {
            _session = null;
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
            _saveSettings.onClick.AddListener(SaveSettings);
            _cancelSettings.onClick.AddListener(CancelSettings);
            _resetSettings.onClick.AddListener(ResetSettings);
            _confirm.onClick.AddListener(Confirm);
            _cancel.onClick.AddListener(Cancel);
        }

        private void OnDisable()
        {
            _resume.onClick.RemoveListener(Resume);
            _settings.onClick.RemoveListener(Settings);
            _return.onClick.RemoveListener(Return);
            _closeObjectives.onClick.RemoveListener(CloseObjectives);
            _replay.onClick.RemoveListener(Replay);
            _saveSettings.onClick.RemoveListener(SaveSettings);
            _cancelSettings.onClick.RemoveListener(CancelSettings);
            _resetSettings.onClick.RemoveListener(ResetSettings);
            _confirm.onClick.RemoveListener(Confirm);
            _cancel.onClick.RemoveListener(Cancel);
        }

        private void LateUpdate()
        {
            _session?.RefreshGuidance();
            bool visible = Time.unscaledTime < _noticeUntil && !_modalRoot.activeSelf;
            if (_notice.gameObject.activeSelf != visible) _notice.gameObject.SetActive(visible);
            if (Input.GetKeyDown(KeyCode.Escape)) _session?.Back();
        }

        private void Resume() => _session?.Resume();
        private void Settings() => _session?.OpenSettings();
        private void Return() => _session?.RequestReturn();
        private void CloseObjectives() => _session?.CloseObjectives();
        private void Replay() => _session?.ReplayGuidance();
        private void SaveSettings() => _session?.SaveSettings();
        private void CancelSettings() => _session?.CancelSettings();
        private void ResetSettings() => _session?.RequestResetSettings();
        private void Confirm() => _session?.Confirm();
        private void Cancel() => _session?.CancelConfirmation();
    }
}
