using System.Collections.Generic;

namespace HomelessPetHelper.Modules.Siege
{
    /// <summary>One state's cosmetic records. Snapshots never share this owner or its free objects.</summary>
    internal sealed class SiegeEffectPool
    {
        internal const int Capacity = 192;
        private readonly Stack<SiegeState.Effect> _free = new Stack<SiegeState.Effect>(Capacity);

        internal SiegeState.Effect Rent() => _free.Count == 0 ? new SiegeState.Effect() : _free.Pop();

        internal void Return(SiegeState.Effect effect)
        {
            effect.Kind = effect.Visual = null;
            effect.From = effect.To = default;
            effect.Radius = 0;
            effect.StartMs = effect.UntilMs = 0;
            effect.Sequence = effect.UnitId = 0;
            if (_free.Count < Capacity) _free.Push(effect);
        }

        internal void Expire(List<SiegeState.Effect> active, int now)
        {
            int kept = 0;
            for (int index = 0; index < active.Count; index++)
            {
                var effect = active[index];
                if (effect.UntilMs <= now) Return(effect);
                else active[kept++] = effect;
            }
            if (kept < active.Count) active.RemoveRange(kept, active.Count - kept);
        }
    }
}
