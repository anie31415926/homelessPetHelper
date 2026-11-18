using System;
using HomelessPetHelper.Modules.Station.Cleaning;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI
{
    public sealed class StationAnimalView : MonoBehaviour
    {
        [SerializeField] private Button _select;
        [SerializeField] private Text _name;
        [SerializeField] private Text _status;
        [SerializeField] private DirtSurfaceView _surface;
        private int _index;

        public event Action<int> Selected;

        public void Refresh(int index, string name, string status, Sprite art, DirtField dirt, bool interactive,
            DirtField fur = null)
        {
            _index = index;
            _name.text = name;
            _status.text = status;
            _surface.Refresh(art, dirt, fur);
            _surface.SetCleaning(false);
            _select.interactable = interactive;
        }

        private void OnEnable()
        {
            _select.onClick.AddListener(Select);
        }

        private void OnDisable()
        {
            _select.onClick.RemoveListener(Select);
        }

        private void Select()
        {
            if (_select.interactable)
            {
                Selected?.Invoke(_index);
            }
        }
    }
}
