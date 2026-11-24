using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

namespace HomelessPetHelper.Editor.StationHabitat
{
    /// <summary>Offline appearance contract. Validation finishes before Unity asset mutations.</summary>
    public static class StationAnimalBake
    {
        [Serializable] public sealed class Data
        {
            public int schema;
            public string appearanceId, template;
            public Target unity;
            public Frame framing;
            public Bone[] bones;
            public float[][] bindposes;
            public Skin[] meshes;
            public Take[] takes;
            public TailMotion tailMotion;
        }
        [Serializable] public sealed class Bone
        {
            public string id;
            public int parent;
            public float[] position, rotation;
        }
        [Serializable] public sealed class Skin
        {
            public string id, tint;
            public int order, seatContact;
            public float[] positions, uv, weights;
            public int[] indices, triangles;
        }
        [Serializable] public sealed class Take
        {
            public string id;
            public float duration;
            public Curve[] curves;
        }
        [Serializable] public sealed class TailMotion
        {
            public float[] joinLocal;
            public float raisedDegrees, swishLiftDegrees, swishDegrees, periodSeconds;
        }
        [Serializable] public sealed class Curve { public string path, property; public float[] times, values; }

        [Serializable] public sealed class Target
        {
            public string assetFolder, actorPath, templateActorPath, atlasPath, shaderPath, assetPrefix;
        }
        [Serializable] public sealed class Frame { public float orthoSize; public float[] cameraPosition; }

        public static Data Load(string relativePath)
        {
            if (Path.IsPathRooted(relativePath) || relativePath.Contains("..") || relativePath.Contains("\\"))
                throw new InvalidDataException("Use a project-relative animal bake path.");
            var data = JsonConvert.DeserializeObject<Data>(File.ReadAllText(
                Path.Combine(Application.dataPath, "..", relativePath)));
            Validate(data);
            return data;
        }

        private static void Validate(Data data)
        {
            if (data == null || data.schema != 2 || data.template != "side-quadruped-v1" ||
                string.IsNullOrWhiteSpace(data.appearanceId) || data.bones == null || data.bones.Length != 36 ||
                data.takes == null || data.takes.Length != 2 || data.takes[0] == null ||
                data.takes[1] == null || data.takes[0].id != "SitDown" ||
                data.takes[1].id != "Tread" || data.meshes == null || data.meshes.Length == 0)
                throw new InvalidDataException("Invalid side-quadruped appearance bake.");
            var target = data.unity ?? throw new InvalidDataException("Missing Unity appearance target.");
            foreach (string path in new[] { target.assetFolder, target.actorPath, target.templateActorPath,
                target.atlasPath, target.shaderPath })
                if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                    path.Contains("..") || path.Contains("\\"))
                    throw new InvalidDataException("Invalid Unity appearance target path.");
            if (!Regex.IsMatch(target.assetPrefix ?? string.Empty, "^[A-Za-z][A-Za-z0-9_]*$") ||
                !target.actorPath.EndsWith(".prefab", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid appearance asset name.");
            if (data.framing == null || !Positive(data.framing.orthoSize) ||
                !Vector(data.framing.cameraPosition, 3) || data.tailMotion == null ||
                !Vector(data.tailMotion.joinLocal, 3) || !Positive(data.tailMotion.periodSeconds) ||
                !Finite(data.tailMotion.raisedDegrees) || !Finite(data.tailMotion.swishDegrees) ||
                !Finite(data.tailMotion.swishLiftDegrees) || data.tailMotion.swishDegrees < 0 ||
                data.tailMotion.swishLiftDegrees < data.tailMotion.swishDegrees)
                throw new InvalidDataException("Invalid appearance framing or tail motion.");
            ValidateGeometry(data);
        }

        private static void ValidateGeometry(Data data)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < data.bones.Length; i++)
            {
                var bone = data.bones[i];
                if (bone == null || string.IsNullOrWhiteSpace(bone.id) || !ids.Add(bone.id) ||
                    bone.parent < -1 || bone.parent >= i || !Vector(bone.position, 3) || !Vector(bone.rotation, 4))
                    throw new InvalidDataException("Invalid appearance bone hierarchy.");
            }
            if (!ids.Contains("tailArt") || data.bindposes == null || data.bindposes.Length != data.bones.Length)
                throw new InvalidDataException("Missing appearance tail or bind poses.");
            foreach (var bind in data.bindposes) if (!Vector(bind, 16))
                throw new InvalidDataException("Invalid appearance bind matrix.");
            foreach (var take in data.takes)
            {
                if (!Positive(take.duration) || take.curves == null || take.curves.Length == 0)
                    throw new InvalidDataException("Missing appearance animation curves.");
                foreach (var curve in take.curves) ValidateCurve(curve, take.duration, ids);
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var skin in data.meshes)
            {
                if (skin == null || !Regex.IsMatch(skin.id ?? string.Empty, "^[A-Za-z][A-Za-z0-9_]*$") ||
                    !names.Add(skin.id)) throw new InvalidDataException("Invalid appearance mesh ID.");
                ValidateMesh(skin, data.bones.Length);
            }
        }

