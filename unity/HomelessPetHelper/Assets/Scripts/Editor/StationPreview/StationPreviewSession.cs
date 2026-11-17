using System;
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
        private readonly bool[] _checked = new bool[3];
        private readonly string[] _names = new string[3];
        private RescueStation _station;
        private ActionWorldClock _clock;
        private StationReturnSummary _summary;
        private WorldTimeVersion _cleaningVersion;
        private int _selected = -1;
        private bool _showSummary;
        private bool _cleaning;
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
            _station = new RescueStation(session, 4);
            _clock = new ActionWorldClock(session, 1440, 540);
            for (int index = 0; index < 3; index++)
            {
                string kind = index == 0 ? "dog" : "cat";
                _art[index] = AssetDatabase.LoadAssetAtPath<Sprite>(StationPreviewAssets.ArtFolder +
                    "/T_Station_" + kind + ".png");
                byte[] mask = AssetDatabase.LoadAssetAtPath<TextAsset>(StationPreviewAssets.ArtFolder +
                    "/T_Station_" + kind + "Mask.bytes").bytes;
                _dirt[index] = CreateDirt(mask, index);
                _names[index] = _text.Get("name" + index);
                _checked[index] = false;
                var arrival = new StationAnimalIntake(Guid.NewGuid(), Guid.NewGuid(), "preview." + kind,
                    new AnimalIntakeCondition(StationHealthAssessment.Unchecked, index == 1 ? 0 : 65,
                        Array.Empty<string>()));
                if (!_station.TryReceive(session, arrival, out _summary, out StationFailure failure))
                {
                    throw new InvalidOperationException("Invalid station fixture: " + failure);
                }
            }

            _selected = -1;
            _showSummary = true;
            _cleaning = false;
            _feedback = _text.Get("welcome");
            Refresh();
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
                    _feedback = _text.Get(_cleaning ? "unfinished" : "stationReady", _names[_selected]);
                    _selected = -1;
                    _cleaning = false;
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
            if (command == StationCommand.Finish)
            {
                Finish();
            }
            else if (!_cleaning && command == StationCommand.Check)
            {
                _checked[_selected] = true;
                _feedback = _text.Get("checked", _names[_selected]);
            }
            else if (!_cleaning && command == StationCommand.Pet)
            {
                _feedback = _text.Get("petted", _names[_selected]);
            }
            else if (!_cleaning && command == StationCommand.Clean && _checked[_selected])
            {
                _cleaning = true;
                _cleaningVersion = _clock.Current.Version;
                _feedback = _text.Get("cleaning", _names[_selected]);
            }
        }

        private void Finish()
        {
            if (!_cleaning || _dirt[_selected].RemovedFraction < 0.97)
            {
                return;
            }

            if (_clock.TryAdvance(_cleaningVersion, 20, out _, out _))
            {
                _dirt[_selected].ClearResidue();
                _feedback = _text.Get("finished", _names[_selected]);
            }
            else
            {
                _feedback = _text.Get("stale");
            }

            _cleaning = false;
        }

        private void Select(int index)
        {
            if (_suspended || _showSummary || _selected >= 0 || index < 0 || index >= 3)
            {
                return;
            }

            _selected = index;
            _feedback = _text.Get(_checked[index] ? "stationReady" : "checkFirst");
            Refresh();
        }

        private void Stroke(Vector2 point, int strength)
        {
            if (_suspended || !_cleaning || _selected < 0 || _showSummary)
            {
                return;
            }

            DirtField dirt = _dirt[_selected];
            if (dirt.TryErase(point.x, point.y, 0.12f, strength))
            {
                _view.RefreshDirt(_art[_selected], dirt, Progress(dirt), dirt.RemovedFraction >= 0.97);
            }
        }

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
            return dirt.RemovedFraction >= 0.97 ? _text.Get("careReady") :
                _text.Get("progress", dirt.RemovedFraction * 100);
        }

        private string Condition(int index)
        {
            string dirt = _dirt[index].RemainingAmount == 0 ? "cleaned" :
                _dirt[index].RemovedFraction > 0 ? "partial" : "dirty";
            return _text.Get("animalStatus", _text.Get(_checked[index] ? "healthy" : "unchecked"), _text.Get(dirt));
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
                        _dirt[index], !_suspended);
                }
            }
            else
            {
                DirtField dirt = _dirt[_selected];
                string progress = _cleaning ? Progress(dirt) :
                    _text.Get(dirt.RemainingAmount == 0 ? "cleaned" : "dirty");
                _view.ShowDetail(_names[_selected], Condition(_selected), progress, _art[_selected], dirt,
                    _cleaning, dirt.RemovedFraction >= 0.97, _checked[_selected], _suspended);
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
