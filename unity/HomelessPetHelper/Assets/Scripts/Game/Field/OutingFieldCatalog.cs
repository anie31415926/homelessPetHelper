using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HomelessPetHelper.Game.Field
{
    /// <summary>Point-keyed authored layouts; unconfigured places keep their existing presentation.</summary>
    public sealed class OutingFieldCatalog
    {
        private readonly Dictionary<string, List<OutingFieldLayout>> _layouts =
            new Dictionary<string, List<OutingFieldLayout>>(StringComparer.Ordinal);
        public IEnumerable<OutingFieldLayout> Layouts => _layouts.Values.SelectMany(items => items);

        public OutingFieldCatalog(IEnumerable<TextAsset> sources, IEnumerable<string> pointIds)
        {
            if (sources == null || pointIds == null) throw new ArgumentNullException();
            var known = new HashSet<string>(pointIds, StringComparer.Ordinal);
            foreach (var source in sources)
            {
                var layout = new OutingFieldLayout(source);
                if (!known.Contains(layout.PointId))
                    throw new ArgumentException("Unknown field point: " + layout.PointId);
                if (!_layouts.TryGetValue(layout.PointId, out var variants))
                    _layouts.Add(layout.PointId, variants = new List<OutingFieldLayout>());
                if (variants.Any(item => item.LayoutId == layout.LayoutId))
                    throw new ArgumentException("Duplicate field layout: " + layout.LayoutId);
                variants.Add(layout);
            }
            foreach (var variants in _layouts.Values)
                variants.Sort((left, right) => string.CompareOrdinal(left.LayoutId, right.LayoutId));
        }

        public OutingFieldLayout Get(string pointId, int seed = 0, bool introductory = false)
        {
            if (string.IsNullOrWhiteSpace(pointId)) throw new ArgumentException("Missing field point.");
            if (seed < 0) throw new ArgumentOutOfRangeException(nameof(seed));
            if (!_layouts.TryGetValue(pointId, out var variants)) return null;
            var candidates = variants.Where(item => item.Introductory == introductory).ToList();
            if (candidates.Count == 0) candidates = variants;
            // Stable across content ordering, machines and processes; never use string.GetHashCode.
            uint hash = 2166136261u;
            foreach (char value in pointId) hash = unchecked((hash ^ value) * 16777619u);
            hash = unchecked((hash ^ (uint)seed) * 16777619u);
            return candidates[(int)(hash % (uint)candidates.Count)];
        }
    }
}
