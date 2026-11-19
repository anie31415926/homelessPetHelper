#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.Siege.PlayMode
{
    public sealed class SiegeTrialTests
    {
        private SiegeTrialView _view;
        private const string ScenePath = "Assets/EditorScenes/Siege/SC_SiegeTrial.unity";

        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
            yield return null;
            _view = UnityEngine.Object.FindObjectOfType<SiegeTrialView>();
            Assert.That(_view, Is.Not.Null);
            Assert.That(_view.Snapshot, Is.Not.Null);
            if (_view.Snapshot.Paused)
                Click("Pause");
        }

        private Button Button(string name) => _view.GetComponentsInChildren<Button>(true).Single(x => x.name == name);

        private void Click(string name)
        {
            var b = Button(name);
            Assert.That(b.interactable, Is.True, name);
            b.onClick.Invoke();
        }

        private Transform BoundRoot(string field) =>
            ((RectTransform)new SerializedObject(_view).FindProperty(field).objectReferenceValue).transform;

        private void Cell(string field, int index)
        {
            var cell = BoundRoot(field).GetChild(index).GetComponentInChildren<Button>();
            Assert.That(cell.interactable, Is.True);
            cell.onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator RealControlsFreezeCommitAndRestart()
        {
            yield return Load();
            var opening = _view.Snapshot;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(_view.Snapshot.ElapsedMs, Is.Zero);
            float cardWidth = ((RectTransform)BoundRoot("_handRoot").GetChild(0)).rect.width;
            Cell("_handRoot", 0);
            Click("Bottom");
            Click("Submit");
            Assert.That(_view.Snapshot.Stats.Deployed, Is.EqualTo(1));
            Canvas.ForceUpdateCanvases();
            Assert.That(
                ((RectTransform)BoundRoot("_handRoot").GetChild(0)).rect.width,
                Is.EqualTo(cardWidth).Within(1),
                "Consumed cards must not stretch remaining slots"
            );
            yield return new WaitForSecondsRealtime(.25f);
            Click("Pause");
            var before = _view.Snapshot;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(_view.Snapshot.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Assert.That(Button("Submit").interactable, Is.False);
            Click("Pause");
            Click("Retry");
            Assert.That(
                _view.Snapshot.Hand.Select(x => x.DefinitionId),
                Is.EqualTo(opening.Hand.Select(x => x.DefinitionId))
            );
            Assert.That(_view.Snapshot.Units, Is.Empty);
            Assert.That(_view.Snapshot.SessionId, Is.Not.EqualTo(opening.SessionId));
            Click("NewSeed");
            Assert.That(_view.Snapshot.Seed, Is.Not.EqualTo(opening.Seed));
            Click("Profile");
            Assert.That(_view.Snapshot.Profile, Is.EqualTo("alert"));
            yield return Load();
            Assert.That(_view.Snapshot.SessionId, Is.Not.EqualTo(opening.SessionId));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator PhoneTabletAndInsetScreensRenderWithoutClippedControls()
        {
            yield return Load();
            var canvas = _view.GetComponent<Canvas>();
            var camera = UnityEngine.Object.FindObjectOfType<Camera>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var safe = _view.GetComponentInChildren<SafeAreaView>();
            safe.enabled = false;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../SiegeEvidence"));
            Directory.CreateDirectory(folder);
            foreach (
                var size in new[]
                {
                    new Vector2Int(375, 667),
                    new Vector2Int(390, 844),
                    new Vector2Int(540, 1080),
                    new Vector2Int(540, 1170),
                    new Vector2Int(540, 1200),
                    new Vector2Int(768, 1024),
                }
            )
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                for (int frame = 0; frame < 3; frame++)
                    yield return null;
                Canvas.ForceUpdateCanvases();
                AssertLayout(canvas);
                Capture(camera, target, Path.Combine(folder, $"siege-{size.x}x{size.y}.png"));
                Click("Materials");
                for (int frame = 0; frame < 3; frame++)
                    yield return null;
                Canvas.ForceUpdateCanvases();
                AssertLayout(canvas);
                Capture(camera, target, Path.Combine(folder, $"siege-materials-{size.x}x{size.y}.png"));
                Click("Materials");
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            var rect = (RectTransform)safe.transform;
            rect.anchorMin = new Vector2(.03f, .05f);
            rect.anchorMax = new Vector2(.97f, .94f);
            var inset = new RenderTexture(540, 1170, 24);
            camera.targetTexture = inset;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertLayout(canvas);
            Capture(camera, inset, Path.Combine(folder, "siege-insets.png"));
            camera.targetTexture = null;
            inset.Release();
            UnityEngine.Object.Destroy(inset);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        private void AssertLayout(Canvas canvas)
        {
            var corners = new Vector3[4];
            Rect bounds = ((RectTransform)canvas.transform).rect;
            foreach (var control in _view.GetComponentsInChildren<Button>())
            {
                var rect = (RectTransform)control.transform;
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(88), control.name);
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(88), control.name);
                rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = canvas.transform.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(bounds.xMin - 1, bounds.xMax + 1), control.name);
                    Assert.That(local.y, Is.InRange(bounds.yMin - 1, bounds.yMax + 1), control.name);
                }
            }
            foreach (var label in _view.GetComponentsInChildren<Text>())
            {
                var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                float preferred = new TextGenerator().GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                Assert.That(preferred, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name);
            }
            Assert.That(
                _view.GetComponentInChildren<SiegeBattlefield>().rectTransform.rect.height,
                Is.GreaterThanOrEqualTo(200)
            );
            var mesh = _view.GetComponentInChildren<SiegeBattlefield>().canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.GreaterThan(1000), "Battlefield mesh is blank");
            Assert.That(_view.GetComponentsInChildren<Image>().Any(x => x.name == "Cat" && x.sprite != null), Is.True);
        }

        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            var old = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                var colors = texture.GetPixels32();
                Assert.That(
                    colors.Count(x => x.r < 230 || x.g < 230 || x.b < 230),
                    Is.GreaterThan(colors.Length / 50),
                    "Blank canvas"
                );
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old;
                UnityEngine.Object.Destroy(texture);
            }
        }

        [Serializable]
        public sealed class BattleFixture
        {
            public BattleCase[] cases;
        }

        [Serializable]
        public sealed class BattleCase
        {
            public uint seed;
            public string profile,
                policy;
            public BattleStep[] steps;
        }

        [Serializable]
        public sealed class BattleStep
        {
            public BattleOrder order;
        }

        [Serializable]
        public sealed class BattleOrder
        {
            public int turn,
                deployId,
                anchor,
                rerouteId;
            public string lane,
                destination;
            public int[] ids;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator CompleteBattleThroughActualHandMaterialAnchorAndRerouteControls()
        {
            yield return Load();
            var fixture = JsonUtility
                .FromJson<BattleFixture>(
                    AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Tests/Siege/Fixtures/SiegeParity.json").text
                )
                .cases.Single(x => x.seed == 20261119 && x.profile == "calm" && x.policy == "read");
            var text = new SiegeText(
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/EditorScenes/Siege/SiegeTrialText.json")
            );
            var canvas = _view.GetComponent<Canvas>();
            var camera = UnityEngine.Object.FindObjectOfType<Camera>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var target = new RenderTexture(375, 667, 24);
            camera.targetTexture = target;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../SiegeEvidence"));
            Directory.CreateDirectory(folder);
            bool shot = false,
                repair = false,
                sweep = false,
                range = false;
            foreach (var step in fixture.steps.Where(x => x.order != null && x.order.turn > 0))
            {
                var order = step.order;
                var s = _view.Snapshot;
                Assert.That(s.Turn, Is.EqualTo(order.turn));
                if (s.Paused)
                    Click("Pause");
                Click("Clear");
                if (order.deployId > 0)
                {
                    Cell("_handRoot", s.Hand.ToList().FindIndex(x => x.Id == order.deployId));
                    Click(
                        order.lane == "top" ? "Top"
                        : order.lane == "mid" ? "Mid"
                        : "Bottom"
                    );
                }
                if (order.ids.Length > 0 || order.rerouteId > 0)
                {
                    if (!Button("NewMaterial").gameObject.activeInHierarchy)
                        Click("Materials");
                    foreach (int id in order.ids)
                    {
                        if (id == -1)
                            Click("NewMaterial");
                        else
                            Cell("_rosterRoot", s.Units.ToList().FindIndex(x => x.Id == id));
                    }
                    if (order.ids.Length > 0)
                    {
                        Click("Merge");
                        string anchor =
                            order.anchor == -1 ? text.Get("unity.newUnit") : text.Format("unity.anchor", order.anchor);
                        for (
                            int attempt = 0;
                            Button("Anchor").GetComponentInChildren<Text>().text != anchor && attempt < 3;
                            attempt++
                        )
                            Click("Anchor");
                        Assert.That(Button("Anchor").GetComponentInChildren<Text>().text, Is.EqualTo(anchor));
                    }
                    else
                    {
                        Cell("_rosterRoot", s.Units.ToList().FindIndex(x => x.Id == order.rerouteId));
                        Click("Reroute");
                        for (
                            int attempt = 0;
                            Button("Destination").GetComponentInChildren<Text>().text
                                != text.Format("unity.destination", text.Get("lane." + order.destination))
                                && attempt < 3;
                            attempt++
                        )
                            Click("Destination");
                    }
                }
                if (s.AnimalState.Intent == SiegeState.IntentKind.Sweep)
                {
                    Canvas.ForceUpdateCanvases();
                    Capture(camera, target, Path.Combine(folder, "battle-sweep-warning.png"));
                }
                Click("Submit");
                Assert.That(_view.Snapshot.Stats.Merges, Is.EqualTo(s.Stats.Merges + (order.ids.Length > 0 ? 1 : 0)));
                float deadline = Time.realtimeSinceStartup + 15;
                while (_view.Snapshot.Phase == SiegeState.BattlePhase.Running)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Battle did not advance");
                    var live = _view.Snapshot;
                    if (!shot && live.Effects.Any(x => x.Kind == "towerShot"))
                    {
                        Capture(camera, target, Path.Combine(folder, "battle-tower-shot.png"));
                        shot = true;
                    }
                    if (!repair && live.AnimalState.RepairTowerId != null)
                    {
                        Capture(camera, target, Path.Combine(folder, "battle-repair.png"));
                        repair = true;
                    }
                    if (!sweep && live.Effects.Any(x => x.Kind == "sweep"))
                    {
                        Capture(camera, target, Path.Combine(folder, "battle-sweep-hit.png"));
                        sweep = true;
                    }
                    if (!range && live.Towers.Count > 0)
                        range = InspectTower(camera);
                    yield return null;
                }
            }
            Assert.That(_view.Snapshot.Phase, Is.EqualTo(SiegeState.BattlePhase.Won));
            Assert.That(_view.Snapshot.Stats.Merges, Is.GreaterThanOrEqualTo(1));
            Assert.That(shot && sweep && range, Is.True);
            Capture(camera, target, Path.Combine(folder, "battle-won.png"));
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.Destroy(target);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        private bool InspectTower(Camera camera)
        {
            var data = JsonUtility.FromJson<SiegeCatalogData>(
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/EditorScenes/Siege/SiegeTrialData.json").text
            );
            var catalog = new SiegeCatalog(data);
            var tower = _view.Snapshot.Towers[0];
            var point = catalog.Lane(tower.LaneId).PointAt(tower.Distance);
            var field = _view.GetComponentInChildren<SiegeBattlefield>();
            var rect = field.rectTransform.rect;
            float scale = Mathf.Min(rect.width / (float)catalog.Width, rect.height / (float)catalog.Height);
            Vector2 local =
                rect.center
                + new Vector2((float)(point.X - catalog.Width / 2), (float)(catalog.Height / 2 - point.Y)) * scale;
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, field.transform.TransformPoint(local)),
            };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Any(x => x.gameObject == field.gameObject), Is.True);
            pointer.pointerPressRaycast = hits.First(x => x.gameObject == field.gameObject);
            pointer.pointerCurrentRaycast = pointer.pointerPressRaycast;
            UnityEngine.EventSystems.ExecuteEvents.Execute(
                field.gameObject,
                pointer,
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler
            );
            var detail = (Text)new SerializedObject(_view).FindProperty("_detail").objectReferenceValue;
            Assert.That(
                detail.text,
                Does.Contain(catalog.Tower.Range.ToString(System.Globalization.CultureInfo.InvariantCulture))
            );
            return true;
        }
    }
}
#endif
