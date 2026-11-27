using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game.Guidance;
using HomelessPetHelper.Game.Text;
using UnityEngine;

namespace HomelessPetHelper.Game.UI
{
    public sealed class StationDialog
    {
        internal string Title, Body, ConfirmLabel, CancelLabel, BrowseLabel;
        internal Sprite Portrait;
        internal Action Accept, Browse, Cancel;
        internal string InitialInput;
        internal Func<string, string> InputError;
        internal Action<string> AcceptInput;
        internal bool Deliberate, CenterBody, HideCancel;
    }

    /// <summary>One level owns one popup chain, input guide and draft settings lifetime.</summary>
    public sealed class LevelOverlaySession : ILevelOverlay, IDisposable
    {
        private enum Confirmation { None, Return, ResetAudio, ReplayGuide }
        private readonly PopupStack<LevelPopupKind> _stack = new PopupStack<LevelPopupKind>();
        private readonly LevelOverlayView _view;
        private readonly LevelOverlayBindings _bindings;
        private readonly MainAudioSession _audio;
        private readonly Settings.PlaySettingsSession _play;
        private readonly GuidanceProgressSession _progress;
        private readonly OptionPreviewText _text;
        private readonly List<RectTransform> _targets = new List<RectTransform>(16);
        private GuideSession _guide;
        private AudioPreferences _settingsBefore;
        private Confirmation _confirmation;
        private StationDialog _stationDialog;
        private float _stationConfirmAfter;
        private bool _focused = true, _backgrounded, _disposed, _completionHandled;
        private bool _saveNeedsReload, _rescueResultReturning;
        private readonly bool _settingsOnly, _returnToLocation;
        private int _guideRevision = -1;
        public LevelPopupKind? Top => _stack.Top;
        public int Depth => _stack.Depth;
        /// <summary>Settings keeps music audible for immediate volume preview while gameplay stays paused.</summary>
        public bool IsSettingsOpen => _stack.Contains(LevelPopupKind.Settings);
        public GuideSession Guide => _guide;
        public string PauseCaption => _text.Get("menu.pause");
        public string ObjectivesCaption => _text.Get("menu.objectives");
        internal string ReturnCaption => _text.Get(_returnToLocation ? "pause.returnLocation" : "pause.return");
        private bool CanInteract => !_disposed && _focused && !_backgrounded;
        internal bool StationConfirmReady => (_stationDialog?.Accept != null || _stationDialog?.AcceptInput != null) &&
            Time.unscaledTime >= _stationConfirmAfter;

        public LevelOverlaySession(LevelOverlayView view, LevelOverlayBindings bindings, MainAudioSession audio,
            GuidanceProgressSession progress, OptionPreviewText text, Settings.PlaySettingsSession play = null,
            bool settingsOnly = false, bool returnToLocation = false)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            _view = view;
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _audio = audio;
            _play = play;
            _settingsOnly = settingsOnly;
            _returnToLocation = returnToLocation;
            _progress = progress;
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _view.Open(this, text);
            var candidate = bindings.CreateGuide();
            if (candidate != null && progress != null && !progress.HasCompleted(candidate.MechanicId))
                _guide = candidate;
            _bindings.BindInput(this, _guide);
            Refresh();
            if (_guide != null && _bindings.DescribeHandbook != null) OpenObjectives();
        }

        public void OpenPause()
        {
            if (_settingsOnly || !CanInteract || _stack.Depth != 0) return;
            _stack.TryOpen(LevelPopupKind.Pause);
            Refresh();
        }

        public void OpenObjectives()
        {
            if (_settingsOnly || !CanInteract || _stack.Depth != 0) return;
            _view.SetObjectives(_bindings.DescribeObjectives(), _bindings.CreateGuide() != null,
                showReplay: !_returnToLocation, showHandbook: _bindings.DescribeHandbook != null);
            _stack.TryOpen(LevelPopupKind.Objectives);
            Refresh();
        }

        public void OpenHandbook()
        {
            if (!CanInteract || Top != LevelPopupKind.Objectives || _bindings.DescribeHandbook == null) return;
            if (!_stack.TryPush(LevelPopupKind.Objectives, LevelPopupKind.Handbook)) return;
            _view.SetHandbook(_bindings.DescribeHandbook(false), false);
            Refresh();
        }

