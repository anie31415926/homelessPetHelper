using HomelessPetHelper.Modules.PetMotion;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEngine;

namespace HomelessPetHelper.Game
{
    public sealed class SiegePetMotion : MonoBehaviour
    {
        [SerializeField] private PetMotionPlayer _player;
        [SerializeField] private SiegeTrialView _view;

        private void OnEnable() => _player.gameObject.SetActive(true);
        private void OnDisable()
        {
            if (_player != null) _player.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            _player.Playing = _view.IsBattleRunning;
        }
    }
}
