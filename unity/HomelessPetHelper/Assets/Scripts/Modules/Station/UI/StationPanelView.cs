using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Animals.UI;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Modules.Station.Cleaning;
using HomelessPetHelper.Framework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI
{
    public sealed class StationPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject _summary;
        [SerializeField] private GameObject _overview;
        [SerializeField] private GameObject _detail;
        [SerializeField] private Text _summaryBody;
        [SerializeField] private Text _clock;
        [SerializeField] private Text _capacity;
        [SerializeField] private Text _name;
        [SerializeField] private Text _condition;
        [SerializeField] private Text _careAvailability;
        [SerializeField] private Text _progress;
        [SerializeField] private Text _feedback;
        [SerializeField] private Image _summaryAnimal;
        [SerializeField] private AnimalCoatView _summaryCoat;
        [SerializeField] private PetPortraitSet _portraits;
        private PetPortraitView _arrivalPortrait;
        [SerializeField] private DirtSurfaceView _surface;
        [SerializeField] private StationAnimalView[] _animals;
        [SerializeField] private RectTransform _rosterContent;
        [SerializeField] private Button _enter;
        [SerializeField] private Button _summaryRename, _detailRename;
        [SerializeField] private Button _back;
        [SerializeField] private Button _check;
        [SerializeField] private Button _pet;
        [SerializeField] private Button _clean;
        [SerializeField] private Button _groom;
        [SerializeField] private Button _quietSpace;
        [SerializeField] private Button _finish;
        [SerializeField] private Button _history;
        [SerializeField] private Button _restart;
        [SerializeField] private Toggle _pause;
        [SerializeField] private CanvasGroup _actions;
        [SerializeField] private Habitat.StationHabitatView _habitat;
        [SerializeField] private Habitat.StationSceneView _scene;
        [SerializeField] private Habitat.StationSceneChrome _chrome;
        private bool _focused = true;
        private bool _applicationPaused;
        private bool _overlaySuspended;
        private bool _renameAvailable;
        private ILevelOverlay _overlay;
        private readonly List<StationAnimalView> _extraAnimals = new List<StationAnimalView>();

        public event Action<StationCommand> CommandRequested;
        public event Action<int> AnimalSelected;
        public event Action<Vector2, int> StrokeRequested;
        public event Action<bool> SuspensionChanged;
        public event Action SettingsRequested;
        public event Action EconomyRequested;
        public event Action MemorialRequested;
        public event Action RenameRequested;

        public DirtSurfaceView Surface => _surface;
        public Habitat.StationHabitatView Habitat => _habitat;
        public Habitat.StationSceneView Scene => _scene;
        public bool IsSceneLayout => _chrome != null;
        public RectTransform MenuTarget => _chrome != null ? _chrome.MenuTarget : null;
        public StationAnimalView[] Animals => _animals.Concat(_extraAnimals).ToArray();

        public void EnsureRosterSize(int count)
        {
            if (count <= _animals.Length + _extraAnimals.Count) return;
            if (_rosterContent == null)
                throw new InvalidOperationException("An expandable station roster needs saved scroll content.");
            while (count > _animals.Length + _extraAnimals.Count)
            {
                StationAnimalView cell = Instantiate(_animals[0], _rosterContent);
                _extraAnimals.Add(cell);
                if (isActiveAndEnabled) cell.Selected += Select;
            }
        }

        public void SetSessionCommand(string label, bool interactive)
        {
            _restart.GetComponentInChildren<Text>().text = label;
            _restart.interactable = interactive;
        }

        public void SetHistoryAvailable(bool available) => _history.interactable = available;
        public void SetRenameLabel(string label)
        {
            if (_summaryRename == null || _detailRename == null) return;
            _renameAvailable = true;
            _summaryRename.GetComponentInChildren<Text>(true).text = label;
            _detailRename.GetComponentInChildren<Text>(true).text = label;
            _summaryRename.gameObject.SetActive(true);
            _detailRename.gameObject.SetActive(true);
        }
        public void SetSettingsAvailable(bool available)
        { if (_chrome != null) _chrome.SetSettingsAvailable(available); }
        public void RequestSettings() => SettingsRequested?.Invoke();
        public void RequestEconomy() => EconomyRequested?.Invoke();
        public void SetEconomyLabel(string label) => _chrome?.SetEconomyLabel(label);
        internal void RequestMemorial() => MemorialRequested?.Invoke();
        public void SetMemorialLabel(string label) => _chrome?.SetMemorialLabel(label);
        public void BindOverlay(ILevelOverlay overlay) => _overlay = overlay;
        public void SetOverlaySuspended(bool suspended)
        {
            _overlaySuspended = suspended;
            _chrome.SetOverlayCovered(suspended);
            RefreshSuspension();
        }
        public void ShowBlockingError(string title, string message)
        {
            _feedback.text = message;
            _chrome?.ShowBlockingError(title, message);
        }
        public void SetQuietSpaceAvailable(bool available) => _quietSpace.gameObject.SetActive(available);
        public void SetAdoptionCommand(string label, bool interactive)
        {
            _quietSpace.GetComponentInChildren<Text>(true).text = label;
            _quietSpace.gameObject.SetActive(true);
            _quietSpace.interactable = interactive;
        }

        public void ApplyFont(Font font)
        {
            foreach (Text label in GetComponentsInChildren<Text>(true))
            {
                label.font = font;
            }
        }

        public void ShowPage(bool summary, bool detail, bool caring = false)
        {
            _surface.SetCleaning(false);
            _summary.SetActive(summary && !_overlaySuspended);
            _overview.SetActive(_chrome != null || (!summary && !detail));
            if (_scene != null) _scene.SetPresentationVisible(!summary && !detail);
            _detail.SetActive(!summary && detail);
            if (_chrome != null) _chrome.SetPage(summary, detail, caring);
        }

        public void ShowSummary(string body, Sprite art, AnimalSnapshot animal = null)
        {
            _summaryBody.text = body;
            _summaryAnimal.sprite = art;
            if (_arrivalPortrait == null) _arrivalPortrait = PetPortraitView.Attach(_summaryAnimal, _portraits);
            if (_arrivalPortrait != null) _arrivalPortrait.Show(_portraits.IsStationDog(art), animal);
            else _summaryCoat.Show(animal);
        }

        public void SetStatus(string clock, string capacity, string feedback)
        {
            _clock.text = clock;
            _capacity.text = capacity;
            _feedback.text = feedback;
            if (_chrome != null) _chrome.Feedback(feedback);
        }

        public void SetFeedback(string feedback)
        {
            _feedback.text = feedback;
            if (_chrome != null) _chrome.Feedback(feedback, true);
        }

        public void DismissDetail() => Back();

        public void ShowDetail(string name, string condition, string progress, Sprite art, IReadOnlyDirtField dirt,
            bool cleaning, bool canFinish, StationCareAvailability care, bool suspended, IReadOnlyDirtField fur = null,
            AnimalSnapshot animal = null)
        {
            _name.text = name;
            if (_detailRename != null) _detailRename.gameObject.SetActive(!cleaning && _renameAvailable);
            _condition.text = condition;
            if (_careAvailability != null)
            {
                _careAvailability.text = care.Explanation;
                _careAvailability.gameObject.SetActive(!cleaning && !string.IsNullOrEmpty(care.Explanation));
            }
            if (_chrome != null)
            {
                _condition.gameObject.SetActive(!cleaning);
                _clean.transform.parent.gameObject.SetActive(!cleaning);
            }
            _progress.text = progress;
            _surface.Refresh(art, dirt, fur);
            _surface.ShowPortrait(_portraits, animal, !cleaning);
            _surface.SetCleaning(cleaning && !suspended);
            _check.gameObject.SetActive(_chrome == null || !cleaning);
            _pet.gameObject.SetActive(_chrome == null || !cleaning);
            _check.interactable = !cleaning;
            _pet.interactable = !cleaning;
            _clean.gameObject.SetActive(_chrome == null || !cleaning);
            _groom.gameObject.SetActive(_chrome == null || !cleaning);
            _quietSpace.gameObject.SetActive(_chrome == null);
            _quietSpace.interactable = !cleaning;
            _clean.interactable = care.CanWash && !suspended;
            _groom.interactable = care.CanGroom && !suspended;
            _finish.gameObject.SetActive(_chrome == null || cleaning);
            _finish.interactable = cleaning && canFinish && !suspended;
        }

        public void RefreshDirt(Sprite art, IReadOnlyDirtField dirt, string progress, bool canFinish,
            IReadOnlyDirtField fur = null)
        {
            _surface.Refresh(art, dirt, fur);
            _progress.text = progress;
            _finish.interactable = canFinish;
        }

        public void SetSuspended(bool suspended)
        {
            _arrivalPortrait?.SetPaused(suspended);
            _surface.SetPortraitPaused(suspended);
            if (_habitat != null) _habitat.SetSuspended(suspended);
            if (_scene != null) _scene.SetSuspended(suspended);
            if (_chrome != null) _chrome.SetSuspended(suspended);
            _actions.interactable = !suspended;
            _actions.alpha = 1;
            _restart.interactable = !suspended;
            if (_summaryRename != null) _summaryRename.interactable = !suspended;
            if (_detailRename != null) _detailRename.interactable = !suspended;
        }

        private void OnEnable()
        {
            _enter.onClick.AddListener(Enter);
            if (_summaryRename != null) _summaryRename.onClick.AddListener(Rename);
            if (_detailRename != null) _detailRename.onClick.AddListener(Rename);
            _back.onClick.AddListener(Back);
            _check.onClick.AddListener(Check);
            _pet.onClick.AddListener(Pet);
            _clean.onClick.AddListener(Clean);
            _groom.onClick.AddListener(Groom);
            _quietSpace.onClick.AddListener(QuietSpace);
            _finish.onClick.AddListener(Finish);
            _history.onClick.AddListener(History);
            _restart.onClick.AddListener(Restart);
            _pause.onValueChanged.AddListener(Pause);
            _surface.StrokeRequested += Stroke;
            if (_scene != null) _scene.Selected += Select;
            foreach (StationAnimalView animal in Animals)
            {
                animal.Selected += Select;
            }
            RefreshSuspension();
        }

        private void OnDisable()
        {
            _enter.onClick.RemoveListener(Enter);
            if (_summaryRename != null) _summaryRename.onClick.RemoveListener(Rename);
            if (_detailRename != null) _detailRename.onClick.RemoveListener(Rename);
            _back.onClick.RemoveListener(Back);
            _check.onClick.RemoveListener(Check);
            _pet.onClick.RemoveListener(Pet);
            _clean.onClick.RemoveListener(Clean);
            _groom.onClick.RemoveListener(Groom);
            _quietSpace.onClick.RemoveListener(QuietSpace);
            _finish.onClick.RemoveListener(Finish);
            _history.onClick.RemoveListener(History);
            _restart.onClick.RemoveListener(Restart);
            _pause.onValueChanged.RemoveListener(Pause);
            _surface.StrokeRequested -= Stroke;
            if (_scene != null) _scene.Selected -= Select;
            foreach (StationAnimalView animal in Animals)
            {
                animal.Selected -= Select;
            }

            _surface.SetCleaning(false);
            SuspensionChanged?.Invoke(true);
        }

        private void Enter() => Request(StationCommand.Enter);
        private void Rename() => RenameRequested?.Invoke();
        private void Back() => Request(StationCommand.Back);
        private void Check() => Request(StationCommand.Check);
        private void Pet() => Request(StationCommand.Pet);
        private void Clean() => Request(StationCommand.Clean);
        private void Groom() => Request(StationCommand.Groom);
        private void QuietSpace() => Request(StationCommand.QuietSpace);
        private void Finish() => Request(StationCommand.Finish);
        private void History() => Request(StationCommand.History);
        private void Restart() => Request(StationCommand.Restart);
        private void Select(int index) => AnimalSelected?.Invoke(index);
        private void Stroke(Vector2 point, int strength) => StrokeRequested?.Invoke(point, strength);

        private void Request(StationCommand command)
        {
            if (_actions.interactable)
            {
                CommandRequested?.Invoke(command);
            }
        }

        private void Pause(bool paused) => RefreshSuspension();

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            _overlay?.SetApplicationFocus(focused);
            RefreshSuspension();
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            _overlay?.SetApplicationPaused(paused);
            RefreshSuspension();
        }

        private void RefreshSuspension()
        {
            SuspensionChanged?.Invoke(_pause.isOn || _overlaySuspended || !_focused ||
                _applicationPaused || !isActiveAndEnabled);
        }
    }
}