        public void SelectHandbookTab(bool defense)
        {
            if (!CanInteract || Top != LevelPopupKind.Handbook) return;
            _view.SetHandbook(_bindings.DescribeHandbook(defense), defense);
        }

        public void CloseHandbook() => CloseTop(LevelPopupKind.Handbook);

        public void Resume() => CloseTop(LevelPopupKind.Pause);
        public void CloseObjectives() => CloseTop(LevelPopupKind.Objectives);

        public void OpenSettings()
        {
            if (!CanInteract || _audio == null || !_stack.TryPush(LevelPopupKind.Pause, LevelPopupKind.Settings))
                return;
            BeginSettingsDraft();
        }

        /// <summary>Opens the shared settings page from an external menu without a level pause page.</summary>
        public void OpenStandaloneSettings()
        {
            if (!_settingsOnly || !CanInteract || _audio == null || _stack.Depth != 0 ||
                !_stack.TryOpen(LevelPopupKind.Settings)) return;
            BeginSettingsDraft();
        }

        private void BeginSettingsDraft()
        {
            _settingsBefore = _audio.Playback.Preferences;
            _view.SetPlaySettings(_play == null || _play.PauseAtDecision);
            _saveNeedsReload = false;
            _view.Audio.Bind(_settingsBefore, ApplyAudio);
            _view.SetSettingsStatus(string.Empty);
            Refresh();
        }

        private void ApplyAudio(AudioPreferences preferences)
        {
            if (!CanInteract || Top != LevelPopupKind.Settings) return;
            _audio.Playback.ApplyPreferences(preferences);
            _view.SetSettingsStatus(string.Empty);
        }

        public void SaveSettings()
        {
            if (!CanInteract || Top != LevelPopupKind.Settings) return;
            if (_saveNeedsReload && !ReloadAudioForRetry())
            { _view.SetSettingsStatus(_text.Get("settings.saveFailed")); return; }
            if (!_audio.TrySavePreferences(out _))
            {
                _saveNeedsReload = true;
                _view.SetSettingsStatus(_text.Get("settings.saveFailed"));
                return;
            }
            // Audio is committed separately. Cancellation must not roll back that successful write.
            _settingsBefore = _audio.Playback.Preferences;
            if (_play != null && !_play.TrySave(_view.PauseAtDecisionDraft))
            { _view.SetSettingsStatus(_text.Get("settings.playSaveFailed")); return; }
            _settingsBefore = null;
            if (_view != null) _view.Audio.Unbind();
            CloseTop(LevelPopupKind.Settings);
        }

        private bool ReloadAudioForRetry()
        {
            var draft = _audio.Playback.Preferences;
            if (!_audio.TryReloadPreferences(out _)) return false;
            var persisted = _audio.Playback.Preferences;
            _audio.Playback.ApplyPreferences(draft);
            // A readback can find our own previously uncertain write. Preserve any unrelated newer settings.
            return persisted.Equals(_settingsBefore) || persisted.Equals(draft);
        }

        public void CancelSettings()
        {
            if (!CanInteract || Top != LevelPopupKind.Settings) return;
            RestoreSettingsDraft();
            CloseTop(LevelPopupKind.Settings);
        }

        public void RequestResetSettings() => RequestConfirmation(LevelPopupKind.Settings,
            Confirmation.ResetAudio, "confirm.resetAudio");

        public void ReplayGuidance() => RequestConfirmation(LevelPopupKind.Objectives,
            Confirmation.ReplayGuide, "confirm.replay");

        public void RequestReturn()
        {
            if (_settingsOnly || !CanInteract) return;
            if (_stack.Depth == 0) _stack.TryOpen(LevelPopupKind.Pause);
            RequestConfirmation(LevelPopupKind.Pause, Confirmation.Return,
                _returnToLocation ? "confirm.returnLocation" : "confirm.return");
        }

        private void RequestConfirmation(LevelPopupKind parent, Confirmation action, string captionKey)
        {
            if (!CanInteract || !_stack.TryPush(parent, LevelPopupKind.Confirmation)) return;
            _confirmation = action;
            _view.SetConfirmation(_text.Get(captionKey));
            Refresh();
        }

