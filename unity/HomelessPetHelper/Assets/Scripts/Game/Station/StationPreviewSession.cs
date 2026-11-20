using System;
using System.Collections.Generic;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.Cleaning;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using HomelessPetHelper.Modules.Station.Habitat;
using HomelessPetHelper.Modules.WorldTime;
using UnityEngine;

namespace HomelessPetHelper.Game.Station
{
    /// <summary>Station presenter. Standalone preview arrivals remain separate from the shared game session.</summary>
    public sealed class StationPreviewSession : IDisposable
    {
        private readonly StationPanelView _view;
        private readonly OptionPreviewText _text;
        private readonly Font _font;
        private readonly StationArtwork _artwork;
        private readonly Dictionary<Guid, StationResidentPresentation> _residents =
            new Dictionary<Guid, StationResidentPresentation>();
        private readonly GameLoopSession _loop;
        private readonly List<StationSceneResident> _sceneResidents = new List<StationSceneResident>();
        private readonly Action _leaveStation;
        private readonly Dictionary<Guid, IReadOnlyList<string>> _sampleAssessments =
            new Dictionary<Guid, IReadOnlyList<string>>();
        private RescueStation _station;
        private ActionWorldClock _clock;
        private StationReturnSummary _summary;
        private Guid _stationSessionId;
        private WorldTimeVersion _cleaningVersion;
        private int _selected = -1;
        private bool _showSummary;
        private StationCareTool _tool;
        private bool _suspended;
        private string _feedback;

        public StationPreviewSession(StationPanelView view, TextAsset text, StationArtwork artwork, Font font,
            GameLoopSession loop = null, Action leaveStation = null)
        {
            _view = view;
            _loop = loop;
            _leaveStation = leaveStation;
            _text = new OptionPreviewText(text);
            _artwork = artwork ?? throw new ArgumentNullException(nameof(artwork));
            _font = font != null ? font : throw new ArgumentNullException(nameof(font));
            _view.ApplyFont(_font);
            _view.CommandRequested += OnCommand;
            _view.AnimalSelected += Select;
            _view.StrokeRequested += Stroke;
            _view.SuspensionChanged += Suspend;
            if (loop == null) Restart();
            else
            {
                _station = loop.Station;
                _clock = loop.Clock;
                _stationSessionId = loop.StationSessionId;
                ShowSharedStation(false);
            }
        }

        private void Restart()
        {
            _station?.Close();
            _clock?.Close();
            Guid session = Guid.NewGuid();
            _stationSessionId = session;
            _station = new RescueStation(session, 4);
            _clock = new ActionWorldClock(session, 1440, 540);
            _sampleAssessments.Clear();
            _residents.Clear();
            if (_view.Habitat != null) _view.Habitat.ClearResidents();
            if (_view.Scene != null) _view.Scene.ClearResidents();
            for (int index = 0; index < 3; index++)
            {
                string kind = index == 0 ? "dog" : "cat";
                var arrival = new StationAnimalIntake(Guid.NewGuid(), Guid.NewGuid(), "preview." + kind,
                    new AnimalIntakeCondition(StationHealthAssessment.Unchecked, index == 1 ? 0 : 65,
                        Array.Empty<string>()));
                if (!_station.TryReceive(session, arrival, out _summary, out StationFailure failure))
                {
                    throw new InvalidOperationException("Invalid station fixture: " + failure);
                }

                // These three fixtures explicitly have no urgent problems; missing results are never healthy.
                _sampleAssessments.Add(arrival.AnimalId, Array.Empty<string>());
                _residents.Add(arrival.AnimalId, StationResidentPresentation.CreateSample(index, _text, _artwork));
            }

            _selected = -1;
            _showSummary = true;
            _tool = StationCareTool.None;
            _feedback = _text.Get("welcome");
            Refresh();
        }

