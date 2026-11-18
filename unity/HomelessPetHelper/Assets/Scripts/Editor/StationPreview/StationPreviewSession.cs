using System;
using System.Collections.Generic;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.Cleaning;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.WorldTime;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor.StationPreview
{
    /// <summary>Editor-only fixture. These arrivals are samples, never production rescue outcomes.</summary>
    public sealed class StationPreviewSession : IDisposable
    {
        private readonly StationPanelView _view;
        private readonly OptionPreviewText _text;
        private readonly Font _font;
        private readonly Sprite[] _art = new Sprite[3];
        private readonly DirtField[] _dirt = new DirtField[3];
        private readonly DirtField[] _fur = new DirtField[3];
        private readonly CareWorkMeter[] _washWork = new CareWorkMeter[3];
        private readonly CareWorkMeter[] _groomWork = new CareWorkMeter[3];
        private readonly StationContactSample[] _contact = new StationContactSample[3];
        private readonly Dictionary<Guid, IReadOnlyList<string>> _sampleAssessments =
            new Dictionary<Guid, IReadOnlyList<string>>();
        private readonly string[] _names = new string[3];
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

        public StationPreviewSession(StationPanelView view, TextAsset text)
        {
            _view = view;
            _text = new OptionPreviewText(text);
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 36);
            _view.ApplyFont(_font);
            _view.CommandRequested += OnCommand;
            _view.AnimalSelected += Select;
            _view.StrokeRequested += Stroke;
            _view.SuspensionChanged += Suspend;
            Restart();
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
            for (int index = 0; index < 3; index++)
            {
                string kind = index == 0 ? "dog" : "cat";
                string artPath = StationPreviewAssets.CareArtFolder + "/T_Station_" +
                    (index == 0 ? "Dog" : "Cat") + "Temporary";
                _art[index] = AssetDatabase.LoadAssetAtPath<Sprite>(artPath + ".png");
                byte[] mask = AssetDatabase.LoadAssetAtPath<TextAsset>(artPath + "Mask.bytes").bytes;
                _dirt[index] = CreateDirt(mask, index);
                _fur[index] = CreateFur(mask, index);
                _washWork[index] = new CareWorkMeter(_dirt[index], 20);
                _groomWork[index] = new CareWorkMeter(_fur[index], 12, 100);
                _contact[index] = new StationContactSample(index == 1, index != 1, index == 2, index == 0);
                _names[index] = _text.Get("name" + index);
                var arrival = new StationAnimalIntake(Guid.NewGuid(), Guid.NewGuid(), "preview." + kind,
                    new AnimalIntakeCondition(StationHealthAssessment.Unchecked, index == 1 ? 0 : 65,
                        Array.Empty<string>()));
                if (!_station.TryReceive(session, arrival, out _summary, out StationFailure failure))
                {
                    throw new InvalidOperationException("Invalid station fixture: " + failure);
                }

                // These three fixtures explicitly have no urgent problems; missing results are never healthy.
                _sampleAssessments.Add(arrival.AnimalId, Array.Empty<string>());
            }

            _selected = -1;
            _showSummary = true;
            _tool = StationCareTool.None;
            _feedback = _text.Get("welcome");
            Refresh();
        }

        private static DirtField CreateFur(byte[] mask, int index)
        {
            var fur = new byte[mask.Length];
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.07f + index * 13, y * 0.07f + 43);
                    fur[y * 128 + x] = (x + y * 2) % 11 < 2 && y % 9 < 6 ?
                        (byte)(Mathf.Clamp01((noise - 0.38f) * 4) * 200) : (byte)0;
                }
            }

            return new DirtField(128, 128, mask, fur);
        }

        private static DirtField CreateDirt(byte[] mask, int index)
        {
            var dirt = new byte[mask.Length];
            if (index != 1)
            {
                for (int y = 0; y < 128; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        float noise = Mathf.PerlinNoise(x * 0.10f + index * 19, y * 0.10f + 7);
                        dirt[y * 128 + x] = (byte)(Mathf.Clamp01((noise - 0.43f) * 5) * 220);
                    }
                }
            }

            return new DirtField(128, 128, mask, dirt);
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
                    Restart();
                    return;
                case StationCommand.Enter:
                    if (!_showSummary) return;
                    _showSummary = false;
                    _feedback = _text.Get("stationReady");
                    break;
                case StationCommand.History:
                    if (_selected >= 0 || _showSummary) return;
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
                _feedback = _text.Get(_contact[_selected].Pet(), _names[_selected]);
            }
            else if (_tool == StationCareTool.None && command == StationCommand.QuietSpace)
            {
                _contact[_selected].ArrangeQuietSpace(_clock.Current.ElapsedMinutes);
                _feedback = _text.Get("quietArranged", _names[_selected]);
            }
            else if (command == StationCommand.Clean || command == StationCommand.Groom)
            {
                if (!CanCleanSample(_selected))
                {
                    _feedback = _text.Get(_contact[_selected].CoatRestricted ? "careRestricted" : "careSpace");
                    return;
                }

                StationCareTool next = command == StationCommand.Clean ? StationCareTool.Wash : StationCareTool.Groom;
                if (_tool == next || (_tool != StationCareTool.None && !EndCare())) return;
                _tool = next;
                _cleaningVersion = _clock.Current.Version;
                _feedback = _text.Get(next == StationCareTool.Wash ? "cleaning" : "grooming", _names[_selected]);
            }
        }

        private void CheckSelectedAnimal()
        {
            StationCareSnapshot care = Care(_selected);
            if (care.BasicAssessment == StationHealthAssessment.Unchecked)
            {
                if (!_sampleAssessments.TryGetValue(care.AnimalId, out var problems) ||
                    !_station.TryCompleteBasicAssessment(_stationSessionId, care.AnimalId, problems, out _))
                {
                    _feedback = _text.Get("assessmentUnavailable");
                    return;
                }
            }

            _feedback = _text.Get("checked", _names[_selected], _text.Get(HealthKey(_selected)));
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
            // No urgent problem is not blanket permission: current willingness and coat restrictions still apply.
            return Care(index).BasicAssessment == StationHealthAssessment.NoUrgentCareRequired &&
                _contact[index].CanCare;
        }

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
            CareWorkMeter work = _tool == StationCareTool.Wash ? _washWork[_selected] : _groomWork[_selected];
            long minutes = work.PendingMinutes;
            if (minutes > 0 && !_clock.TryAdvance(_cleaningVersion, minutes, out _, out _))
            {
                _feedback = _text.Get("stale");
                return false;
            }

            work.ConfirmSettlement();
            if (_tool == StationCareTool.Wash && _dirt[_selected].RemovedFraction >= 0.97)
            {
                _dirt[_selected].ClearResidue();
                // Completing the last small residue is included in the same capped duration.
                work.ConfirmSettlement();
            }

            foreach (StationContactSample contact in _contact)
            {
                contact.ObserveSettledTime(_clock.Current.ElapsedMinutes);
            }

            _feedback = _text.Get("careSettled", _names[_selected], minutes);
            _tool = StationCareTool.None;
            return true;
        }

        private void Select(int index)
        {
            if (_suspended || _showSummary || _selected >= 0 || index < 0 || index >= 3)
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

            DirtField field = ActiveField();
            if (!field.ContainsCoat(point.x, point.y)) return;
            DirtField blocker = _tool == StationCareTool.Groom ? _dirt[_selected] : null;
            if (field.TryErase(point.x, point.y, 0.12f, strength, blocker))
            {
                _view.RefreshDirt(_art[_selected], _dirt[_selected], Progress(field), true, _fur[_selected]);
                if (_tool == StationCareTool.Wash) _view.Surface.ShowCleaningContact(point);
            }
            else if (_tool == StationCareTool.Groom && blocker.HasResidueAt(point.x, point.y))
            {
                _feedback = _text.Get("groomBlocked");
                _view.SetFeedback(_feedback);
            }
        }

        private DirtField ActiveField() => _tool == StationCareTool.Groom ? _fur[_selected] : _dirt[_selected];

        private void Suspend(bool suspended)
        {
            _suspended = suspended;
            _clock.SetSuspended(suspended);
            _station.SetSuspended(suspended);
            if (_view != null && _view.isActiveAndEnabled)
            {
                Refresh();
            }
        }

        private string Progress(DirtField dirt)
        {
            string key = _tool == StationCareTool.Groom ? "groomProgress" : "progress";
            return _text.Get(key, dirt.RemovedFraction * 100);
        }

        private string Condition(int index)
        {
            string dirt = _dirt[index].RemainingAmount == 0 ? "cleaned" :
                _dirt[index].RemovedFraction > 0 ? "partial" : "dirty";
            return _text.Get("animalStatus", _text.Get(HealthKey(index)), _text.Get(dirt),
                _text.Get(_contact[index].SignalKey));
        }

        private void Refresh()
        {
            _view.ShowPage(_showSummary, _selected >= 0);
            _view.SetSuspended(_suspended);
            long minutes = _clock.Current.ElapsedMinutes;
            _view.SetStatus(_text.Get("clock", minutes / 1440 + 1, minutes % 1440 / 60, minutes % 60),
                _text.Get("capacity", _station.Animals.Count, _station.TemporaryCapacity),
                _suspended ? _text.Get("paused") : _feedback);
            if (_showSummary)
            {
                _view.ShowSummary(_text.Get("summaryBody", _names[2], _summary.Arrival.Condition.Dirtiness), _art[2]);
            }
            else if (_selected < 0)
            {
                StationAnimalView[] animals = _view.Animals;
                for (int index = 0; index < animals.Length; index++)
                {
                    animals[index].Refresh(index, _names[index], Condition(index), _art[index],
                        _dirt[index], !_suspended, _fur[index]);
                }
            }
            else
            {
                DirtField dirt = _dirt[_selected];
                string progress = _tool != StationCareTool.None ? Progress(ActiveField()) :
                    _text.Get("bothProgress", dirt.RemovedFraction * 100, _fur[_selected].RemovedFraction * 100);
                _view.ShowDetail(_names[_selected], Condition(_selected), progress, _art[_selected], dirt,
                    _tool != StationCareTool.None, true, CanCleanSample(_selected), _suspended, _fur[_selected]);
            }
        }

        public void Dispose()
        {
            _view.CommandRequested -= OnCommand;
            _view.AnimalSelected -= Select;
            _view.StrokeRequested -= Stroke;
            _view.SuspensionChanged -= Suspend;
            _station.Close();
            _clock.Close();
            if (_view != null)
            {
                _view.Surface.SetCleaning(false);
                _view.ApplyFont(UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            }
            UnityEngine.Object.Destroy(_font);
        }
    }
}
