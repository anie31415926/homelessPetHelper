using System;
using HomelessPetHelper.Modules.Station.Cleaning;
using UnityEngine;

namespace HomelessPetHelper.Modules.Station.UI
{
    /// <summary>Maps the rescue silhouette onto the unchanged saved coat grid.</summary>
    internal sealed class CareCoatProjection
    {
        internal const int Size = 128;
        private readonly byte[] _map;

        internal CareCoatProjection(TextAsset asset)
        {
            _map = asset != null ? asset.bytes : throw new ArgumentNullException(nameof(asset));
            if (_map.Length != Size * Size * 4) throw new ArgumentException("Invalid care projection size.");
        }

        private int Source(int offset) => _map[offset] | (_map[offset + 1] << 8);

        internal bool TryTranslate(Vector2 uv, out Vector2 fieldPoint)
        {
            fieldPoint = default;
            int x = Mathf.RoundToInt(uv.x * (Size - 1)), y = Mathf.RoundToInt(uv.y * (Size - 1));
            int offset = (x + y * Size) * 4;
            if (_map[offset + 2] == 0) return false;
            int from = Source(offset);
            fieldPoint = new Vector2(from % Size, from / Size) / (Size - 1);
            return true;
        }

        internal Color32 Paint(IReadOnlyDirtField field, int index, bool fur)
        {
            int offset = index * 4;
            int coverage = _map[offset + 2];
            if (coverage == 0) return default;
            int alpha = field.Sample(Source(offset)) * coverage / 255;
            int grain = (index * 13 ^ (index / Size) * 61) & 15;
            if (!fur) return new Color32((byte)(106 + grain * 3), (byte)(78 + grain * 2),
                (byte)(59 + grain), (byte)(alpha * (6 + grain % 3) / 8));
            int stroke = _map[offset + 3];
            return grain < 7 ?
                new Color32(122, 76, 59, (byte)(Mathf.Min(180, alpha * 2) * stroke / 255)) :
                new Color32(255, 238, 200, (byte)(Mathf.Min(220, alpha * 2) * stroke / 255));
        }
    }
}
