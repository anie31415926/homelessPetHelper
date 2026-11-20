#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.PetMotion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HomelessPetHelper.Tests.PetMotion
{
    public sealed class PetMotionOptimizationTests
    {
        private const string Folder = "Assets/Art/PetMotion";
        [Serializable] private sealed class Maps { public MeshMap[] meshes; }
        [Serializable] private sealed class MeshMap { public string id; public int[] sourceVertices; }

        [UnityTest]
        public IEnumerator InstancesShareImmutableAssetsButNotBonesOrPlayback()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Widget_PetMotion.prefab");
            var first = UnityEngine.Object.Instantiate(prefab);
            var second = UnityEngine.Object.Instantiate(prefab);
            yield return null;
            try
            {
                var a = first.GetComponent<PetMotionPlayer>();
                var b = second.GetComponent<PetMotionPlayer>();
                a.Select(3); a.Seek(.33f);
                b.Select(1); b.Seek(.71f);
                float otherTime = b.TimeSeconds;
                var skinsA = first.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var skinsB = second.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var material = skinsA[0].sharedMaterial;
                foreach (var skin in skinsA)
                {
                    var other = skinsB.Single(x => x.name == skin.name);
                    Assert.That(other.sharedMesh, Is.SameAs(skin.sharedMesh));
                    Assert.That(skin.sharedMaterial, Is.SameAs(material));
                    Assert.That(other.sharedMaterial, Is.SameAs(material));
                    Assert.That(other.bones[0], Is.Not.SameAs(skin.bones[0]));
                }
                var clipsA = new SerializedObject(a).FindProperty("_clips");
                var clipsB = new SerializedObject(b).FindProperty("_clips");
                for (int i = 0; i < 6; i++)
                    Assert.That(clipsA.GetArrayElementAtIndex(i).objectReferenceValue,
                        Is.SameAs(clipsB.GetArrayElementAtIndex(i).objectReferenceValue));
                a.Select(4); a.Seek(.91f);
                Assert.That(b.Action, Is.EqualTo(1));
                Assert.That(b.TimeSeconds, Is.EqualTo(otherTime));
                Assert.That(first.GetComponentsInChildren<Renderer>(true).All(x =>
                    x.sharedMaterial == material), Is.True);
            }
            finally { UnityEngine.Object.Destroy(first); UnityEngine.Object.Destroy(second); }
        }

        [UnityTest]
        public IEnumerator OptimizationMatchesCommittedRigAcrossWholeTakesAndBackgrounds()
        {
            string baselinePath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../PetMotionBaseline/pet-motion"));
            if (!File.Exists(baselinePath))
                Assert.Ignore("A/B needs prior rig-only Windows bundle: test-results/pet-motion/baseline-rig.bundle.");
            var bundle = AssetBundle.LoadFromFile(baselinePath);
            Assert.That(bundle, Is.Not.Null);
            var oldRoot = UnityEngine.Object.Instantiate(bundle.LoadAsset<GameObject>(
                "Assets/EditorScenes/PetMotion/Widget_PetMotion.prefab"));
            var newRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "/Widget_PetMotion.prefab"));
            var oldPlayer = oldRoot.GetComponent<PetMotionPlayer>();
            var newPlayer = newRoot.GetComponent<PetMotionPlayer>();
            oldPlayer.Playing = false; newPlayer.Playing = false;
            var target = new RenderTexture(512, 512, 0);
            yield return null;
            try
            {
                var maps = JsonUtility.FromJson<Maps>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/EditorScenes/PetMotion/PetMotionFixtures.json").text);
                float worstVertex = 0, worstMean = 0, worstChangedFraction = 0;
                float[] times = { 0, .005f, .013f, .025f, .05f, .1f, .13f, .23f, .25f, .49f,
                    .5f, .75f, .89f, .98f, .997f, 1 };
                for (int action = 0; action < 6; action++)
                {
                    oldPlayer.Select(action); newPlayer.Select(action);
                    Assert.That(newPlayer.Duration, Is.EqualTo(oldPlayer.Duration).Within(.00001f));
                    foreach (float time in times)
                    {
                        oldPlayer.Seek(time); newPlayer.Seek(time);
                        float error = CompareVertices(oldRoot, newRoot, maps);
                        worstVertex = Mathf.Max(worstVertex, error);
                        Assert.That(error, Is.LessThan(.65f), action + "/" + time);
                    }
                    foreach (bool source in new[] { false, true })
                    foreach (var background in new[] { new Color32(240, 246, 241, 255), new Color32(31, 37, 33, 255) })
                    foreach (float time in new[] { .005f, .13f, .49f, .997f })
                    {
                        oldPlayer.SetSource(source); newPlayer.SetSource(source);
                        oldPlayer.Seek(time); newPlayer.Seek(time);
                        var before = Render(oldRoot, newRoot, target, background);
                        var after = Render(newRoot, oldRoot, target, background);
                        float sum = 0; int changed = 0;
                        for (int i = 0; i < before.Length; i++)
                        {
                            int difference = Math.Abs(before[i].r-after[i].r) + Math.Abs(before[i].g-after[i].g) +
                                Math.Abs(before[i].b-after[i].b);
                            sum += difference;
                            if (difference > 48) changed++;
                        }
                        float mean = sum / (before.Length * 3), fraction = (float)changed / before.Length;
                        worstMean = Mathf.Max(worstMean, mean);
                        worstChangedFraction = Mathf.Max(worstChangedFraction, fraction);
                        Assert.That(mean, Is.LessThan(.5f), "RGB mean / " + action + "/source=" + source);
                        Assert.That(fraction, Is.LessThan(.005f), "Changed pixels / " + action + "/source=" + source);
                        oldRoot.SetActive(true); newRoot.SetActive(true);
                    }
                    oldPlayer.SetSource(false); newPlayer.SetSource(false);
                }
                Debug.Log("Pet optimization A/B: worst retained vertex=" + worstVertex +
                    ", RGB mean=" + worstMean + ", changed pixel fraction=" + worstChangedFraction);
            }
            finally
            {
                UnityEngine.Object.Destroy(oldRoot); UnityEngine.Object.Destroy(newRoot);
                target.Release(); UnityEngine.Object.Destroy(target);
                bundle.Unload(false);
            }
        }

        private static float CompareVertices(GameObject oldRoot, GameObject newRoot, Maps maps)
        {
            float worst = 0;
            foreach (var renderer in newRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var previous = oldRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x => x.name == renderer.name);
                var current = WorldVertices(renderer); var original = WorldVertices(previous);
                var map = maps.meshes.Single(x => x.id == renderer.name).sourceVertices;
                for (int i = 0; i < current.Length; i++)
                    worst = Mathf.Max(worst, Vector3.Distance(current[i], original[map[i]]));
            }
            return worst;
        }

        internal static Vector3[] WorldVertices(SkinnedMeshRenderer renderer)
        {
            // BakeMesh strips transform scale by default, including mirror signs. Compare actual skin matrices.
            var mesh = renderer.sharedMesh;
            var vertices = mesh.vertices; var weights = mesh.boneWeights; var bind = mesh.bindposes;
            var bones = renderer.bones; var matrices = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) matrices[i] = bones[i].localToWorldMatrix * bind[i];
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i]; var v = vertices[i];
                vertices[i] = matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 +
                    matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1 +
                    matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 +
                    matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
            }
            return vertices;
        }

        private static Color32[] Render(GameObject shown, GameObject hidden, RenderTexture target, Color background)
        {
            hidden.SetActive(false); shown.SetActive(true);
            var camera = shown.GetComponentInChildren<Camera>(true);
            camera.targetTexture = target; camera.backgroundColor = background;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                return texture.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
#endif
