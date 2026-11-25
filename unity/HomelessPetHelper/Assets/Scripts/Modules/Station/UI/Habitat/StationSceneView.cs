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
        private List<StationResidentView> _pool;
        private readonly List<Guid> _departed = new List<Guid>();
        private readonly Dictionary<Guid, StationHabitatMotion> _motions = new Dictionary<Guid, StationHabitatMotion>();
        private bool _suspended;
        private bool _focused = true;
        private bool _applicationPaused;
        private int _inspecting = -1;
        private bool _presentationVisible = true;
        public event Action<int> Selected;
        public IReadOnlyList<StationResidentView> Cells => (IReadOnlyList<StationResidentView>)_pool ?? _cells;
        public int Count { get; private set; }
        public StationRoomView Room => _room;

        public void Show(IReadOnlyList<StationSceneResident> residents, int inspecting)
        {
            if (residents == null) throw new ArgumentNullException(nameof(residents));
            EnsureSlots(residents.Count);
            ForgetDeparted(residents);
            Count = residents.Count;
            _inspecting = inspecting;
            _empty.gameObject.SetActive(_presentationVisible && Count == 0);
            int free = 0, caged = 0;
            for (int i = 0; i < Cells.Count; i++)
            {
                Cells[i].gameObject.SetActive(_presentationVisible && i < Count);
                if (i >= Count) continue;
                var resident = residents[i];
                if (!_motions.TryGetValue(resident.AnimalId, out var motion))
                {
                    if (_room != null && _area == null) _area = _room.CreateArea();
                    motion = new StationHabitatMotion(resident.AnimalId.GetHashCode(), _area);
                    for (int step = 0; step < i * 240; step++) motion.Tick(.05);
                    _motions.Add(resident.AnimalId, motion);
                }
                Cells[i].Bind(resident, motion, i, resident.Observing ? caged++ : free++, _room);
                Cells[i].SetInspecting(resident.Index == inspecting);
            }
            RefreshSuspension();
        }

        private void EnsureSlots(int count)
        {
            if (count <= Cells.Count) return;
            _pool ??= new List<StationResidentView>(_cells);
            var source = _cells[0];
            while (_pool.Count < count)
            {
                var slot = Instantiate(source, source.transform.parent);
                slot.gameObject.SetActive(false);
                if (isActiveAndEnabled) slot.Selected += Select;
                _pool.Add(slot);
                _room?.Register(slot);
            }
        }

        private void ForgetDeparted(IReadOnlyList<StationSceneResident> residents)
        {
            _departed.Clear();
            foreach (Guid id in _motions.Keys)
            {
                bool stays = false;
                foreach (var resident in residents)
                    if (resident.AnimalId == id) { stays = true; break; }
                if (!stays) _departed.Add(id);
            }
            foreach (Guid id in _departed) _motions.Remove(id);
        }

        public void ClearResidents()
        {
            _motions.Clear();
            Count = 0;
            foreach (var cell in Cells) cell.gameObject.SetActive(false);
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
            for (int i = 0; i < Cells.Count; i++)
                Cells[i].gameObject.SetActive(visible && i < Count);
            _empty.gameObject.SetActive(visible && Count == 0);
        }

        private void OnEnable()
        {
            foreach (var cell in Cells) cell.Selected += Select;
            RefreshSuspension();
        }
        private void OnDisable()
        {
            foreach (var cell in Cells) cell.Selected -= Select;
        }
        private void OnApplicationFocus(bool focused) { _focused = focused; RefreshSuspension(); }
        private void OnApplicationPause(bool paused) { _applicationPaused = paused; RefreshSuspension(); }
        private void RefreshSuspension()
        {
            bool suspended = _suspended || !_focused || _applicationPaused || !isActiveAndEnabled;
            foreach (var cell in Cells) cell.SetSuspended(suspended);
        }
        private void Select(int index)
        {
            if (!_suspended && _focused && !_applicationPaused && _inspecting < 0) Selected?.Invoke(index);
        }
    }
}