        public void ShowSharedStation(bool showReturn)
        {
            if (_loop == null) throw new InvalidOperationException("This is a standalone fixture.");
            foreach (StationAnimalIntake arrival in _station.Animals)
            {
                if (_residents.ContainsKey(arrival.AnimalId)) continue;
                _residents.Add(arrival.AnimalId, StationResidentPresentation.CreateShared(_station,
                    _stationSessionId, arrival, _clock, _residents.Count, _text, _artwork));
            }
            _summary = _loop.LastArrival;
            _selected = -1;
            _showSummary = showReturn && _summary != null;
            _feedback = _text.Get(_station.Animals.Count == 0 ? "loop.empty" : "stationReady");
            Refresh();
        }

        private StationResidentPresentation Resident(int index) => _residents[_station.Animals[index].AnimalId];

        public void ReportOutingFailure(OutingAssemblyFailure failure)
        {
            _feedback = _text.Get("loop.failure." + failure);
            Refresh();
        }

        private void OnCommand(StationCommand command)
        {
            if (_suspended)
            {
                return;
            }

            switch (command)
            {
                case StationCommand.Restart:
                    if (_loop != null)
                    {
                        if (_selected >= 0 || _showSummary || _tool != StationCareTool.None) return;
                        _leaveStation?.Invoke();
                        return;
                    }
                    Restart();
                    return;
                case StationCommand.Enter:
                    if (!_showSummary) return;
                    _showSummary = false;
                    _feedback = _text.Get("stationReady");
                    break;
                case StationCommand.History:
                    if (_selected >= 0 || _showSummary || _summary == null) return;
                    _showSummary = true;
                    break;
                case StationCommand.Back:
                    if (_selected < 0 || _showSummary) return;
                    if (_tool != StationCareTool.None && !EndCare()) break;
                    _selected = -1;
                    break;
                default:
                    if (_selected < 0 || _showSummary) return;
                    HandleCare(command);
                    break;
            }

            Refresh();
        }

        private void HandleCare(StationCommand command)
        {
            if (command == StationCommand.Finish && _tool != StationCareTool.None)
            {
                EndCare();
            }
            else if (_tool == StationCareTool.None && command == StationCommand.Check)
            {
                CheckSelectedAnimal();
            }
            else if (_tool == StationCareTool.None && command == StationCommand.Pet)
            {
                string response = _loop == null ? Resident(_selected).Contact.Pet() :
                    Current(_selected).CarePermissions.AcceptsTouch && IsCalm(_selected) ? "petted" : "petQuiet";
                _feedback = _text.Get(response, Resident(_selected).Name);
            }
            else if (_tool == StationCareTool.None && command == StationCommand.QuietSpace)
            {
                if (_loop == null) Resident(_selected).Contact.ArrangeQuietSpace(_clock.Current.ElapsedMinutes);
                _feedback = _text.Get("quietArranged", Resident(_selected).Name);
            }
            else if (command == StationCommand.Clean || command == StationCommand.Groom)
            {
                BeginCare(command == StationCommand.Groom);
            }
        }

        private void BeginCare(bool grooming)
        {
            StationCareTool next = grooming ? StationCareTool.Groom : StationCareTool.Wash;
            if (_tool == next) return;
            if (_loop == null && !CanCleanSample(_selected))
            {
                _feedback = _text.Get(Resident(_selected).Contact.CoatRestricted ? "careRestricted" : "careSpace");
                return;
            }
            if (_loop != null)
            {
                StationCoatCareFailure failure = SharedCare().GetBeginFailure(grooming);
                if (failure != StationCoatCareFailure.None)
                {
                    _feedback = _text.Get("care.failure." + failure);
                    return;
                }
            }
            if (_tool != StationCareTool.None && !EndCare()) return;
            if (_loop != null && !SharedCare().TryBegin(grooming, out var beginFailure))
            {
                _feedback = _text.Get("care.failure." + beginFailure);
                return;
            }
            _tool = next;
            _cleaningVersion = _clock.Current.Version;
            _feedback = _text.Get(grooming ? "grooming" : "cleaning", Resident(_selected).Name);
        }

