using System;
using System.Collections.Generic;
using UnityEngine;

namespace HomelessPetHelper.Game.Field
{
    /// <summary>Point-keyed authored layouts; unconfigured places keep their existing presentation.</summary>
    public sealed class OutingFieldCatalog
    {
        private readonly Dictionary<string, OutingFieldLayout> _layouts =
            new Dictionary<string, OutingFieldLayout>(StringComparer.Ordinal);
        public IEnumerable<OutingFieldLayout> Layouts => _layouts.Values;

        public OutingFieldCatalog(IEnumerable<TextAsset> sources, IEnumerable<string> pointIds)
        {
            if (sources == null || pointIds == null) throw new ArgumentNullException();
            var known = new HashSet<string>(pointIds, StringComparer.Ordinal);
            foreach (var source in sources)
            {
                var layout = new OutingFieldLayout(source);
                if (!known.Contains(layout.PointId) || _layouts.ContainsKey(layout.PointId))
                    throw new ArgumentException("Unknown or duplicate field point: " + layout.PointId);
                _layouts.Add(layout.PointId, layout);
            }
        }

        public OutingFieldLayout Get(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId)) throw new ArgumentException("Missing field point.");
            return _layouts.TryGetValue(pointId, out var layout) ? layout : null;
        }
    }
}
