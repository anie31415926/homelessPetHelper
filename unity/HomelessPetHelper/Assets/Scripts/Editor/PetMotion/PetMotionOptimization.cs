using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor.PetMotion
{
    internal static class PetMotionOptimization
    {
        internal static void Apply(PetMotionBakeData data)
        {
            if (data.schema != 2 || data.rigs.Length != 7 || data.sources.Length != 3)
                throw new InvalidDataException("Expected coordinate-preserving pet atlas export schema 2.");
            PetMotionAssets.ImportTextures();
            var root = PrefabUtility.LoadPrefabContents(PetMotionAssets.RigPath);
            try
            {
                var material = PetMotionAssets.Material("Atlas");
                foreach (var definition in data.rigs)
                {
                    var renderer = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Single(x => x.name == definition.id);
                    ValidateRestBones(renderer, definition);
                    var bind = renderer.sharedMesh.bindposes;
                    renderer.sharedMesh = PetMotionAssets.WriteMesh(definition, bind);
                    renderer.sharedMaterial = material;
                }
                for (int i = 0; i < data.sources.Length; i++)
                {
                    var filter = root.GetComponentsInChildren<MeshFilter>(true).Single(x => x.name == "Source" + i);
                    // Sit/side source comparison uses the exact same immutable rest geometry.
                    string pose = i == 1 ? "sit" : "side";
                    filter.sharedMesh = i == 0
                        ? PetMotionAssets.WriteMesh(data.sources[i], Array.Empty<Matrix4x4>())
                        : AssetDatabase.LoadAssetAtPath<Mesh>(PetMotionAssets.Folder + "/Mesh_" + pose + ".asset");
                    filter.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PetMotionAssets.RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ValidateRestBones(SkinnedMeshRenderer renderer, PetMotionBakeData.Rig definition)
        {
            var bones = renderer.bones;
            if (bones.Length != definition.bones.Length)
                throw new InvalidDataException("Skeleton migration required: " + definition.id);
            for (int i = 0; i < bones.Length; i++)
            {
                var expected = definition.bones[i];
                var position = new Vector3(expected.position[0], expected.position[1], expected.position[2]);
                if (bones[i].name != expected.id || Vector3.Distance(bones[i].localPosition, position) > .001f)
                    throw new InvalidDataException("Rest bone changed; refusing UV-only upgrade: " + definition.id);
            }
        }
    }
}
