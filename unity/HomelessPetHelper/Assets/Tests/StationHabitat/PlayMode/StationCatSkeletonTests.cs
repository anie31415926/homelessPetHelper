#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Station.Habitat;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.StationHabitat.PlayMode
{
    public sealed class StationCatSkeletonTests
    {
        [Serializable] private sealed class Fixtures { public Fixture[] fixtures; }
        [Serializable] private sealed class Fixture
        {
            public float progress;
            public string id;
            public int[] indices;
            public float[] vertices;
        }

        [UnityTest]
        public IEnumerator NativeSkinMatchesApprovedRigAndSitNeverSwapsOrLoops()
        {
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab"));
            var surface = new GameObject("CatTestSurface", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage));
            var renderer = actor.GetComponent<StationHabitatRenderer>();
            var image = surface.GetComponent<RawImage>();
            renderer.Bind(image);
            try
            {
                var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                AssertNativeGeometry(renderer, skins);
                AssertSharedSkinAcrossActions(renderer, skins);
                AssertRiseAndTread(renderer, skins[0].bones);
                yield return Capture(renderer, image);
                actor.SetActive(false);
                Assert.That(image.texture, Is.Null);
            }
            finally
            {
                UnityEngine.Object.Destroy(actor);
                UnityEngine.Object.Destroy(surface);
            }
            yield return null;
        }

        private static void AssertNativeGeometry(StationHabitatRenderer renderer, SkinnedMeshRenderer[] skins)
        {
            var fixtures = JsonUtility.FromJson<Fixtures>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Tests/StationHabitat/PetSitFixtures.json").text);
            var baked = new Mesh();
            try
            {
                foreach (var fixture in fixtures.fixtures)
                {
                    renderer.Render(HabitatAction.Sit, fixture.progress * renderer.Player.SitDuration, true);
                    skins.Single(s => s.name == fixture.id).BakeMesh(baked);
                    var vertices = baked.vertices;
                    for (int i = 0; i < fixture.indices.Length; i++)
                    {
                        var expected = new Vector3(fixture.vertices[i * 3], fixture.vertices[i * 3 + 1],
                            fixture.vertices[i * 3 + 2]);
                        Assert.That(Vector3.Distance(vertices[fixture.indices[i]], expected), Is.LessThan(.008f),
                            fixture.id + " vertex " + fixture.indices[i] + " at " + fixture.progress +
                            ": expected " + expected.ToString("F5") + ", actual " +
                            vertices[fixture.indices[i]].ToString("F5"));
                    }
                }
            }
            finally { UnityEngine.Object.Destroy(baked); }
        }

        private static void AssertSharedSkinAcrossActions(StationHabitatRenderer renderer,
            SkinnedMeshRenderer[] skins)
        {
            Assert.That(skins.Length, Is.EqualTo(14));
            var contact = skins.Where(s => s.sharedMesh.uv2.Length != 0).ToArray();
            CollectionAssert.AreEquivalent(new[] { "torso", "tail", "nearThigh", "nearLowerCoat" },
                contact.Select(s => s.name));
            var properties = new MaterialPropertyBlock();
            renderer.Render(HabitatAction.Sit, 20, true);
            contact[0].GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_SeatContact"), Is.EqualTo(1));
            renderer.Render(HabitatAction.Walk, .1f, false);
            contact[0].GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_SeatContact"), Is.Zero);
            var bones = skins[0].bones;
            Assert.That(bones.Length, Is.EqualTo(36));
            foreach (var skin in skins) CollectionAssert.AreEqual(bones, skin.bones);
            var meshes = skins.Select(skin => skin.sharedMesh).ToArray();
            var texture = skins[0].sharedMaterial.mainTexture;
            Assert.That(texture, Is.Not.Null);
            renderer.Render(HabitatAction.Sit, 20, true);
            var settled = bones.Select(b => b.localToWorldMatrix).ToArray();
            renderer.Render(HabitatAction.Sit, 50, true);
            CollectionAssert.AreEqual(settled, bones.Select(b => b.localToWorldMatrix).ToArray());
            foreach (var action in new[] { HabitatAction.Observe, HabitatAction.Walk, HabitatAction.Sit })
            {
                renderer.Render(action, .7f, false);
                CollectionAssert.AreEqual(meshes, skins.Select(s => s.sharedMesh).ToArray());
                foreach (var skin in skins)
                {
                    Assert.That(skin.gameObject.activeInHierarchy, Is.True);
                    Assert.That(skin.sharedMaterial.mainTexture, Is.SameAs(texture));
                }
            }
        }

        private static void AssertRiseAndTread(StationHabitatRenderer renderer, Transform[] bones)
        {
            renderer.Render(HabitatAction.Walk, 0, true, true);
            Assert.That(renderer.Player.Posture, Is.EqualTo(1));
            renderer.Render(HabitatAction.Walk, StationHabitatMotion.RiseSeconds / 2, true, true);
            Assert.That(renderer.Player.Posture, Is.EqualTo(.5f).Within(.001f));
            renderer.Render(HabitatAction.Walk, StationHabitatMotion.RiseSeconds, true, true);
            Assert.That(renderer.Player.Posture, Is.Zero);
            renderer.Player.Sample(HabitatAction.Walk, .1f, false);
            var firstGait = bones.Select(b => b.localToWorldMatrix).ToArray();
            renderer.Player.Sample(HabitatAction.Walk, .4f, false);
            Assert.That(bones.Where((b, i) => b.localToWorldMatrix != firstGait[i]).Count(),
                Is.GreaterThan(10), "Tread must animate the same bones.");
        }

        private static IEnumerator Capture(StationHabitatRenderer renderer, RawImage image)
        {
            string folder = Path.GetFullPath("StationCatEvidence");
            Directory.CreateDirectory(folder);
            Color32[] first = null;
            var camera = renderer.GetComponentInChildren<Camera>();
            for (int frame = 0; frame <= 72; frame++)
            {
                renderer.Render(HabitatAction.Sit, frame / 30f, true);
                // Unity updates the skinned render buffers once per frame. BakeMesh alone
                // cannot prove the player sees the current animation pose.
                yield return null;
                camera.Render();
                var target = (RenderTexture)image.texture;
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                var pixels = texture.GetPixels32();
                if (frame == 0) first = pixels;
                if (frame == 72)
                    Assert.That(pixels.Where((pixel, i) => !pixel.Equals(first[i])).Count(), Is.GreaterThan(5000));
                Assert.That(pixels.Count(p => p.a > 200 && p.r > p.g * 1.1f), Is.GreaterThan(1000), "Cat absent");
                int bottom = Array.FindIndex(pixels, p => p.a > 180) / target.width;
                Assert.That(bottom, Is.InRange(45, 75), "Contact changed at frame " + frame);
                File.WriteAllBytes(Path.Combine(folder, "sit-" + frame.ToString("D3") + ".png"), texture.EncodeToPNG());
                UnityEngine.Object.Destroy(texture);
            }
            yield return null;
        }
    }
}
#endif
