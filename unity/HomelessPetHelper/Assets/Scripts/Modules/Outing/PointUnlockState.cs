using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Holds point-open overrides supplied from outside the map module. Default-open points
    /// need no entry here; only externally unlocked or closed points are tracked.
    /// Unlock criteria (level/building) are owned by the caller and are not evaluated here.
    /// </summary>
    public sealed class PointUnlockState
    {
        private readonly HashSet<string> _unlocked = new HashSet<string>();
        private readonly HashSet<string> _closed = new HashSet<string>();

        /// <summary>
        /// Resolves effective openness: explicit overrides win over the definition default.
        /// </summary>
        public bool IsOpen(MapPointDefinition point)
        {
            if (point == null)
            {
                return false;
            }

            if (_closed.Contains(point.PointId))
            {
                return false;
            }

            return point.OpenByDefault || _unlocked.Contains(point.PointId);
        }

        public void Unlock(string pointId)
        {
            if (!string.IsNullOrWhiteSpace(pointId))
            {
                _unlocked.Add(pointId);
                _closed.Remove(pointId);
            }
        }

        public void Close(string pointId)
        {
            if (!string.IsNullOrWhiteSpace(pointId))
            {
                _closed.Add(pointId);
                _unlocked.Remove(pointId);
            }
        }
    }
}