        internal void ShowStationDialog(StationDialog dialog, bool replaceWhileInactive = false)
        {
            if (!_settingsOnly || (!CanInteract && !(replaceWhileInactive && !_disposed && _stationDialog != null)) ||
                (_stationDialog == null && !_stack.TryOpen(LevelPopupKind.Confirmation))) return;
            _stationDialog = dialog;
            _stationConfirmAfter = Time.unscaledTime + (dialog.Deliberate ? 0.35f : 0f);
            _view.SetStationDialog(dialog);
            Refresh();
        }

        internal void BrowseStationDialog()
        {
            if (CanInteract && Top == LevelPopupKind.Confirmation) _stationDialog?.Browse?.Invoke();
        }

        internal void ShowRescueResult(string title, string message, string returnLabel)
        {
            if (_disposed || _stack.Contains(LevelPopupKind.RescueResult)) return;
            // A completed battle supersedes any battle menu; keep a single modal backdrop.
            RestoreSettingsDraft();
            _stack.Clear();
            _confirmation = Confirmation.None;
            _stack.TryOpen(LevelPopupKind.RescueResult);
            _view.SetRescueResult(title, message, returnLabel);
            Refresh();
        }

        internal void AllowRescueResultRetry(string message = null)
        {
            if (_disposed || Top != LevelPopupKind.RescueResult) return;
            _rescueResultReturning = false;
            _view.SetRescueResultReturning(false);
            if (!string.IsNullOrEmpty(message)) _view.SetRescueResultMessage(message);
            Refresh();
        }

        public void Confirm()
        {
            if (Top == LevelPopupKind.RescueResult)
            {
                if (!CanInteract || _rescueResultReturning) return;
                _rescueResultReturning = true;
                _view.SetRescueResultReturning(true);
                _bindings.Return();
                return;
            }
            if (CanInteract && Top == LevelPopupKind.Confirmation && _stationDialog != null)
            {
                var dialog = _stationDialog;
                if (!StationConfirmReady) return;
                if (dialog.AcceptInput != null)
                {
                    string input = _view.StationInput;
                    string error = dialog.InputError?.Invoke(input);
                    if (error != null) { _view.ShowInputError(error); return; }
                    CloseStationDialog();
                    dialog.AcceptInput(input);
                    return;
                }
                CloseStationDialog();
                dialog.Accept();
                return;
            }
            if (!CanInteract || Top != LevelPopupKind.Confirmation || _confirmation == Confirmation.None) return;
            Confirmation action = _confirmation;
            _confirmation = Confirmation.None;
            _stack.TryClose(LevelPopupKind.Confirmation);
            if (action == Confirmation.Return)
            {
                _stack.Clear();
                _bindings.Suspend(false);
                _bindings.Return();
                // A business refusal keeps the same level and its overlay owner available for retry.
                if (!_disposed) Refresh();
                return;
            }
            if (action == Confirmation.ResetAudio)
            {
                _audio.Playback.ApplyPreferences(AudioPreferences.Default);
                _view.Audio.Refresh(_audio.Playback.Preferences);
                _view.SetPlaySettings(true);
            }
            else if (action == Confirmation.ReplayGuide)
            {
                _guide?.Cancel();
                _guide = _bindings.CreateGuide();
                _completionHandled = false;
                _guideRevision = -1;
                _bindings.BindInput(this, _guide);
                _stack.TryClose(LevelPopupKind.Objectives);
                if (_guide == null) _view.ShowNotice(_text.Get("guide.targetUnavailable"));
            }
            Refresh();
        }

        public void CancelConfirmation()
        {
            if (!CanInteract || Top != LevelPopupKind.Confirmation) return;
            if (_stationDialog != null)
            {
                if (_stationDialog.HideCancel) return;
                var cancel = _stationDialog.Cancel;
                CloseStationDialog();
                cancel?.Invoke();
                return;
            }
            _confirmation = Confirmation.None;
            CloseTop(LevelPopupKind.Confirmation);
        }

        internal void UpdateStationDialogBody(string text)
        {
            if (_stationDialog == null || Top != LevelPopupKind.Confirmation) return;
            _stationDialog.Body = text;
            _view.SetStationDialogBody(text);
        }

