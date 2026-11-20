using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.Audio.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Audio/Button Audio")]
    public sealed class ButtonAudio : MonoBehaviour
    {
        public const string DefaultCueId = "ui.tap";
        [SerializeField] private AudioScope _scope;
        [SerializeField] private string _cueId = DefaultCueId;
        [SerializeField] private bool _silent;
        private Button _button;
        private AudioScope _parentScope;
        public string CueId { get => _cueId; set => _cueId = value; }
        public bool Silent { get => _silent; set => _silent = value; }

        private void Awake() => _button = GetComponent<Button>();

        private void OnEnable()
        {
            RefreshParentScope();
            _button.onClick.AddListener(PlayAcceptedClick);
        }

        private void OnDisable() => _button.onClick.RemoveListener(PlayAcceptedClick);

        private void OnTransformParentChanged() => RefreshParentScope();

        private void RefreshParentScope()
        {
            _parentScope = GetComponentInParent<AudioScope>(true);
        }

        private void PlayAcceptedClick()
        {
            // onClick already passed Button's pointer/submit eligibility checks. A business
            // listener may now close this panel; do not discard that accepted navigation click.
            if (_silent) return;
            AudioScope scope = _scope != null ? _scope : _parentScope;
            if (scope != null) scope.TryPlayEffect(_cueId);
        }
    }
}
