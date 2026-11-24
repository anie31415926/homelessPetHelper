using HomelessPetHelper.Modules.Animals.UI;
using UnityEngine;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    /// <summary>Adds expression after posture sampling, keeping the authored tail root attached.</summary>
    public sealed class StationCatTail : MonoBehaviour
    {
        [SerializeField] private Transform _tail;
        [SerializeField] private Vector3 _joinLocal;
        [SerializeField] private float _raisedDegrees;
        [SerializeField] private float _swishLiftDegrees;
        [SerializeField] private float _swishDegrees;
        [SerializeField] private float _periodSeconds;
        public Transform Tail => _tail;
        public Vector3 JoinLocal => _joinLocal;
        public float PeriodSeconds => _periodSeconds;

        public void Apply(AnimalExpression expression, float seconds)
        {
            if (expression.TailRaise == 0 && expression.TailSwish == 0) return;
            Vector3 pivot = _tail.localPosition + _tail.localRotation * _joinLocal;
            Quaternion raised = Quaternion.Euler(0, 0, _raisedDegrees);
            Quaternion rotation = Quaternion.Slerp(_tail.localRotation, raised, expression.TailRaise);
            float swish = -expression.TailSwish * (_swishLiftDegrees + _swishDegrees *
                Mathf.Sin(seconds * 2 * Mathf.PI / _periodSeconds));
            _tail.localRotation = rotation * Quaternion.Euler(0, 0, swish);
            _tail.localPosition = pivot - _tail.localRotation * _joinLocal;
        }
    }
}