        private static void ValidateCurve(Curve curve, float duration, HashSet<string> ids)
        {
            if (curve == null || curve.times == null || curve.times.Length < 2 ||
                !Vector(curve.values, curve.times.Length) || !Vector(curve.times, curve.times.Length) ||
                curve.times[0] != 0 || Math.Abs(curve.times[curve.times.Length - 1] - duration) > .00001f ||
                string.IsNullOrEmpty(curve.path) || string.IsNullOrEmpty(curve.property) ||
                !Regex.IsMatch(curve.property, "^m_Local(Position\\.[xyz]|Rotation\\.[xyzw])$"))
                throw new InvalidDataException("Invalid appearance animation curve.");
            foreach (string segment in curve.path.Split('/')) if (!ids.Contains(segment))
                throw new InvalidDataException("Animation curve references an unknown bone.");
            for (int i = 1; i < curve.times.Length; i++) if (curve.times[i] <= curve.times[i - 1])
                throw new InvalidDataException("Animation curve times must increase.");
        }

        private static void ValidateMesh(Skin skin, int boneCount)
        {
            if (skin.seatContact < 0 || skin.seatContact > 2 ||
                skin.positions == null || skin.positions.Length == 0 || skin.positions.Length % 3 != 0 ||
                !Vector(skin.positions, skin.positions.Length) || skin.uv == null ||
                !Vector(skin.uv, skin.positions.Length / 3 * 2) || skin.weights == null ||
                !Vector(skin.weights, skin.positions.Length / 3 * 4) || skin.indices == null ||
                skin.indices.Length != skin.weights.Length || skin.triangles == null ||
                skin.triangles.Length == 0 || skin.triangles.Length % 3 != 0 ||
                !Regex.IsMatch(skin.tint ?? string.Empty, "^[a-fA-F0-9]{6}$"))
                throw new InvalidDataException("Invalid appearance mesh layout.");
            for (int i = 0; i < skin.weights.Length; i += 4)
            {
                float total = 0;
                for (int j = 0; j < 4; j++)
                {
                    if (skin.weights[i + j] < 0 || skin.weights[i + j] > 1 ||
                        skin.indices[i + j] < 0 || skin.indices[i + j] >= boneCount)
                        throw new InvalidDataException("Invalid appearance skin weights.");
                    total += skin.weights[i + j];
                }
                if (Math.Abs(total - 1) > .00001f) throw new InvalidDataException("Unnormalized appearance weights.");
            }
            foreach (int index in skin.triangles) if (index < 0 || index >= skin.positions.Length / 3)
                throw new InvalidDataException("Appearance triangle is outside the mesh.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => value > 0 && !float.IsInfinity(value);
        private static bool Vector(float[] values, int count)
        {
            if (values == null || values.Length != count) return false;
            foreach (float value in values) if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            return true;
        }
    }
}