        private void CheckSelectedAnimal()
        {
            StationCareSnapshot care = Care(_selected);
            if (care.BasicAssessment == StationHealthAssessment.Unchecked)
            {
                bool success = _loop != null ?
                    _station.TryAssessKnownHealthyAnimal(_stationSessionId, care.AnimalId, out _) :
                    _sampleAssessments.TryGetValue(care.AnimalId, out var problems) &&
                    _station.TryCompleteBasicAssessment(_stationSessionId, care.AnimalId, problems, out _);
                if (!success)
                {
                    _feedback = _text.Get("assessmentUnavailable");
                    return;
                }
            }

            if (_loop != null && _loop.Flow.Stage == RescueStage.Care &&
                _loop.LastArrival.Arrival.AnimalId == care.AnimalId &&
                !_loop.Outing.TryCompleteCare(_loop.Outing.Session.SessionId, out var failure))
            {
                _feedback = _text.Get("loop.failure." + failure);
                return;
            }

            _feedback = _text.Get("checked", Resident(_selected).Name, _text.Get(HealthKey(_selected)));
        }

        private StationCareSnapshot Care(int index)
        {
            Guid animalId = _station.Animals[index].AnimalId;
            if (!_station.TryGetCareSnapshot(animalId, out var care))
            {
                throw new InvalidOperationException("Station fixture has no care record: " + animalId);
            }

            return care;
        }

        private bool CanCleanSample(int index)
        {
            // Legacy contact fixtures are separate from the shared individual's care rule owner.
            return Care(index).BasicAssessment == StationHealthAssessment.NoUrgentCareRequired &&
                Resident(index).Contact.CanCare;
        }

        private StationCareAvailability CareAvailability(int index)
        {
            if (_loop != null)
            {
                StationCoatCare care = Resident(index).SharedCare;
                return new StationCareAvailability(care.CanBegin(false), care.CanBegin(true));
            }
            bool available = CanCleanSample(index);
            return new StationCareAvailability(available, available);
        }

        private AnimalSnapshot Current(int index)
        {
            if (!_station.TryGetCurrentAnimal(_station.Animals[index].AnimalId, out var current))
                throw new InvalidOperationException("Resident has no current individual.");
            return current;
        }

        private bool IsCalm(int index) => Current(index).Disposition.Stress == AnimalStress.None &&
            Current(index).Disposition.Emotion == AnimalEmotion.Relaxed;

        private StationCoatCare SharedCare() => Resident(_selected).SharedCare;

        private string HealthKey(int index)
        {
            switch (Care(index).BasicAssessment)
            {
                case StationHealthAssessment.Unchecked: return "unchecked";
                case StationHealthAssessment.NoUrgentCareRequired: return "healthy";
                case StationHealthAssessment.UrgentCareRequired: return "urgent";
                default: throw new InvalidOperationException("Unknown station assessment.");
            }
        }

        private bool EndCare()
        {
            if (_loop != null)
            {
                if (!SharedCare().TryEnd(out long settled, out var failure))
                {
                    _feedback = _text.Get("care.failure." + failure);
                    return false;
                }
                _tool = StationCareTool.None;
                _feedback = _text.Get("careSettled", Resident(_selected).Name, settled);
                return true;
            }
            StationResidentPresentation selected = Resident(_selected);
            CareWorkMeter work = _tool == StationCareTool.Wash ? selected.WashWork : selected.GroomWork;
            long minutes = work.PendingMinutes;
            if (minutes > 0 && !_clock.TryAdvance(_cleaningVersion, minutes, out _, out _))
            {
                _feedback = _text.Get("stale");
                return false;
            }

            work.ConfirmSettlement();
            if (_tool == StationCareTool.Wash && Resident(_selected).Dirt.RemovedFraction >= 0.97)
            {
                Resident(_selected).ClearSampleDirtResidue();
                // Completing the last small residue is included in the same capped duration.
                work.ConfirmSettlement();
            }

            foreach (StationResidentPresentation resident in _residents.Values)
            {
                resident.Contact?.ObserveSettledTime(_clock.Current.ElapsedMinutes);
            }

            _feedback = _text.Get("careSettled", Resident(_selected).Name, minutes);
            _tool = StationCareTool.None;
            return true;
        }

