using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Outing.UI
{
    /// <summary>Saved map bindings only; the owner supplies all rules and localized copy.</summary>
    public sealed class CityMapView : MonoBehaviour
    {
        [SerializeField] private Text _status;
        [SerializeField] private Text _heading;
        [SerializeField] private Text _details;
        [SerializeField] private Text _feedback;
        [SerializeField] private Button[] _places;
        [SerializeField] private Button _primary;
        [SerializeField] private Text _primaryLabel;
        [SerializeField] private Button _cancel;
        [SerializeField] private Toggle _pause;

        public event Action<int> PlaceSelected;
        public event Action PrimaryClicked;
        public event Action CancelClicked;
        public event Action<bool> PauseChanged;

        private void Awake()
        {
            if (_status == null || _heading == null || _details == null || _feedback == null ||
                _places == null || _places.Length != 3 || Array.Exists(_places, place => place == null) ||
                _primary == null || _primaryLabel == null || _cancel == null || _pause == null)
                throw new InvalidOperationException("City map has incomplete saved bindings.");
            _places[0].onClick.AddListener(SelectParking);
            _places[1].onClick.AddListener(SelectYard);
            _places[2].onClick.AddListener(SelectPorch);
            _primary.onClick.AddListener(OnPrimary);
            _cancel.onClick.AddListener(OnCancel);
            _pause.onValueChanged.AddListener(OnPause);
        }

        private void OnDestroy()
        {
            _places[0].onClick.RemoveListener(SelectParking);
            _places[1].onClick.RemoveListener(SelectYard);
            _places[2].onClick.RemoveListener(SelectPorch);
            _primary.onClick.RemoveListener(OnPrimary);
            _cancel.onClick.RemoveListener(OnCancel);
            _pause.onValueChanged.RemoveListener(OnPause);
        }

        public void Render(string status, string heading, string details, string feedback,
            string command, int selection, bool selecting, bool paused)
        {
            _status.text = status;
            _heading.text = heading;
            _details.text = details;
            _feedback.text = feedback;
            _primaryLabel.text = command;
            _primary.interactable = !paused;
            _cancel.gameObject.SetActive(selecting);
            _cancel.interactable = !paused;
            _pause.SetIsOnWithoutNotify(paused);
            for (int index = 0; index < _places.Length; index++)
            {
                _places[index].interactable = selecting && !paused;
                _places[index].image.color = index == selection ?
                    new Color32(255, 214, 122, 255) : new Color32(255, 255, 255, 245);
            }
        }

        private void SelectParking() => PlaceSelected?.Invoke(0);
        private void SelectYard() => PlaceSelected?.Invoke(1);
        private void SelectPorch() => PlaceSelected?.Invoke(2);
        private void OnPrimary() => PrimaryClicked?.Invoke();
        private void OnCancel() => CancelClicked?.Invoke();
        private void OnPause(bool value) => PauseChanged?.Invoke(value);
    }
}
