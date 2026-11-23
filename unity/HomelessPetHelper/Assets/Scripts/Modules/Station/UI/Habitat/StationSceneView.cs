using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Station.Habitat;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    public sealed class StationSceneView : MonoBehaviour
    {
        [SerializeField] private StationResidentView[] _cells;
        [SerializeField] private Text _empty;
        [SerializeField] private StationRoomView _room;
        private StationHabitatArea _area;
        private readonly Dictionary<Guid, StationHabitatMotion> _motions = new Dictionary<Guid, StationHabitatMotion>();
        private bool _suspended;
        private bool _focused = true;
        private bool _applicationPaused;
        private int _inspecting = -1;
        private bool _presentationVisible = true;
        public event Action<int> Selected;
        public IReadOnlyList<StationResidentView> Cells => _cells;
        public int Count { get; private set; }
        public StationRoomView Room => _room;

        public void Show(IReadOnlyList<StationSceneResident> residents, int inspecting)
        {
            if (residents == null || residents.Count > _cells.Length)
                throw new ArgumentException("Station scene exceeds its saved display slots.");
            Count = residents.Count;
            _inspecting = inspecting;
            _empty.gameObject.SetActive(_presentationVisible && Count == 0);
            int free = 0, caged = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i].gameObject.SetActive(_presentationVisible && i < Count);
                if (i >= Count) continue;
                var resident = residents[i];
                if (!_motions.TryGetValue(resident.AnimalId, out var motion))
                {
                    if (_room != null && _area == null) _area = _room.CreateArea();
                    motion = new StationHabitatMotion(resident.AnimalId.GetHashCode(), _area);
                    for (int step = 0; step < i * 240; step++) motion.Tick(.05);
                    _motions.Add(resident.AnimalId, motion);
                }
                _cells[i].Bind(resident, motion, i, resident.Observing ? caged++ : free++, _room);
                _cells[i].SetInspecting(resident.Index == inspecting);
            }
            RefreshSuspension();
        }

        public void ClearResidents()
        {
            _motions.Clear();
            Count = 0;
            foreach (var cell in _cells) cell.gameObject.SetActive(false);
            _empty.gameObject.SetActive(true);
        }

        public void SetSuspended(bool suspended)
        {
            _suspended = suspended;
            RefreshSuspension();
        }

        public void SetPresentationVisible(bool visible)
        {
            _presentationVisible = visible;
            for (int i = 0; i < _cells.Length; i++)
                _cells[i].gameObject.SetActive(visible && i < Count);
            _empty.gameObject.SetActive(visible && Count == 0);
        }

        private void OnEnable()
        {
            foreach (var cell in _cells) cell.Selected += Select;
            RefreshSuspension();
        }
        private void OnDisable()
        {
            foreach (var cell in _cells) cell.Selected -= Select;
        }
        private void OnApplicationFocus(bool focused) { _focused = focused; RefreshSuspension(); }
        private void OnApplicationPause(bool paused) { _applicationPaused = paused; RefreshSuspension(); }
        private void RefreshSuspension()
        {
            bool suspended = _suspended || !_focused || _applicationPaused || !isActiveAndEnabled;
            foreach (var cell in _cells) cell.SetSuspended(suspended);
        }
        private void Select(int index)
        {
            if (!_suspended && _focused && !_applicationPaused && _inspecting < 0) Selected?.Invoke(index);
        }
    }
}