        private void Select(int index)
        {
            if (_suspended || _showSummary || _selected >= 0 || index < 0 || index >= _station.Animals.Count)
            {
                return;
            }

            _selected = index;
            _feedback = _text.Get(Care(index).BasicAssessment == StationHealthAssessment.Unchecked ?
                "checkFirst" : "stationReady");
            Refresh();
        }

        private void Stroke(Vector2 point, int strength)
        {
            if (_suspended || _tool == StationCareTool.None || _selected < 0 || _showSummary)
            {
                return;
            }

            IReadOnlyDirtField field = ActiveField();
            if (!field.ContainsCoat(point.x, point.y)) return;
            IReadOnlyDirtField blocker = _tool == StationCareTool.Groom ? Resident(_selected).Dirt : null;
            bool changed = _loop == null ?
                Resident(_selected).TrySampleStroke(_tool == StationCareTool.Groom, point.x, point.y, strength) :
                SharedCare().TryStroke(point.x, point.y, strength);
            if (changed)
            {
                StationResidentPresentation selected = Resident(_selected);
                _view.RefreshDirt(selected.Art, selected.Dirt, Progress(field), true, selected.Fur);
                if (_tool == StationCareTool.Wash) _view.Surface.ShowCleaningContact(point);
            }
            else if (_tool == StationCareTool.Groom && blocker.HasResidueAt(point.x, point.y))
            {
                _feedback = _text.Get("groomBlocked");
                _view.SetFeedback(_feedback);
            }
        }

        private IReadOnlyDirtField ActiveField() => _tool == StationCareTool.Groom ?
            Resident(_selected).Fur : Resident(_selected).Dirt;

        private void Suspend(bool suspended)
        {
            _suspended = suspended;
            if (_loop != null) _loop.SetSuspended(suspended);
            else
            {
                _clock.SetSuspended(suspended);
                _station.SetSuspended(suspended);
            }
            if (_view != null && _view.isActiveAndEnabled)
            {
                Refresh();
            }
        }

        private string Progress(IReadOnlyDirtField dirt)
        {
            string key = _tool == StationCareTool.Groom ? "groomProgress" : "progress";
            return _text.Get(key, dirt.RemovedFraction * 100);
        }

        private string Condition(int index)
        {
            string dirt = Resident(index).Dirt.RemainingAmount == 0 ? "cleaned" :
                Resident(index).Dirt.RemovedFraction > 0 ? "partial" : "dirty";
            return _text.Get("animalStatus", _text.Get(HealthKey(index)), _text.Get(dirt),
                _text.Get(_loop == null ? Resident(index).Contact.SignalKey :
                    Current(index).CarePermissions.AcceptsTouch && IsCalm(index) ? "contactWilling" : "contactQuiet"));
        }

