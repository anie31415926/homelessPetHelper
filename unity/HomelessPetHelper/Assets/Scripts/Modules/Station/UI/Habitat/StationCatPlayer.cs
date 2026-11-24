using HomelessPetHelper.Modules.Station.Habitat;
using HomelessPetHelper.Modules.Animals.UI;
using UnityEngine;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    /// <summary>Samples one immutable skin and skeleton. The view owns presentation time.</summary>
    public sealed class StationCatPlayer : MonoBehaviour
    {
        [SerializeField] private string _appearanceId;
        [SerializeField] private Transform _rig;
        [SerializeField] private AnimationClip _sitDown;
        [SerializeField] private AnimationClip _tread;
        [SerializeField] private StationCatTail _tail;
        [SerializeField] private SkinnedMeshRenderer[] _seatContactRenderers;
        private static readonly int SeatContact = Shader.PropertyToID("_SeatContact");
        private MaterialPropertyBlock _contactBlock;
        private float _lastContact = -1;
        public string AppearanceId => _appearanceId;
        public float Posture { get; private set; }
        public Transform Rig => _rig;
        public float ExpressionPeriodSeconds => _tail.PeriodSeconds;
        public float SitDuration => _sitDown.length;

        public void Sample(HabitatAction action, float seconds, bool rising,
            AnimalExpression expression = default, float expressionSeconds = 0)
        {
            bool resting = action == HabitatAction.Sit || action == HabitatAction.Lie;
            Posture = resting ? Mathf.Clamp01(seconds / _sitDown.length) : rising ?
                1 - Mathf.Clamp01(seconds / StationHabitatMotion.RiseSeconds) : 0;
            if (action == HabitatAction.Walk && (!rising || seconds >= StationHabitatMotion.RiseSeconds))
            {
                float walkSeconds = seconds - (rising ? StationHabitatMotion.RiseSeconds : 0);
                _tread.SampleAnimation(_rig.gameObject, Mathf.Repeat(walkSeconds, _tread.length));
            }
            else _sitDown.SampleAnimation(_rig.gameObject, Posture * _sitDown.length);
            _tail.Apply(expression, expressionSeconds);
            ApplySeatContact();
        }

        private void ApplySeatContact()
        {
            if (_seatContactRenderers == null || _seatContactRenderers.Length == 0) return;
            float contact = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, .99f, Posture));
            if (contact == _lastContact) return;
            _lastContact = contact;
            if (_contactBlock == null) _contactBlock = new MaterialPropertyBlock();
            foreach (var renderer in _seatContactRenderers)
            {
                renderer.GetPropertyBlock(_contactBlock);
                _contactBlock.SetFloat(SeatContact, contact);
                renderer.SetPropertyBlock(_contactBlock);
            }
        }
    }
}
