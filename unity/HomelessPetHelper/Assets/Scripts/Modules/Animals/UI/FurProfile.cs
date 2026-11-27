using System;
using UnityEngine;

namespace HomelessPetHelper.Modules.Animals.UI
{
    [Serializable]
    public sealed class FurProfile
    {
        [Serializable] public sealed class Crop { public int x, y, width, height; }
        [Serializable] public sealed class Light { public float angle, shadow; }
        [Serializable] public sealed class Edge { public int width, rows; public string data; }
        [Serializable]
        public sealed class Patch
        {
            public float x, y, gx, gy, width, length, initial, roll, teeth, toothDepth, seed, minWarmth, maxWarmth;
            public Vector2 Root => new(x, y);
            public Vector2 Grain => new(gx, gy);
            public Vector2 Cross => new(gy, -gx);
        }

        public int width, height;
        public Crop crop;
        public Light light;
        public Edge edge;
        public Patch[] patches;

        public static FurProfile Read(TextAsset data)
        {
            var result = JsonUtility.FromJson<FurProfile>(data.text);
            if (result?.crop == null || result.crop.width <= 0 || result.crop.height <= 0 || result.light == null ||
                result.edge == null || result.patches == null || result.patches.Length == 0 || result.patches.Length > 72)
                throw new ArgumentException("Invalid pet fur profile.", nameof(data));
            return result;
        }

        public Texture2D CreateEdgeTexture()
        {
            var source = Convert.FromBase64String(edge.data);
            if (source.Length != edge.width * edge.rows * 2) throw new InvalidOperationException("Invalid fur edge texture size.");
            var pixels = new Color32[edge.width * edge.rows];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(source[i * 2], 0, 0, source[i * 2 + 1]);
            var texture = new Texture2D(edge.width, edge.rows, TextureFormat.RGBA32, false, true);
            texture.name = "Fur edge profiles";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