        private void Refresh()
        {
            _view.ShowPage(_showSummary, _selected >= 0, _tool != StationCareTool.None);
            _view.SetSuspended(_suspended);
            RefreshScene();
            if (_loop != null)
            {
                bool canLeave = _tool == StationCareTool.None && !_showSummary && _selected < 0 &&
                    _loop.Flow.Stage == RescueStage.Station && _leaveStation != null;
                _view.SetSessionCommand(_text.Get("loop.outing"), !_suspended && canLeave);
                _view.SetHistoryAvailable(_summary != null && !_suspended && _selected < 0 && !_showSummary);
            }
            WorldTimeSnapshot time = _clock.Current;
            _view.SetStatus(_text.Get("clock", time.DayIndex + 1m, time.MinuteOfDay / 60, time.MinuteOfDay % 60),
                _text.Get("capacity", _station.Animals.Count, _station.TemporaryCapacity),
                _suspended ? _text.Get("paused") : _feedback);
            if (_showSummary)
            {
                StationResidentPresentation resident = _residents[_summary.Arrival.AnimalId];
                _view.ShowSummary(_text.Get("summaryBody", resident.Name, _summary.Arrival.Condition.Dirtiness),
                    resident.Art);
            }
            else if (_selected < 0)
            {
                if (_view.Scene != null) return;
                RefreshHabitat();
                _view.EnsureRosterSize(_station.Animals.Count);
                StationAnimalView[] animals = _view.Animals;
                for (int index = 0; index < animals.Length; index++)
                {
                    animals[index].gameObject.SetActive(index < _station.Animals.Count);
                    if (index >= _station.Animals.Count) continue;
                    animals[index].Refresh(index, Resident(index).Name, Condition(index), Resident(index).Art,
                        Resident(index).Dirt, !_suspended, Resident(index).Fur);
                }
            }
            else
            {
                StationResidentPresentation selected = Resident(_selected);
                IReadOnlyDirtField dirt = selected.Dirt;
                string progress = _tool != StationCareTool.None ? Progress(ActiveField()) :
                    _text.Get("bothProgress", dirt.RemovedFraction * 100, selected.Fur.RemovedFraction * 100);
                _view.ShowDetail(selected.Name, Condition(_selected), progress, selected.Art, dirt,
                    _tool != StationCareTool.None, true, CareAvailability(_selected), _suspended, selected.Fur);
                if (_loop != null) _view.SetQuietSpaceAvailable(false);
            }
        }

        private void RefreshHabitat()
        {
            if (_view.Habitat == null) return;
            for (int index = 0; index < _station.Animals.Count; index++)
            {
                var arrival = _station.Animals[index];
                bool cat = arrival.AnimalDefinitionId == "cat.shy";
                if (!cat || Care(index).BasicAssessment != StationHealthAssessment.NoUrgentCareRequired) continue;
                // Permission is read from the real individual, never granted by presentation time.
                _view.Habitat.ShowResident(arrival.AnimalId, Resident(index).Name,
                    Current(index).CarePermissions.AcceptsTouch && IsCalm(index));
                return;
            }
            _view.Habitat.ShowResident(Guid.Empty, string.Empty, false);
        }

        private void RefreshScene()
        {
            if (_view.Scene == null) return;
            _sceneResidents.Clear();
            for (int index = 0; index < _station.Animals.Count; index++)
            {
                var arrival = _station.Animals[index];
                _station.TryGetCurrentAnimal(arrival.AnimalId, out var current);
                bool observing = StationHabitatPlacement.NeedsObservation(Care(index).BasicAssessment, current);
                bool cat = arrival.AnimalDefinitionId == "cat.shy" || arrival.AnimalDefinitionId == "preview.cat";
                _sceneResidents.Add(new StationSceneResident(arrival.AnimalId, index, Resident(index).Name,
                    Resident(index).Art, cat, observing));
            }
            _view.Scene.Show(_sceneResidents, _selected);
        }

        public void Dispose()
        {
            _view.CommandRequested -= OnCommand;
            _view.AnimalSelected -= Select;
            _view.StrokeRequested -= Stroke;
            _view.SuspensionChanged -= Suspend;
            if (_loop == null)
            {
                _station.Close();
                _clock.Close();
            }
            if (_view != null)
            {
                if (_view.Habitat != null) _view.Habitat.ClearResidents();
                if (_view.Scene != null) _view.Scene.ClearResidents();
                _view.Surface.SetCleaning(false);
                _view.ApplyFont(UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            }
        }
    }
}
