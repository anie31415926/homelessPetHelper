using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game.Guidance;
using HomelessPetHelper.Game.Text;
using UnityEngine;

namespace HomelessPetHelper.Game.UI
{
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
        private bool _focused = true, _backgrounded, _disposed, _completionHandled;
        private bool _saveNeedsReload;
        private int _guideRevision = -1;
        public LevelPopupKind? Top => _stack.Top;
        public int Depth => _stack.Depth;
        public GuideSession Guide => _guide;
        public string PauseCaption => _text.Get("menu.pause");
        public string ObjectivesCaption => _text.Get("menu.objectives");
        private bool CanInteract => !_disposed && _focused && !_backgrounded;

        public LevelOverlaySession(LevelOverlayView view, LevelOverlayBindings bindings, MainAudioSession audio,
            GuidanceProgressSession progress, OptionPreviewText text, Settings.PlaySettingsSession play = null)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            _view = view;
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _audio = audio;
            _play = play;
            _progress = progress;
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _view.Open(this, text);
            var candidate = bindings.CreateGuide();
            if (candidate != null && progress != null && !progress.HasCompleted(candidate.MechanicId))
                _guide = candidate;
            _bindings.BindInput(this, _guide);
            Refresh();
        }

        public void OpenPause()
        {
            if (!CanInteract || _stack.Depth != 0) return;
            _stack.TryOpen(LevelPopupKind.Pause);
            Refresh();
        }

        public void OpenObjectives()
        {
            if (!CanInteract || _stack.Depth != 0) return;
            _view.SetObjectives(_bindings.DescribeObjectives(), _bindings.CreateGuide() != null);
            _stack.TryOpen(LevelPopupKind.Objectives);
            Refresh();
        }

        public void Resume() => CloseTop(LevelPopupKind.Pause);
        public void CloseObjectives() => CloseTop(LevelPopupKind.Objectives);

        public void OpenSettings()
        {
            if (!CanInteract || _audio == null || !_stack.TryPush(LevelPopupKind.Pause, LevelPopupKind.Settings))
                return;
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
            if (!CanInteract) return;
            if (_stack.Depth == 0) _stack.TryOpen(LevelPopupKind.Pause);
            RequestConfirmation(LevelPopupKind.Pause, Confirmation.Return, "confirm.return");
        }

        private void RequestConfirmation(LevelPopupKind parent, Confirmation action, string captionKey)
        {
            if (!CanInteract || !_stack.TryPush(parent, LevelPopupKind.Confirmation)) return;
            _confirmation = action;
            _view.SetConfirmation(_text.Get(captionKey));
            Refresh();
        }

        public void Confirm()
        {
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
            _confirmation = Confirmation.None;
            CloseTop(LevelPopupKind.Confirmation);
        }

        public void Back()
        {
            if (!CanInteract) return;
            if (!Top.HasValue) OpenPause();
            else if (Top == LevelPopupKind.Confirmation) CancelConfirmation();
            else if (Top == LevelPopupKind.Settings) CancelSettings();
            else if (Top == LevelPopupKind.Objectives) CloseObjectives();
            else Resume();
        }

        public void SetApplicationFocus(bool focused)
        { if (_disposed) return; _focused = focused; Interrupt(!focused); }
        public void SetApplicationPaused(bool paused)
        { if (_disposed) return; _backgrounded = paused; Interrupt(paused); }

        private void Interrupt(bool interrupted)
        {
            if (interrupted && _stack.Depth == 0) _stack.TryOpen(LevelPopupKind.Pause);
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
            _bindings.Suspend(_stack.Depth > 0 || !_focused || _backgrounded);
            _view.Refresh(Top, CanInteract);
            _guideRevision = -1;
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
                    _view.ShowNotice(_text.Get(saved ? "guide.complete" : "guide.saveFailed"));
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
            _bindings.BindInput(null, null);
            _bindings.Suspend(false);
            if (_view != null) _view.Close();
        }
    }
}
