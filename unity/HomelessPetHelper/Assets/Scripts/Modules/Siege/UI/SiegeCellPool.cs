using System;
using System.Collections.Generic;
using UnityEngine;

namespace HomelessPetHelper.Modules.Siege.UI
{
    internal sealed class SiegeCellPool : IDisposable
    {
        private readonly List<SiegeChoiceCell> _cells = new List<SiegeChoiceCell>();
        private readonly SiegeChoiceCell _prefab;
        private readonly Transform _parent;
        private readonly int _capacity;

        public SiegeCellPool(SiegeChoiceCell prefab, Transform parent, int capacity)
        {
            _prefab = prefab;
            _parent = parent;
            _capacity = capacity;
        }

        public SiegeChoiceCell Get(int index)
        {
            if (index < 0 || index >= _capacity)
                throw new ArgumentOutOfRangeException(nameof(index));
            while (_cells.Count <= index)
                _cells.Add(UnityEngine.Object.Instantiate(_prefab, _parent, false));
            var cell = _cells[index];
            if (!cell.gameObject.activeSelf)
                cell.gameObject.SetActive(true);
            return cell;
        }

        public void Trim(int count)
        {
            for (int i = count; i < _cells.Count; i++)
                if (_cells[i].gameObject.activeSelf)
                    _cells[i].gameObject.SetActive(false);
        }

        public void CancelContacts()
        {
            foreach (var cell in _cells)
                if (cell != null) cell.CancelDrag();
        }

        public void Dispose()
        {
            foreach (var cell in _cells)
                if (cell != null)
                {
                    // Destroy is deferred. A panel may reopen in the same frame, so revoke input now.
                    cell.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(cell.gameObject);
                }
            _cells.Clear();
        }
    }
}
