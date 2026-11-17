using System;
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
        [SerializeField] private Button _enter;
        [SerializeField] private Button _back;
        [SerializeField] private Button _check;
        [SerializeField] private Button _pet;
        [SerializeField] private Button _clean;
        [SerializeField] private Button _finish;
        [SerializeField] private Button _history;
        [SerializeField] private Button _restart;
        [SerializeField] private Toggle _pause;
        [SerializeField] private CanvasGroup _actions;
        private bool _focused = true;
        private bool _applicationPaused;

        public event Action<StationCommand> CommandRequested;
        public event Action<int> AnimalSelected;
        public event Action<Vector2, int> StrokeRequested;
        public event Action<bool> SuspensionChanged;

        public DirtSurfaceView Surface => _surface;
        public StationAnimalView[] Animals => (StationAnimalView[])_animals.Clone();

        public void ApplyFont(Font font)
        {
            foreach (Text label in GetComponentsInChildren<Text>(true))
            {
                label.font = font;
            }
        }

        public void ShowPage(bool summary, bool detail)
        {
            _surface.SetCleaning(false);
            _summary.SetActive(summary);
            _overview.SetActive(!summary && !detail);
            _detail.SetActive(!summary && detail);
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
        }

        public void ShowDetail(string name, string condition, string progress, Sprite art, DirtField dirt,
            bool cleaning, bool canFinish, bool checkedHealth, bool suspended)
        {
            _name.text = name;
            _condition.text = condition;
            _progress.text = progress;
            _surface.Refresh(art, dirt);
            _surface.SetCleaning(cleaning && !suspended);
            _check.gameObject.SetActive(!cleaning);
            _pet.gameObject.SetActive(!cleaning);
            _clean.gameObject.SetActive(!cleaning);
            _clean.interactable = checkedHealth;
            _finish.gameObject.SetActive(cleaning);
            _finish.interactable = canFinish && !suspended;
        }

        public void RefreshDirt(Sprite art, DirtField dirt, string progress, bool canFinish)
        {
            _surface.Refresh(art, dirt);
            _progress.text = progress;
            _finish.interactable = canFinish;
        }

        public void SetSuspended(bool suspended)
        {
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
            _finish.onClick.AddListener(Finish);
            _history.onClick.AddListener(History);
            _restart.onClick.AddListener(Restart);
            _pause.onValueChanged.AddListener(Pause);
            _surface.StrokeRequested += Stroke;
            foreach (StationAnimalView animal in _animals)
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
            _finish.onClick.RemoveListener(Finish);
            _history.onClick.RemoveListener(History);
            _restart.onClick.RemoveListener(Restart);
            _pause.onValueChanged.RemoveListener(Pause);
            _surface.StrokeRequested -= Stroke;
            foreach (StationAnimalView animal in _animals)
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
