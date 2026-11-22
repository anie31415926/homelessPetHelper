using UnityEngine;

namespace HomelessPetHelper.Game.UI
{
    public sealed class LevelOverlayAnchor : MonoBehaviour
    {
        [SerializeField] private LevelOverlayView _view;
        public LevelOverlayView View => _view;
    }
}