        internal void DismissStationDialog()
        {
            if (_stationDialog != null && Top == LevelPopupKind.Confirmation) CloseStationDialog();
        }

        private void CloseStationDialog()
        {
            _stationDialog = null;
            _view.SetConfirmation(string.Empty);
            CloseTop(LevelPopupKind.Confirmation);
        }

        public void Back()
        {
            if (!CanInteract) return;
            // A dialog without a cancel button owns an operation that must finish (for example a
            // checkpoint write); back must not hide it while the write still commits.
            if (_stationDialog != null && _stationDialog.HideCancel) return;
            if (!Top.HasValue) OpenPause();
            else if (Top == LevelPopupKind.Confirmation) CancelConfirmation();
            else if (Top == LevelPopupKind.Settings) CancelSettings();
            else if (Top == LevelPopupKind.Handbook) CloseHandbook();
            else if (Top == LevelPopupKind.Objectives) CloseObjectives();
            else Resume();
        }

        public void SetApplicationFocus(bool focused)
        { if (_disposed) return; _focused = focused; Interrupt(!focused); }
        public void SetApplicationPaused(bool paused)
        { if (_disposed) return; _backgrounded = paused; Interrupt(paused); }

        private void Interrupt(bool interrupted)
        {
            if (!_settingsOnly && interrupted && _stack.Depth == 0) _stack.TryOpen(LevelPopupKind.Pause);
            Refresh();
        }

        private void CloseTop(LevelPopupKind expected)
        {
            if (!CanInteract || !_stack.TryClose(expected)) return;
            Refresh();
        }

        private void Refresh()
        {
            if (_disposed) return;
            // A won battle is already terminal. Keep the confirmed rescue queryable while the result is visible.
            bool modalPausesBattle = Top.HasValue && Top != LevelPopupKind.RescueResult;
            _bindings.Suspend(modalPausesBattle || !_focused || _backgrounded);
            _view.Refresh(Top, CanInteract);
            _guideRevision = -1;
        }

        internal bool TryShowBattleHint(string key)
        {
            if (!CanInteract || Top.HasValue || _view.HasNotice || _guide is { IsActive: true } ||
                _guide is { IsComplete: true } && !_completionHandled) return false;
            _view.ShowNotice(_text.Get(key));
            return true;
        }

        public void RefreshGuidance()
        {
            if (_disposed || _guide == null || Top.HasValue || !CanInteract) return;
            if (_guide.Revision == _guideRevision) return;
            _guideRevision = _guide.Revision;
            if (!_guide.IsActive)
            {
                _view.HideGuide();
                if (_guide.IsComplete && !_completionHandled)
                {
                    _completionHandled = true;
                    bool saved = _progress == null || _progress.TryRecordCompleted(_guide);
                    if (!saved) _view.ShowNotice(_text.Get("guide.saveFailed"));
                }
                return;
            }
            _targets.Clear();
            foreach (string action in _guide.Current.AllowedActions)
                if (!_bindings.ResolveTarget(action, _targets))
                {
                    _guide.Cancel();
                    _view.HideGuide();
                    _view.ShowNotice(_text.Get("guide.targetUnavailable"));
                    return;
                }
            _targets.Add(_bindings.PauseTarget);
            _targets.Add(_bindings.ObjectivesTarget);
            _view.ShowGuide(_text.Get("guide.step", _guide.StepIndex + 1, _guide.StepCount,
                _text.Get(_guide.Current.CaptionKey)), _targets);
        }

        private void RestoreSettingsDraft()
        {
            if (_settingsBefore != null) _audio.Playback.ApplyPreferences(_settingsBefore);
            _settingsBefore = null;
            if (_view != null) _view.Audio.Unbind();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RestoreSettingsDraft();
            if (_guide != null && _guide.IsComplete && !_completionHandled) _progress?.TryRecordCompleted(_guide);
            _guide?.Cancel();
            _stack.Clear();
            _confirmation = Confirmation.None;
            _stationDialog = null;
            _bindings.BindInput(null, null);
            _bindings.Suspend(false);
            if (_view != null) _view.Close();
        }
    }
}
