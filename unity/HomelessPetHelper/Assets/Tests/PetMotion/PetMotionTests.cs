#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.PetMotion;
using HomelessPetHelper.Modules.Rescue.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PetMotion
{
    public sealed class PetMotionTests
    {
        private const string Folder = "Assets/Art/PetMotion";
        private PetMotionPlayer _player;
        private GameObject _panel;

        [Serializable] private sealed class Fixtures { public Fixture[] fixtures; }
        [Serializable] private sealed class Fixture
        {
            public string action, id;
            public float time;
            public int[] indices;
            public float[] vertices;
        }

        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/EditorScenes/PetMotion/SC_PetMotion.unity",
                new LoadSceneParameters());
            yield return null;
            yield return null;
            yield return null;
            _player = UnityEngine.Object.FindObjectOfType<PetMotionPlayer>();
            _panel = _player.transform.parent.gameObject;
            _player.Playing = false;
        }

        [UnityTest]
        public IEnumerator RuntimeTexturesAreBoundWithoutPreviewSceneDependencies()
        {
            yield return Load();
            var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            Assert.That(dependencies.Any(x => x.StartsWith(Folder, StringComparison.Ordinal)), Is.True);
            Assert.That(dependencies.Any(x => x.StartsWith("Assets/EditorScenes/", StringComparison.Ordinal)),
                Is.False);
            Assert.That(EditorBuildSettings.scenes.Any(x => x.path.StartsWith(Folder,
                StringComparison.Ordinal)), Is.False);
            var textures = AssetDatabase.GetDependencies(Folder + "/Widget_PetMotion.prefab", true)
                .Where(x => x.EndsWith(".png", StringComparison.Ordinal)).ToArray();
            Assert.That(textures.Length, Is.EqualTo(1));
            foreach (string path in textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(texture.width, Is.EqualTo(876));
                Assert.That(texture.height, Is.EqualTo(924));
                Assert.That(importer.isReadable || importer.mipmapEnabled, Is.False);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            }
            yield return Unload();
        }

        [UnityTest]
        public IEnumerator NativeSkinningMatchesAcceptedThreeFixtures()
        {
            yield return Load();
            var fixtures = JsonUtility.FromJson<Fixtures>(
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/EditorScenes/PetMotion/PetMotionFixtures.json").text);
            var names = new[] { "idle", "look", "tread", "walk", "sit", "side" };
            foreach (var fixture in fixtures.fixtures)
            {
                _player.Select(Array.IndexOf(names, fixture.action));
                _player.Seek(fixture.time / _player.Duration);
                yield return null;
                var renderer = _player.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(x => x.name == fixture.id);
                var vertices = PetMotionOptimizationTests.WorldVertices(renderer);
                for (int i = 0; i < fixture.indices.Length; i++)
                {
                    var actual = vertices[fixture.indices[i]];
                    int v = i * 3;
                    var expected = new Vector3(fixture.vertices[v], fixture.vertices[v + 1],
                        fixture.vertices[v + 2]);
                    Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.65f),
                        fixture.action + "/" + fixture.id + "/" + fixture.time + "/" + fixture.indices[i] +
                        ": actual=" + actual + ", expected=" + expected);
                }
            }
            yield return Unload();
        }

        [UnityTest]
        public IEnumerator SwitchingActionsResetsBonesAndSources()
        {
            yield return Load();
            _player.Select(1);
            _player.Seek(.01f);
            _player.Select(0);
            var head = _player.GetComponentsInChildren<Transform>().Single(x => x.name == "head");
            Assert.That(Quaternion.Angle(head.localRotation, Quaternion.identity), Is.LessThan(.001f));
            for (int action = 0; action < 6; action++)
            {
                _player.Select(action);
                Assert.That(_player.GetComponentsInChildren<SkinnedMeshRenderer>().Length,
                    Is.EqualTo(action < 4 ? 5 : 1));
                _player.SetSource(true);
                Assert.That(_player.GetComponentsInChildren<SkinnedMeshRenderer>(), Is.Empty);
                Assert.That(_player.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(1));
                _player.SetSource(false);
            }
            _player.ResetPreview();
            Assert.That(_player.Action, Is.Zero);
            Assert.That(_player.Source, Is.False);
            yield return Unload();
        }

        [UnityTest]
        public IEnumerator RealControlsPauseSeekAndChangeActions()
        {
            yield return Load();
            var controls = _panel.GetComponentInChildren<PetMotionControls>();
            var action = controls.GetComponentInChildren<Dropdown>();
            Assert.That(action.options.Count, Is.EqualTo(6));
            action.value = 5;
            Assert.That(_player.Action, Is.EqualTo(5));
            var sliders = controls.GetComponentsInChildren<Slider>();
            sliders.Single(x => x.name == "Speed").value = 1.5f;
            Assert.That(_player.Speed, Is.EqualTo(1.5f));
            sliders.Single(x => x.name == "Progress").value = .5f;
            Assert.That(_player.TimeSeconds, Is.EqualTo(_player.Duration * .5f).Within(.001f));
            Assert.That(_player.Playing, Is.False);
            float time = _player.TimeSeconds;
            yield return null;
            Assert.That(_player.TimeSeconds, Is.EqualTo(time));
            controls.GetComponentsInChildren<Toggle>().Single(x => x.name == "Playing").isOn = true;
            yield return null;
            Assert.That(_player.TimeSeconds, Is.GreaterThan(time));
            controls.GetComponentsInChildren<Toggle>().Single(x => x.name == "Source").isOn = true;
            Assert.That(_player.Source, Is.True);
            controls.GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(_player.Source, Is.False);
            Assert.That(_player.Action, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => _player.Select(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _player.Seek(float.NaN));
            yield return Unload();
        }

        [UnityTest]
        public IEnumerator DisabledPanelReleasesTargetAndCanReopen()
        {
            yield return Load();
            var image = _panel.GetComponentInChildren<RawImage>();
            Assert.That(image.texture, Is.Not.Null);
            _panel.SetActive(false);
            Assert.That(image.texture, Is.Null);
            yield return null;
            _panel.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(image.texture, Is.Not.Null);
            yield return Unload();
        }

        [UnityTest]
        public IEnumerator SamplingAllocationAndCostAreRecorded()
        {
            yield return Load();
            _player.Select(3);
            for (int i = 0; i < 10; i++) _player.Seek(i / 20f);
            var timer = new System.Diagnostics.Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread();
            timer.Start();
            for (int i = 0; i < 200; i++) _player.Seek(i / 201f);
            timer.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log("Pet motion 200 CPU samples: managedBytes=" + allocated +
                ", milliseconds=" + timer.Elapsed.TotalMilliseconds + "; Editor CPU only, not target frame.");
            Assert.That(allocated, Is.Zero);
            yield return Unload();
        }

        [UnityTest]
        public IEnumerator InsetScreenDropdownRemainsReadableAndSelectable()
        {
            yield return Load();
            var canvas = _panel.GetComponentInChildren<Canvas>();
            var camera = SceneManager.GetActiveScene().GetRootGameObjects()
                .Single(x => x.name == "MainCamera").GetComponent<Camera>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            camera.cullingMask = ~(1 << 30);
            var safe = canvas.GetComponentInChildren<SafeAreaView>();
            safe.enabled = false;
            var rect = (RectTransform)safe.transform;
            rect.anchorMin = new Vector2(0, 34f / 844);
            rect.anchorMax = new Vector2(1, 1 - 46f / 844);
            var target = new RenderTexture(390, 844, 24);
            try
            {
                camera.targetTexture = target;
                camera.aspect = 390f / 844;
                Canvas.ForceUpdateCanvases();
                yield return null;
                var dropdown = canvas.GetComponentInChildren<Dropdown>();
                dropdown.Show();
                yield return new WaitForSecondsRealtime(.2f);
                var items = canvas.GetComponentsInChildren<Toggle>().Where(x => x.name.StartsWith("Item ",
                    StringComparison.Ordinal)).ToArray();
                Assert.That(items.Length, Is.EqualTo(6));
                foreach (var item in items)
                {
                    Assert.That(item.targetGraphic.GetComponent<Image>().sprite, Is.Null);
                    var itemRect = (RectTransform)item.transform;
                    Assert.That(itemRect.rect.height, Is.GreaterThanOrEqualTo(144));
                    var corners = new Vector3[4];
                    var scroll = item.GetComponentInParent<ScrollRect>();
                    itemRect.GetWorldCorners(corners);
                    float bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
                    float top = scroll.viewport.InverseTransformPoint(corners[1]).y;
                    float shift = top > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - top :
                        bottom < scroll.viewport.rect.yMin ? scroll.viewport.rect.yMin - bottom : 0;
                    scroll.content.anchoredPosition += new Vector2(0, shift);
                    Canvas.ForceUpdateCanvases();
                    itemRect.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = camera.WorldToViewportPoint(corner);
                        Assert.That(point.y, Is.InRange(34f / 844 - .005f, 1 - 46f / 844 + .005f));
                    }
                }
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PetMotionEvidence"));
                Directory.CreateDirectory(folder);
                Capture(camera, target, Path.Combine(folder, "inset-dropdown"));
                items[5].isOn = true;
                Assert.That(_player.Action, Is.EqualTo(5));
                dropdown.Hide();
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            yield return Unload();
        }

        [UnityTest]
        public IEnumerator SixAspectsRenderEveryActionAndVisibleMotion()
        {
            yield return Load();
            var canvas = _panel.GetComponentInChildren<Canvas>();
            var camera = SceneManager.GetActiveScene().GetRootGameObjects()
                .Single(x => x.name == "MainCamera").GetComponent<Camera>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            camera.cullingMask = ~(1 << 30);
            var safe = canvas.GetComponentInChildren<SafeAreaView>();
            safe.enabled = false;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PetMotionEvidence"));
            Directory.CreateDirectory(folder);
            foreach (var size in new[] { new Vector2Int(375, 667), new Vector2Int(390, 844),
                new Vector2Int(540, 1080), new Vector2Int(540, 1170), new Vector2Int(540, 1200),
                new Vector2Int(768, 1024) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                camera.aspect = (float)size.x / size.y;
                Canvas.ForceUpdateCanvases();
                yield return null;
                foreach (var selectable in canvas.GetComponentsInChildren<Selectable>())
                {
                    var corners = new Vector3[4];
                    ((RectTransform)selectable.transform).GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var screen = camera.WorldToViewportPoint(corner);
                        Assert.That(screen.x, Is.InRange(-.005f, 1.005f), selectable.name);
                        Assert.That(screen.y, Is.InRange(-.005f, 1.005f), selectable.name);
                    }
                }
                for (int action = 0; action < 6; action++)
                {
                    canvas.GetComponent<PetMotionControls>().GetComponentInChildren<Dropdown>().value = action;
                    _player.Select(action);
                    _player.Seek(.13f);
                    yield return null;
                    var pixels = Capture(camera, target, Path.Combine(folder, size.x + "x" + size.y + "-" + action));
                    Assert.That(pixels.Count(c => c.r > c.g * 1.12f && c.g > c.b * 1.1f),
                        Is.GreaterThan(200), "Missing cat pixels: " + action);
                }
                _player.Select(2);
                _player.Seek(.05f);
                yield return null;
                var stage = (RenderTexture)_panel.GetComponentInChildren<RawImage>().texture;
                var first = Read(stage);
                _player.Seek(.065f);
                yield return null;
                stage = (RenderTexture)_panel.GetComponentInChildren<RawImage>().texture;
                var second = Read(stage);
                Assert.That(second.Length, Is.EqualTo(first.Length), "Stable stage size at " + size);
                Assert.That(first.Where((c, i) => !c.Equals(second[i])).Count(), Is.GreaterThan(50),
                    "Visible animation at " + size);
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            yield return Unload();
        }

        private static Color32[] Capture(Camera camera, RenderTexture target, string path)
        {
            camera.Render();
            var texture = ReadTexture(target);
            try
            {
                File.WriteAllBytes(path + ".png", texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally { UnityEngine.Object.Destroy(texture); }
        }

        private static Color32[] Read(RenderTexture target)
        {
            var texture = ReadTexture(target);
            try { return texture.GetPixels32(); }
            finally { UnityEngine.Object.Destroy(texture); }
        }

        private static Texture2D ReadTexture(RenderTexture target)
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            RenderTexture.active = target;
            try
            {
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally { RenderTexture.active = previous; }
        }

        private static IEnumerator Unload()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }
    }
}
#endif
