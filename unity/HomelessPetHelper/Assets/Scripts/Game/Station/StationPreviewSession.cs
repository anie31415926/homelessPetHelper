using System;
using System.Collections.Generic;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Progress;
using HomelessPetHelper.Game.UI;
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
        private readonly StationCareRoster _careRoster;
        private readonly Func<bool> _checkpoint;
        private readonly MainProgressSession _progress;
        private readonly StationEconomyMenu _economyMenu;
        private readonly Action<StationDialog> _showDialog;
        private bool _persistenceBlocked;
        private readonly Dictionary<Guid, IReadOnlyList<string>> _sampleAssessments =
            new Dictionary<Guid, IReadOnlyList<string>>();
        private RescueStation _station;
        private ActionWorldClock _clock;
        private StationReturnSummary _summary;
        private Guid _stationSessionId;
        private WorldTimeVersion _cleaningVersion;
        private int _selected = -1;
        private bool _showSummary;
        private bool _firstRescueSummary;
        internal bool CanPresentRescueNotice => !_suspended && !_persistenceBlocked && !_showSummary &&
            _selected < 0 && _tool == StationCareTool.None;
        private StationCareTool _tool;
        private bool _suspended;
        private string _feedback;

        public StationPreviewSession(StationPanelView view, TextAsset text, StationArtwork artwork, Font font,
            GameLoopSession loop = null, Action leaveStation = null, StationCareRoster careRoster = null,
            Func<bool> checkpoint = null, MainProgressSession progress = null,
            Action<StationDialog> showDialog = null)
        {
            _view = view;
            _loop = loop;
            _leaveStation = leaveStation;
            _careRoster = careRoster;
            _checkpoint = checkpoint;
            _progress = progress;
            _showDialog = showDialog;
            _text = new OptionPreviewText(text);
            _artwork = artwork ?? throw new ArgumentNullException(nameof(artwork));
            _font = font != null ? font : throw new ArgumentNullException(nameof(font));
            _view.ApplyFont(_font);
            _view.CommandRequested += OnCommand;
            _view.AnimalSelected += Select;
            _view.StrokeRequested += Stroke;
            _view.SuspensionChanged += Suspend;
            _view.MemorialRequested += OpenMemorial;
            _view.RenameRequested += OpenRename;
            if (progress?.EconomyRules != null && showDialog != null)
            {
                _economyMenu = new StationEconomyMenu(progress, _text, showDialog, Refresh,
                    ReportPersistenceFailure);
                _view.EconomyRequested += OpenEconomy;
                _view.SetEconomyLabel(_text.Get("economy.menu"));
            }
            if (progress != null) _view.SetMemorialLabel(_text.Get("adoption.memorial"));
            if (progress != null) _view.SetRenameLabel(_text.Get("petName.edit"));
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

        public void ShowSharedStation(bool showReturn, bool firstRescue = false)
        {
            if (_loop == null) throw new InvalidOperationException("This is a standalone fixture.");
            foreach (StationAnimalIntake arrival in _station.Animals)
            {
                if (_residents.ContainsKey(arrival.AnimalId)) continue;
                _residents.Add(arrival.AnimalId, StationResidentPresentation.CreateShared(_station,
                    _stationSessionId, arrival, _clock, _residents.Count, _text, _artwork,
                        _careRoster?.Get(arrival.AnimalId),
                    _careRoster?.UsesDogArtwork(arrival.AnimalDefinitionId) ?? arrival.AnimalDefinitionId == "dog"));
                if (_progress != null) _residents[arrival.AnimalId].Name =
                    _progress.GetCustomName(arrival.AnimalId) ?? _text.Get(_progress.DefaultNameKey(arrival));
            }
            _summary = _loop.LastArrival;
            _firstRescueSummary = firstRescue;
            _selected = -1;
            _showSummary = showReturn && _summary != null;
            _feedback = _text.Get(_station.Animals.Count == 0 ? "loop.empty" : "stationReady");
            Refresh();
        }

        private StationResidentPresentation Resident(int index) => _residents[_station.Animals[index].AnimalId];

        public void ReportPersistenceFailure()
        {
            _persistenceBlocked = true;
            _feedback = _text.Get("save.writeFailed");
            Refresh();
            _view.ShowBlockingError(_text.Get("save.errorTitle"), _feedback);
        }

        public void ReportPersistenceRecovery()
        {
            _feedback = _text.Get("save.recovered");
            Refresh();
        }

        public void ReportOutingFailure(OutingAssemblyFailure failure)
        {
            _feedback = _text.Get("loop.failure." + failure);
            Refresh();
        }

        private void OnCommand(StationCommand command)
        {
            if (_suspended || _persistenceBlocked)
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

        private void OpenEconomy()
        {
            if (CanPresentRescueNotice && _progress.CanContinue) _economyMenu?.Open();
        }

        private void HandleCare(StationCommand command)
        {
            if (command == StationCommand.Finish && _tool != StationCareTool.None)
            {
                bool washing = _tool == StationCareTool.Wash;
                if (EndCare() && washing && _progress?.HasCleaningUpgrade == true &&
                    CareAvailability(_selected).CanGroom)
                    OfferGrooming();
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
                if (_progress != null) { OpenAdoption(); return; }
                if (_loop == null) Resident(_selected).Contact.ArrangeQuietSpace(_clock.Current.ElapsedMinutes);
                _feedback = _text.Get("quietArranged", Resident(_selected).Name);
            }
            else if (command == StationCommand.Clean || command == StationCommand.Groom)
            {
                BeginCare(command == StationCommand.Groom);
            }
        }

        private void OfferGrooming()
        {
            _showDialog?.Invoke(new StationDialog
            {
                Title = _text.Get("economy.Cleaning.title"), Body = _text.Get("economy.groomHere"),
                CancelLabel = _text.Get("adoption.later"), ConfirmLabel = _text.Get("economy.groomNow"),
                Accept = () => { BeginCare(true); Refresh(); }
            });
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
            if (_persistenceBlocked) return;
            if (_loop != null && !SharedCare().TryBegin(grooming, out var beginFailure))
            {
                _feedback = _text.Get("care.failure." + beginFailure);
                return;
            }
            _tool = next;
            _cleaningVersion = _clock.Current.Version;
            _feedback = _text.Get(grooming ? "grooming" : "cleaning", Resident(_selected).Name);
            if (_checkpoint != null) _feedback = _text.Get("save.careBoundary", _feedback);
        }

        private void OpenAdoption()
        {
            Guid animalId = _station.Animals[_selected].AnimalId;
            var resident = Resident(_selected);
            string blocked = _progress.AdoptionBlock(animalId);
            if (blocked != null)
            {
                ShowAdoptionMessage("adoption.entry", _text.Get(blocked));
                return;
            }
            if (_progress.IsListed(animalId)) ShowApplicants(animalId, resident, 0);
            else _showDialog(new StationDialog
            {
                Title = _text.Get("adoption.entry"), Body = _text.Get("adoption.publishInfo", resident.Name),
                CancelLabel = _text.Get("adoption.later"), ConfirmLabel = _text.Get("adoption.publish"),
                Accept = () => PublishAdoption(animalId, resident)
            });
        }

        private void OpenRename()
        {
            if (_progress == null || _suspended || _persistenceBlocked || _tool != StationCareTool.None) return;
            Guid animalId = _showSummary ? _summary.Arrival.AnimalId :
                _selected >= 0 ? _station.Animals[_selected].AnimalId : Guid.Empty;
            if (!_residents.TryGetValue(animalId, out var resident)) return;
            _showDialog(new StationDialog
            {
                Title = _text.Get("petName.title"), Body = _text.Get("petName.hint"),
                CancelLabel = _text.Get("petName.cancel"), ConfirmLabel = _text.Get("petName.save"),
                InitialInput = resident.Name, InputError = NameError,
                AcceptInput = name => SaveName(animalId, resident, name.Trim())
            });
        }

        private string NameError(string input)
        {
            string name = input.Trim();
            if (!MainProgressSession.IsValidAnimalName(name)) return _text.Get("petName.invalid");
            foreach (char character in name)
                if (!_font.HasCharacter(character)) return _text.Get("petName.glyph");
            return null;
        }

        private void SaveName(Guid animalId, StationResidentPresentation resident, string name)
        {
            if (name == resident.Name) return;
            if (_progress.TryRenameResident(animalId, name))
            {
                resident.Name = name;
                Refresh();
            }
            else if (!_progress.CanContinue) ReportPersistenceFailure();
        }

        private void PublishAdoption(Guid animalId, StationResidentPresentation resident)
        {
            if (!_progress.TryPublishAdoption(animalId)) { ReportAdoptionFailure(); return; }
            Refresh();
            ShowApplicants(animalId, resident, 0);
        }

        private void ShowApplicants(Guid animalId, StationResidentPresentation resident, int index)
        {
            int applicant = MainProgressSession.Applicant(animalId, index);
            string family = _text.Get("adoption.family." + applicant);
            _showDialog(new StationDialog
            {
                Title = _text.Get("adoption.applications"),
                Body = _text.Get("adoption.application", index + 1, family, _text.Get("adoption.profile." + applicant)),
                CancelLabel = _text.Get("adoption.later"), ConfirmLabel = _text.Get("adoption.pick"),
                BrowseLabel = _text.Get(index == 0 ? "adoption.nextFamily" : "adoption.prevFamily"),
                Browse = () => ShowApplicants(animalId, resident, 1 - index),
                Accept = () => ShowFarewell(animalId, resident, applicant, family)
            });
        }

        private void ShowFarewell(Guid animalId, StationResidentPresentation resident, int applicant, string family)
        {
            _showDialog(new StationDialog
            {
                Title = _text.Get("adoption.farewell"), Portrait = resident.Art,
                Body = _text.Get("adoption.confirmInfo", resident.Name, family),
                CancelLabel = _text.Get("adoption.later"), ConfirmLabel = _text.Get("adoption.send"),
                Deliberate = true, Accept = () => SendForAdoption(animalId, resident, applicant)
            });
        }

        private void SendForAdoption(Guid animalId, StationResidentPresentation resident, int applicant)
        {
            int before = _progress.AidBalance;
            if (!_progress.TrySendForAdoption(animalId, resident.Name, applicant))
            { ReportAdoptionFailure(); return; }
            _residents.Remove(animalId);
            if (_view.Scene != null) _view.Scene.ClearResidents();
            if (_view.Habitat != null) _view.Habitat.ClearResidents();
            ShowSharedStation(false);
            string body = _text.Get("adoption.sent", resident.Name);
            if (_progress.AidBalance > before)
                body += "\n" + _text.Get("economy.amount", _progress.AidBalance - before);
            ShowAdoptionMessage("adoption.completed", body, resident.Art);
        }

        private void ReportAdoptionFailure()
        {
            if (!_progress.CanContinue) ReportPersistenceFailure();
            else ShowAdoptionMessage("adoption.entry", _text.Get("adoption.unavailable"));
        }

        private void OpenMemorial()
        {
            if (_progress == null || _persistenceBlocked || _suspended || _showSummary || _selected >= 0) return;
            if (_progress.MemorialCount == 0)
            { ShowAdoptionMessage("adoption.memorial", _text.Get("adoption.empty")); return; }
            ShowMemorial(_progress.MemorialCount - 1);
        }

        private void ShowMemorial(int index)
        {
            var memorial = _progress.GetMemorial(index);
            bool canBrowse = _progress.MemorialCount > 1;
            _showDialog(new StationDialog
            {
                Title = _text.Get("adoption.memorial"),
                Body = canBrowse ? _text.Get("adoption.memory", memorial.Name, _progress.MemorialCount - index,
                    _progress.MemorialCount) : memorial.Name,
                CenterBody = true,
                Portrait = _artwork.Get(_careRoster.UsesDogArtwork(memorial.Appearance), out _),
                CancelLabel = _text.Get("adoption.close"),
                BrowseLabel = _text.Get("adoption.nextMemory"),
                Browse = canBrowse ?
                    (Action)(() => ShowMemorial(index == 0 ? _progress.MemorialCount - 1 : index - 1)) : null
            });
        }

        private void ShowAdoptionMessage(string titleKey, string body, Sprite art = null) =>
            _showDialog(new StationDialog
            {
                Title = _text.Get(titleKey), Body = body, Portrait = art,
                CancelLabel = _text.Get("adoption.close")
            });

        private void CheckSelectedAnimal()
        {
            StationCareSnapshot care = Care(_selected);
            bool firstCheckForAnimal = care.BasicAssessment == StationHealthAssessment.Unchecked;
            if (firstCheckForAnimal)
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
            int before = _progress?.AidBalance ?? 0;
            if (firstCheckForAnimal) _progress?.RecordFirstAssessment();
            if (_checkpoint?.Invoke() == true && _progress != null && _progress.AidBalance > before)
                _feedback = _text.Get("economy.amount", _progress.AidBalance - before);
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
                var wash = care.GetBeginFailure(false);
                var groom = care.GetBeginFailure(true);
                return new StationCareAvailability(wash == StationCoatCareFailure.None,
                    groom == StationCoatCareFailure.None, DescribeCareAvailability(
                        wash == StationCoatCareFailure.None ? null : _text.Get("care.failure." + wash),
                        groom == StationCoatCareFailure.None ? null : _text.Get("care.failure." + groom)));
            }
            bool available = CanCleanSample(index);
            string reason = available ? null : _text.Get(
                Care(index).BasicAssessment == StationHealthAssessment.Unchecked ? "checkFirst" :
                Care(index).BasicAssessment != StationHealthAssessment.NoUrgentCareRequired ?
                    "care.failure.HealthCareRequired" :
                    Resident(index).Contact.CoatRestricted ? "careRestricted" : "careSpace");
            return new StationCareAvailability(available, available, DescribeCareAvailability(reason, reason));
        }

        private string DescribeCareAvailability(string wash, string groom)
        {
            if (wash == null && groom == null) return string.Empty;
            if (wash == groom) return _text.Get("care.unavailableBoth", wash);
            if (wash == null) return _text.Get("care.unavailableAction", _text.Get("groom"), groom);
            if (groom == null) return _text.Get("care.unavailableAction", _text.Get("clean"), wash);
            return _text.Get("care.unavailableAction", _text.Get("clean"), wash) + "\n" +
                _text.Get("care.unavailableAction", _text.Get("groom"), groom);
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
                return _checkpoint?.Invoke() ?? true;
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
            if (_suspended || _persistenceBlocked || _showSummary || _selected >= 0 || index < 0
                || index >= _station.Animals.Count)
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
            if (_suspended || _persistenceBlocked || _tool == StationCareTool.None || _selected < 0 || _showSummary)
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
            _view.SetSuspended(_suspended || _persistenceBlocked);
            RefreshScene();
            if (_loop != null)
            {
                bool canLeave = _tool == StationCareTool.None && !_showSummary && _selected < 0 &&
                    _loop.Flow.Stage == RescueStage.Station && _leaveStation != null;
                _view.SetSessionCommand(_text.Get("loop.outing"), !_suspended && !_persistenceBlocked && canLeave);
                _view.SetHistoryAvailable(_summary != null && !_suspended && !_persistenceBlocked
                    && _selected < 0 && !_showSummary);
            }
            WorldTimeSnapshot time = _clock.Current;
            _view.SetStatus(_text.Get("clock", time.DayIndex + 1m, time.MinuteOfDay / 60, time.MinuteOfDay % 60),
                _text.Get("capacity", _station.Animals.Count, _station.TemporaryCapacity),
                _suspended && !_persistenceBlocked ? _text.Get("paused") : _feedback);
            if (_showSummary)
            {
                StationResidentPresentation resident = _residents[_summary.Arrival.AnimalId];
                string body = _text.Get(_firstRescueSummary ? "opening.summary" : "summaryBody", resident.Name,
                    _summary.Arrival.Condition.Dirtiness);
                int award = _progress?.RescueAidFor(_summary.Arrival.OutingId) ?? 0;
                if (award > 0) body += "\n" + _text.Get("economy.amount", award);
                _view.ShowSummary(body, resident.Art, _summary.Arrival.AnimalAtArrival);
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
                        Resident(index).Dirt, !_suspended && !_persistenceBlocked, Resident(index).Fur);
                }
            }
            else
            {
                StationResidentPresentation selected = Resident(_selected);
                IReadOnlyDirtField dirt = selected.Dirt;
                string progress = _tool != StationCareTool.None ? Progress(ActiveField()) :
                    _text.Get("bothProgress", dirt.RemovedFraction * 100, selected.Fur.RemovedFraction * 100);
                _view.ShowDetail(selected.Name, Condition(_selected), progress, selected.Art, dirt,
                    _tool != StationCareTool.None, true, CareAvailability(_selected), _suspended
                        || _persistenceBlocked, selected.Fur, Current(_selected));
                if (_progress != null && _tool == StationCareTool.None)
                    _view.SetAdoptionCommand(_text.Get(_progress.IsListed(_station.Animals[_selected].AnimalId)
                        ? "adoption.view" : "adoption.entry"), !_suspended && !_persistenceBlocked);
                else if (_loop != null) _view.SetQuietSpaceAvailable(false);
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
                bool dog = _careRoster?.UsesDogArtwork(arrival.AnimalDefinitionId) ??
                    arrival.AnimalDefinitionId == "dog";
                _sceneResidents.Add(new StationSceneResident(arrival.AnimalId, index, Resident(index).Name,
                    Resident(index).Art, !dog, observing, current, animatedShiba: dog));
            }
            _view.Scene.Show(_sceneResidents, _selected);
        }

        public void Dispose()
        {
            _view.CommandRequested -= OnCommand;
            _view.AnimalSelected -= Select;
            _view.StrokeRequested -= Stroke;
            _view.SuspensionChanged -= Suspend;
            _view.MemorialRequested -= OpenMemorial;
            _view.RenameRequested -= OpenRename;
            _view.EconomyRequested -= OpenEconomy;
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
