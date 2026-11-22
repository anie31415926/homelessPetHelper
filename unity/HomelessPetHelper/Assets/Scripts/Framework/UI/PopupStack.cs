using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>
    /// Bounded navigation owned by one presentation context. Children require an explicit parent;
    /// independent requests wait in priority/FIFO order until the current chain closes.
    /// </summary>
    public sealed class PopupStack<T> where T : struct, Enum
    {
        private readonly List<T> _stack = new List<T>();
        private readonly List<Pending> _pending = new List<Pending>();
        private readonly int _maxDepth, _maxPending;
        private readonly struct Pending
        {
            public readonly T Kind;
            public readonly int Priority;
            public Pending(T kind, int priority) { Kind = kind; Priority = priority; }
        }

        public int Depth => _stack.Count;
        public int PendingCount => _pending.Count;
        public int Revision { get; private set; }
        public T? Top => _stack.Count == 0 ? (T?)null : _stack[_stack.Count - 1];

        public PopupStack(int maxDepth = 4, int maxPending = 8)
        {
            if (maxDepth < 1 || maxPending < 0) throw new ArgumentOutOfRangeException(nameof(maxDepth));
            _maxDepth = maxDepth;
            _maxPending = maxPending;
        }

        public bool Contains(T kind) => _stack.Contains(kind) || _pending.Exists(item => item.Kind.Equals(kind));

        public bool TryOpen(T kind, int priority = 0)
        {
            Validate(kind);
            if (Contains(kind)) return false;
            if (_stack.Count == 0) _stack.Add(kind);
            else
            {
                if (_pending.Count >= _maxPending) return false;
                int index = _pending.FindIndex(item => item.Priority < priority);
                _pending.Insert(index < 0 ? _pending.Count : index, new Pending(kind, priority));
            }
            Revision++;
            return true;
        }

        public bool TryPush(T parent, T child)
        {
            Validate(child);
            if (!Top.HasValue || !Top.Value.Equals(parent) || Contains(child) || _stack.Count >= _maxDepth)
                return false;
            _stack.Add(child);
            Revision++;
            return true;
        }

        public bool TryClose(T expectedTop)
        {
            if (!Top.HasValue || !Top.Value.Equals(expectedTop)) return false;
            _stack.RemoveAt(_stack.Count - 1);
            if (_stack.Count == 0 && _pending.Count > 0)
            {
                _stack.Add(_pending[0].Kind);
                _pending.RemoveAt(0);
            }
            Revision++;
            return true;
        }

        public void Clear()
        {
            _stack.Clear();
            _pending.Clear();
            Revision++;
        }

        private static void Validate(T kind)
        {
            if (!Enum.IsDefined(typeof(T), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
