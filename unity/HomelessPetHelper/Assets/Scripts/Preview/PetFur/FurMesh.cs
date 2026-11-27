using System.Collections.Generic;
using UnityEngine;

namespace HomelessPetHelper.Preview.PetFur
{
    internal sealed class FurMesh
    {
        public const float PixelsPerUnit = 100;
        private static readonly Vector2[] Corners = { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        private static readonly int[] Quad = { 0, 1, 2, 2, 1, 3 };
        private readonly FurProfile _profile;
        private readonly List<Vector3> _positions;
        private readonly List<Vector4>[] _uv = new List<Vector4>[5];
        private readonly int[] _indices;
        public Mesh Mesh { get; }

        public FurMesh(FurProfile profile)
        {
            _profile = profile;
            int capacity = profile.patches.Length * Corners.Length;
            _positions = new List<Vector3>(capacity);
            for (int i = 0; i < _uv.Length; i++) _uv[i] = new List<Vector4>(capacity);
            _indices = new int[profile.patches.Length * Quad.Length];
            for (int i = 0; i < profile.patches.Length; i++)
                for (int j = 0; j < Quad.Length; j++) _indices[i * Quad.Length + j] = i * Corners.Length + Quad[j];
            Mesh = new Mesh { name = "Pet fur preview batch" };
            Mesh.MarkDynamic();
        }

        public static Vector2 ToWorld(FurProfile.Crop crop, Vector2 pixel) => new(
            (pixel.x - crop.x - crop.width * .5f) / PixelsPerUnit,
            (crop.y + crop.height * .5f - pixel.y) / PixelsPerUnit);

        public static Vector2 ToPixel(FurProfile.Crop crop, Vector2 world) => new(
            crop.x + crop.width * .5f + world.x * PixelsPerUnit,
            crop.y + crop.height * .5f - world.y * PixelsPerUnit);

        public void Upload(FurPose pose)
        {
            _positions.Clear();
            foreach (var uv in _uv) uv.Clear();
            for (int i = 0; i < pose.Count; i++)
                if (pose[i].Lift > .008f) AddPatch(_profile.patches[i], pose[i]);

            Mesh.Clear();
            if (_positions.Count == 0) return;
            Mesh.SetVertices(_positions);
            for (int i = 0; i < _uv.Length; i++) Mesh.SetUVs(i, _uv[i]);
            Mesh.SetIndices(_indices, 0, _positions.Count / Corners.Length * Quad.Length, MeshTopology.Triangles, 0);
        }

        private void AddPatch(FurProfile.Patch patch, FurPose.Tuft tuft)
        {
            foreach (var local in Corners)
            {
                var pixel = patch.Root + patch.Cross * ((local.x - .5f) * patch.width) + patch.Grain * (local.y * patch.length);
                _positions.Add(ToWorld(_profile.crop, pixel));
                _uv[0].Add(new Vector4(local.x, local.y, tuft.Lift, tuft.Curl));
                _uv[1].Add(new Vector4(patch.gy, -patch.gx, patch.gx, patch.gy));
                _uv[2].Add(new Vector4(patch.x, patch.y, patch.width, patch.length));
                _uv[3].Add(new Vector4(patch.teeth, patch.toothDepth, patch.seed));
                _uv[4].Add(new Vector4(patch.minWarmth, patch.maxWarmth));
            }
        }
    }
}
