using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Rescue.UI
{
    // Presentation bindings shared by the saved Editor trial panel; no trial rules live here.
    public sealed class RescueCaseView : MonoBehaviour
    {
        [SerializeField] private OptionPanelView _options;
        [SerializeField] private Text _animal;
        [SerializeField] private Text _intent;
        [SerializeField] private Text _details;
        [SerializeField] private Button _advance;
        [SerializeField] private Text _advanceLabel;
        [SerializeField] private Button _handbook;
        [SerializeField] private Button _assistance;
        [SerializeField] private GameObject _choices;
        [SerializeField] private GameObject _use;
        [SerializeField] private Text _holdHint;
        [SerializeField] private ScrollRect _detailsScroll;

        public OptionPanelView Options => _options;
        public event Action AdvanceRequested;
        public event Action HandbookRequested;
        public event Action AssistanceRequested;

        private void OnEnable()
        {
            _advance.onClick.AddListener(OnAdvance);
            _handbook.onClick.AddListener(OnHandbook);
            _assistance.onClick.AddListener(OnAssistance);
        }

        private void OnDisable()
        {
            _advance.onClick.RemoveListener(OnAdvance);
            _handbook.onClick.RemoveListener(OnHandbook);
            _assistance.onClick.RemoveListener(OnAssistance);
        }

        public void SetAnimal(string animal, string intent)
        {
            _animal.text = animal;
            _intent.text = intent;
        }

        public void SetDetails(string details)
        {
            bool changed = _details.text != details;
            _details.text = details;
            _details.gameObject.SetActive(!string.IsNullOrEmpty(details));
            _detailsScroll.gameObject.SetActive(!string.IsNullOrEmpty(details));
            if (changed) _detailsScroll.verticalNormalizedPosition = 1;
        }

        public void SetHoldHint(string hint) => _holdHint.text = hint;

        public void SetCommands(string advanceLabel, bool canAdvance, bool canAssist, bool choosing)
        {
            _advanceLabel.text = advanceLabel;
            _advance.gameObject.SetActive(!string.IsNullOrEmpty(advanceLabel));
            _advance.interactable = canAdvance;
            _assistance.interactable = canAssist;
            _choices.SetActive(choosing);
            _use.SetActive(choosing && !_options.HoldToConfirm);
            _holdHint.gameObject.SetActive(choosing && _options.HoldToConfirm);
        }

        private void OnAdvance() => AdvanceRequested?.Invoke();
        private void OnHandbook() => HandbookRequested?.Invoke();
        private void OnAssistance() => AssistanceRequested?.Invoke();
    }
}
