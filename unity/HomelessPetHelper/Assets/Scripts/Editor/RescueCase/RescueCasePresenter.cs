using System;
using System.Linq;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.RescueCase
{
    public sealed class RescueCasePresenter : IDisposable
    {
        private readonly RescueCaseView _view;
        private readonly OptionPreviewText _text;
        private readonly RescueOptionContentLoader _content;
        private readonly Font _font;
        private bool _manualPause;
        private bool _focusPause;
        private bool _handbookOpen;
        private bool _disposed;
        private string _error = string.Empty;
        private CaseStart _start;

        public RescueCaseTrial Trial { get; private set; }

        public RescueCasePresenter(RescueCaseView view, TextAsset text, RescueOptionContentLoader content,
            CaseStart start = CaseStart.Normal)
        {
            ValidateStart(start);
            _start = start;
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _text = new OptionPreviewText(text);
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _view.SetHoldHint(_text.Get("holdHint"));
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 36);
            foreach (Text label in _view.GetComponentsInChildren<Text>(true))
            {
                label.font = _font;
            }

            _view.Options.UseRequested += Submit;
            _view.Options.RetentionRequested += Retain;
            _view.Options.PauseRequested += Pause;
            _view.Options.RestartRequested += Restart;
            _view.AdvanceRequested += Advance;
            _view.HandbookRequested += ToggleHandbook;
            _view.AssistanceRequested += Assist;
            Application.focusChanged += Focus;
            Restart();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Application.focusChanged -= Focus;
            Trial.Dispose();
            if (_view != null)
            {
                _view.Options.UseRequested -= Submit;
                _view.Options.RetentionRequested -= Retain;
                _view.Options.PauseRequested -= Pause;
                _view.Options.RestartRequested -= Restart;
                _view.AdvanceRequested -= Advance;
                _view.HandbookRequested -= ToggleHandbook;
                _view.AssistanceRequested -= Assist;
                _view.Options.Close();
            }

            if (_font != null)
            {
                UnityEngine.Object.Destroy(_font);
            }
        }

        private void Restart()
        {
            Trial?.Dispose();
            Trial = new RescueCaseTrial(Guid.NewGuid(), 1731, start: _start, catalog: _content.Catalog);
            _manualPause = false;
            _handbookOpen = false;
            _error = string.Empty;
            _view.Options.Open(Trial.Rules.UseLimit, Trial.Rules.LockLimit);
            Trial.SetSuspended(_focusPause);
            Render();
        }

        public void StartScenario(CaseStart start)
        {
            ValidateStart(start);
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RescueCasePresenter));
            }

            _start = start;
            Restart();
        }

        private static void ValidateStart(CaseStart start)
        {
            if (start != CaseStart.Normal && start != CaseStart.StressRecovery)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
        }

        private void Submit()
        {
            Trial.TrySubmit(Trial.CurrentTurn, _view.Options.SelectedIds, out var failure);
            Report(failure);
        }

        private void Retain(long id, bool retained)
        {
            Trial.TrySetLocked(Trial.CurrentTurn, id, retained, out var failure);
            Report(failure);
        }

        private void Advance()
        {
            CaseFailure failure;
            switch (Trial.Stage)
            {
                case CaseStage.Responding:
                    Trial.TryContinue(Trial.CurrentTurn, out failure);
                    break;
                case CaseStage.ReadyForRescue:
                    Trial.TryFinishRescue(Trial.CurrentTurn, out failure);
                    break;
                case CaseStage.Approachable:
                    Trial.TryPrepareRescue(Trial.CurrentTurn, out failure);
                    break;
                case CaseStage.Summary:
                    Trial.TryReturnToStation(Trial.CurrentTurn, out failure);
                    break;
                case CaseStage.Station:
                    if (Trial.StationCare.BasicAssessment == StationHealthAssessment.Unchecked)
                    {
                        Trial.TryCheckAtStation(Trial.CurrentTurn, out failure);
                    }
                    else
                    {
                        Trial.TryInteract(Trial.CurrentTurn, out failure);
                    }
                    break;
                default:
                    failure = CaseFailure.UnexpectedStage;
                    break;
            }

            Report(failure);
        }

        private void Assist()
        {
            Trial.TryRequestAssistance(Trial.CurrentTurn, out var failure);
            Report(failure);
        }

        private void ToggleHandbook()
        {
            _handbookOpen = !_handbookOpen;
            Render();
        }

        private void Pause(bool paused)
        {
            _manualPause = paused;
            ApplyPause();
        }

        private void Focus(bool focused)
        {
            _focusPause = !focused;
            ApplyPause();
        }

        private void ApplyPause()
        {
            Trial.SetSuspended(_manualPause || _focusPause);
            Render();
        }

        private void Report(CaseFailure failure)
        {
            _error = failure == CaseFailure.None ? string.Empty : _text.Get("failure." + failure);
            Render();
        }

        private void Render()
        {
            CaseAnimalSnapshot animal = Trial.Animal;
            var titles = Trial.Options.Select(option => animal.HasSpace && option.Definition.Id == "give-space" ?
                _text.Get("maintain-space") : _content.GetText(_content.Catalog.Get(option.Definition.Id).TitleKey))
                .ToArray();
            var reasons = Trial.Options.Select(option => Trial.GetOptionFailure(option.InstanceId))
                .Select(failure => failure == CaseFailure.None ? string.Empty : _text.Get("unavailable." + failure))
                .ToArray();
            _view.Options.Refresh(Trial.Options, titles, _font, reasons);
            string responseKey = Trial.LastResponse == CaseResponse.None && Trial.Start == CaseStart.StressRecovery ?
                "response.StressStart" : "response." + Trial.LastResponse;
            string feedback = string.IsNullOrEmpty(_error) ? GetFeedback(responseKey) : _error;
            _view.Options.SetStatus(_text.Get("turn", Trial.CurrentTurn.TurnNumber, Trial.FoodRemaining),
                _text.Get("retained", Trial.Options.Count(option => option.IsLocked)),
                Trial.IsSuspended ? _text.Get("paused") : feedback,
                Trial.IsSuspended, Trial.Stage == CaseStage.Choosing);
            _view.SetAnimal(_text.Get("animal", _text.Get(animal.IsHungry ? "hungry" : "fed"),
                _text.Get("emotion." + animal.Emotion), _text.Get("stress." + animal.Stress),
                _text.Get("behavior." + animal.Behavior)),
                _text.Get("scenario." + Trial.Start) + " · " + _text.Get("stage." + Trial.Stage) + "\n" +
                (Trial.Stage == CaseStage.Choosing ? _text.Get("intent." + Trial.Intent) : string.Empty));
            string details = _handbookOpen ? _text.Get("handbook.body") : _text.Get("details." + Trial.Stage);
            if (!_handbookOpen && Trial.Stage == CaseStage.Station)
            {
                details = _text.Get(Trial.StationCare.NecessaryCareIsArranged ?
                    "station.ready" : "station.unchecked");
            }

            _view.SetDetails(details == "-" ? string.Empty : details);
            string advance = _text.Get(Trial.Stage == CaseStage.Station &&
                Trial.StationCare.BasicAssessment == StationHealthAssessment.Unchecked ?
                "advance.CheckAtStation" : "advance." + Trial.Stage);
            _view.SetCommands(advance == "-" ? string.Empty : advance,
                !Trial.IsSuspended && !Trial.HasInteracted,
                !Trial.IsSuspended && (Trial.Stage == CaseStage.Choosing || Trial.Stage == CaseStage.Responding ||
                    Trial.Stage == CaseStage.ReadyForRescue || Trial.Stage == CaseStage.Approachable),
                Trial.Stage == CaseStage.Choosing);
        }

        private string GetFeedback(string responseKey)
        {
            CaseTurnResult result = Trial.LastTurnResult;
            if (result == null || string.IsNullOrEmpty(result.FeedbackKey) ||
                (Trial.Stage != CaseStage.Responding && Trial.Stage != CaseStage.Choosing))
            {
                return _text.Get(responseKey);
            }

            string prompt = _content.GetText(result.FeedbackKey);
            return result.AnimalResponse == CaseResponse.NoNewResponse ? prompt :
                prompt + "\n" + _text.Get(responseKey);
        }
    }
}
