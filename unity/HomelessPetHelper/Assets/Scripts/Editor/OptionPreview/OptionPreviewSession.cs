using System;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.Options;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.OptionPreview
{
    /// <summary>
    /// Editor-only option fixture. Deterministic supplies and immediate response completion are
    /// intentionally not production draw rules, animal behavior, safe-action evaluation or rewards.
    /// </summary>
    public sealed class OptionPreviewSession : IDisposable
    {
        private readonly OptionPanelView _view;
        private readonly int _offerCount;
        private readonly int _lockLimit;
        private static readonly string[] ActionIds =
        {
            "give-space", "wait", "food", "toy", "remove-noise", "approach"
        };

        private RescueOptionHand _hand;
        private RescueOptionRules _rules;
        private OptionPreviewText _text;
        private Font _font;
        private string _feedback;
        private bool _manualPause;
        private bool _focusPause;
        private bool _disposed;

        public RescueOptionHand Hand => _hand;

        public OptionPreviewSession(OptionPanelView view, TextAsset textSource, int offerCount = 3, int lockLimit = 2)
        {
            _view = view;
            _offerCount = offerCount;
            _lockLimit = lockLimit;
            _text = new OptionPreviewText(textSource);
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 36);
            foreach (Text text in _view.GetComponentsInChildren<Text>(true))
            {
                text.font = _font;
            }

            _view.UseRequested += OnUseRequested;
            _view.RetentionRequested += OnRetentionRequested;
            _view.RestartRequested += Restart;
            _view.PauseRequested += OnPauseRequested;
            Application.focusChanged += OnApplicationFocus;
            Restart();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Application.focusChanged -= OnApplicationFocus;
            if (_view != null)
            {
                _view.UseRequested -= OnUseRequested;
                _view.RetentionRequested -= OnRetentionRequested;
                _view.RestartRequested -= Restart;
                _view.PauseRequested -= OnPauseRequested;
                _view.Close();
            }

            _hand?.Close();
            if (_font != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_font);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_font);
                }
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            _focusPause = !focused;
            ApplyPause();
        }

        private void Restart()
        {
            _hand?.Close();
            _manualPause = false;
            _rules = new RescueOptionRules(_offerCount, 1, _lockLimit);
            _hand = new RescueOptionHand(Guid.NewGuid(), _rules);
            _view.Open(_rules.UseLimit, _rules.LockLimit);
            _feedback = _text.Get("ready");
            SupplyNextTurn();
            ApplyPause();
        }

        private void OnUseRequested()
        {
            if (!_hand.TryConsumeSelection(_hand.CurrentTurn, _view.SelectedIds, out var consumed, out var failure))
            {
                _feedback = _text.Get("rejected", failure.ToString());
                Render();
                return;
            }

            _feedback = _text.Get("used", _text.Get(consumed[0].Definition.Id));
            SupplyNextTurn();
            Render();
        }

        private void OnRetentionRequested(long id, bool retained)
        {
            if (!_hand.TrySetLocked(_hand.CurrentTurn, id, retained, out var failure))
            {
                _feedback = _text.Get("rejected", failure.ToString());
            }

            Render();
        }

        private void OnPauseRequested(bool paused)
        {
            _manualPause = paused;
            ApplyPause();
        }

        private void ApplyPause()
        {
            if (_hand == null)
            {
                return;
            }

            _hand.SetSuspended(_manualPause || _focusPause);
            Render();
        }

        private void SupplyNextTurn()
        {
            int count = _rules.OfferCount - _hand.Options.Count(option => option.IsLocked);
            int offset = _hand.CurrentTurn.TurnNumber;
            var definitions = Enumerable.Range(0, count)
                .Select(index => new RescueActionDefinition(ActionIds[(offset + index) % ActionIds.Length])).ToArray();
            var nextTurn = new OptionTurnId(_hand.CurrentTurn.SessionId, _hand.CurrentTurn.TurnNumber + 1);
            if (!_hand.TryRefresh(nextTurn, definitions, ActionIds, out var failure))
            {
                throw new InvalidOperationException("Preview fixture refresh rejected: " + failure);
            }
        }

        private void Render()
        {
            var titles = _hand.Options.Select(option => _text.Get(option.Definition.Id)).ToArray();
            _view.Refresh(_hand.Options, titles, _font);
            _view.SetStatus(_text.Get("turn", _hand.CurrentTurn.TurnNumber),
                _text.Get("retained", _hand.Options.Count(option => option.IsLocked), _rules.LockLimit),
                _hand.IsSuspended ? _text.Get("paused") : _feedback, _hand.IsSuspended);
        }
    }
}
