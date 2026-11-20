using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Modules.Station.Cleaning;
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
        [SerializeField] private Text _progress;
        [SerializeField] private Text _feedback;
        [SerializeField] private Image _summaryAnimal;
        [SerializeField] private DirtSurfaceView _surface;
        [SerializeField] private StationAnimalView[] _animals;
        [SerializeField] private RectTransform _rosterContent;
        [SerializeField] private Button _enter;
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
        private readonly List<StationAnimalView> _extraAnimals = new List<StationAnimalView>();

        public event Action<StationCommand> CommandRequested;
        public event Action<int> AnimalSelected;
        public event Action<Vector2, int> StrokeRequested;
        public event Action<bool> SuspensionChanged;

        public DirtSurfaceView Surface => _surface;
        public Habitat.StationHabitatView Habitat => _habitat;
        public Habitat.StationSceneView Scene => _scene;
        public bool IsSceneLayout => _chrome != null;
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
        public void SetQuietSpaceAvailable(bool available) => _quietSpace.gameObject.SetActive(available);

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
            _summary.SetActive(summary);
            _overview.SetActive(!summary && (_chrome != null ? !caring : !detail));
            _detail.SetActive(!summary && detail);
            if (_chrome != null) _chrome.SetPage(summary, detail, caring);
        }

        public void ShowSummary(string body, Sprite art)
        {
            _summaryBody.text = body;
            _summaryAnimal.sprite = art;
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
            if (_chrome != null) _chrome.Feedback(feedback);
        }

        public void DismissDetail() => Back();

        public void ShowDetail(string name, string condition, string progress, Sprite art, IReadOnlyDirtField dirt,
            bool cleaning, bool canFinish, StationCareAvailability care, bool suspended, IReadOnlyDirtField fur = null)
        {
            _name.text = name;
            _condition.text = condition;
            _progress.text = progress;
            _surface.Refresh(art, dirt, fur);
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
            if (_habitat != null) _habitat.SetSuspended(suspended);
            if (_scene != null) _scene.SetSuspended(suspended);
            if (_chrome != null) _chrome.SetSuspended(suspended);
            _actions.interactable = !suspended;
            _actions.alpha = suspended ? 0.45f : 1;
            _restart.interactable = !suspended;
        }

        private void OnEnable()
        {
            _enter.onClick.AddListener(Enter);
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
            RefreshSuspension();
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            RefreshSuspension();
        }

        private void RefreshSuspension()
        {
            SuspensionChanged?.Invoke(_pause.isOn || !_focused || _applicationPaused || !isActiveAndEnabled);
        }
    }
}
