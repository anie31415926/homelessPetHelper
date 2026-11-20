#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Tests.Siege.PlayMode
{
    public sealed class SiegeContentViewTests
    {
        private SiegeTrialView _view;
        private const string ScenePath = "Assets/EditorScenes/Siege/SC_SiegeTrial.unity";
        private const string DataPath = "Assets/Settings/Siege/SiegeTrialData.json";
        private const string TextPath = "Assets/Settings/Siege/SiegeTrialText.json";
        private Button Button(string name) => _view.GetComponentsInChildren<Button>(true).Single(x => x.name == name);
        private void Click(string name)
        {
            Assert.That(Button(name).interactable, Is.True, name);
            Button(name).onClick.Invoke();
        }

        private void Cell(string field, int index)
        {
            var root = (RectTransform)new SerializedObject(_view).FindProperty(field).objectReferenceValue;
            var button = root.GetChild(index).GetComponentInChildren<Button>();
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator ThemeSelectorChangesSupplyAnimalAndRestartsCleanly()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters());
            yield return null;
            _view = UnityEngine.Object.FindObjectOfType<SiegeTrialView>();
            Assert.That(_view.Snapshot.EncounterId, Is.EqualTo("cat"));
            var old = _view.Snapshot.SessionId;
            Click("Encounter"); yield return null;
            Assert.That(_view.Snapshot.EncounterId, Is.EqualTo("dog"));
            Assert.That(_view.Snapshot.SessionId, Is.Not.EqualTo(old));
            Assert.That(_view.Snapshot.Hand.All(x => new[] { "bone", "disc", "rope" }
                .Contains(x.DefinitionId)), Is.True);
            var animal = _view.GetComponentsInChildren<Image>().Single(x => x.name == "Cat");
            Assert.That(animal.sprite.name, Does.Contain("DogWhite"));
            Click("Encounter"); yield return null;
            Assert.That(_view.Snapshot.EncounterId, Is.Null);
            Assert.That(animal.sprite.name, Does.Contain("CatOrange"));
            Click("Encounter"); yield return null;
            Assert.That(_view.Snapshot.EncounterId, Is.EqualTo("cat"));
            Assert.That(_view.Snapshot.Units, Is.Empty);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CatAndDogRunThroughRealControlsWithFrozenAnimatedMeshesAndScreenshots()
        {
            foreach (string encounter in new[] { "cat", "dog" })
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                    new LoadSceneParameters());
                yield return null;
                _view = UnityEngine.Object.FindObjectOfType<SiegeTrialView>();
                var catalog = new SiegeCatalog(JsonUtility.FromJson<SiegeCatalogData>(
                    AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath).text));
                var session = new SiegeSession(catalog, 20261119, "calm", "view-" + encounter, encounter);
                _view.gameObject.SetActive(false);
                _view.BindEncounter(session, AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath),
                    () => { }, () => { }, session.SetPaused);
                _view.gameObject.SetActive(true);
                var canvas = _view.GetComponent<Canvas>();
                var camera = UnityEngine.Object.FindObjectOfType<Camera>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var target = new RenderTexture(540, 1170, 24);
                camera.targetTexture = target;
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../SiegeContentEvidence"));
                Directory.CreateDirectory(folder);
                bool captured = false, frozen = false, merged = false, warning = false, leap = false;
                int attempts = 0;
                while ((session.Phase == BattlePhase.Decision || session.Phase == BattlePhase.Running)
                    && attempts++ < 3000)
                {
                    if (session.Phase == BattlePhase.Decision)
                    {
                        Click("Clear");
                        var s = session.Snapshot;
                        if (s.Hand.Count > 0 && s.Energy >= 2)
                        {
                            int index = s.Hand.ToList().FindIndex(card =>
                                s.Units.Count(u => u.DefinitionId == card.DefinitionId) >= 2);
                            if (index < 0) index = 0;
                            var card = s.Hand[index];
                            Cell("_handRoot", index);
                            string lane = s.Turn % 3 == 0 ? "Top" : s.Turn % 3 == 1 ? "Mid" : "Bottom";
                            Click(lane);
                            var materials = s.Units.Where(x => x.DefinitionId == card.DefinitionId
                                && x.TransitRemainingMs == 0 && !x.IsCarried).Take(2).ToArray();
                            if (materials.Length == 2 && s.Energy >= 3)
                            {
                                if (!Button("NewMaterial").gameObject.activeInHierarchy) Click("Materials");
                                foreach (var unit in materials)
                                    Cell("_rosterRoot", s.Units.ToList().FindIndex(x => x.Id == unit.Id));
                                Click("NewMaterial"); Click("Merge");
                            }
                        }
                        Click("Submit");
                        yield return null;
                    }
                    else { session.Advance(50); yield return null; }
                    var snapshot = session.Snapshot;
                    if (!captured && snapshot.Units.Count >= 2)
                    { Capture(camera, target, Path.Combine(folder, encounter + "-army.png")); captured = true; }
                    if (!merged && snapshot.Effects.Any(x => x.Kind == "mergePart"))
                    { Capture(camera, target, Path.Combine(folder, encounter + "-merge.png")); merged = true; }
                    if (!warning && snapshot.Towers.Any(x => x.WindingUp))
                    { Capture(camera, target, Path.Combine(folder, encounter + "-tower-warning.png")); warning = true; }
                    if (!leap && snapshot.AnimalState.ImpactAtMs > snapshot.ElapsedMs)
                    {
                        if (snapshot.ElapsedMs - snapshot.AnimalState.LeapStartMs >= 200)
                        { Capture(camera, target, Path.Combine(folder, encounter + "-leap.png")); leap = true; }
                    }
                    if (!frozen && captured && session.Phase == BattlePhase.Decision)
                    {
                        var field = _view.GetComponentInChildren<SiegeBattlefield>();
                        Canvas.ForceUpdateCanvases();
                        var vertices = field.canvasRenderer.GetMesh().vertices;
                        var animal = field.GetComponentInChildren<Image>().rectTransform.anchoredPosition;
                        int time = session.ElapsedMs;
                        yield return null; yield return null;
                        Canvas.ForceUpdateCanvases();
                        Assert.That(session.ElapsedMs, Is.EqualTo(time));
                        Assert.That(field.canvasRenderer.GetMesh().vertices, Is.EqualTo(vertices));
                        Assert.That(field.GetComponentInChildren<Image>().rectTransform.anchoredPosition,
                            Is.EqualTo(animal));
                        frozen = true;
                    }
                }
                Assert.That(attempts, Is.LessThan(3000));
                Assert.That(captured && frozen && merged, Is.True, encounter + " core visuals not exercised");
                Assert.That(warning, Is.True, encounter + " push warning was not exercised");
                if (encounter == "dog") Assert.That(leap, Is.True, "Mid-flight movement was not exercised");
                Assert.That(session.Phase, Is.EqualTo(BattlePhase.Won), encounter);
                Capture(camera, target, Path.Combine(folder, encounter + "-won.png"));
                foreach (var size in new[] { new Vector2Int(375, 667), new Vector2Int(768, 1024) })
                {
                    camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
                    target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                    yield return null; yield return null;
                    AssertBounds(canvas);
                    Capture(camera, target, Path.Combine(folder, encounter + "-" + size.x + ".png"));
                }
                camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
            }
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest]
        public IEnumerator AllSixMergeShapesRenderAndFreezeWithoutNewSceneAssets()
        {
            foreach (string encounter in new[] { "cat", "dog" })
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
                yield return null;
                _view = UnityEngine.Object.FindObjectOfType<SiegeTrialView>();
                var data = JsonUtility.FromJson<SiegeCatalogData>(
                    AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath).text);
                data.rules.playerPeriodMs = 50; data.rules.turnEnergy = 6; data.rules.animalBuilds = 0;
                foreach (var p in data.animalStates) p.periodMs = 1000000;
                foreach (var p in data.map.pads) p.initial = false;
                foreach (var u in data.contentUnits) u.speed = 12;
                var catalog = new SiegeCatalog(data);
                var session = new SiegeSession(catalog, 1, "calm", "gallery", encounter);
                var definition = catalog.Content.Encounter(encounter);
                for (int family = 0; family < 3; family++)
                {
                    string id = definition.UnitIds[family];
                    string lane = catalog.Lanes[family].Id;
                    for (int material = 0; material < 3; material++)
                    {
                        for (int attempt = 0; attempt < 30; attempt++)
                        {
                            var s = session.Snapshot;
                            var card = s.Hand.FirstOrDefault(x => x.DefinitionId == id);
                            var order = new SiegeOrder { SessionId = s.SessionId, Turn = s.Turn };
                            if (card != null) { order.DeployCardId = card.Id; order.DeployLaneId = lane; }
                            else order.DiscardCardId = s.Hand[0].Id;
                            Assert.That(session.TryOrder(order, true, out string reason, out _), Is.True, reason);
                            session.Advance(50);
                            if (card != null) break;
                        }
                    }
                    var current = session.Snapshot;
                    var ids = current.Units.Where(x => x.DefinitionId == id).Select(x => x.Id).ToArray();
                    Assert.That(ids.Length, Is.EqualTo(3));
                    Assert.That(session.TryOrder(new SiegeOrder { SessionId = current.SessionId, Turn = current.Turn,
                        MergeIds = ids, AnchorId = ids[0] }, true, out _, out _), Is.True);
                    session.Advance(50);
                }
                Assert.That(session.Snapshot.Units.Select(x => x.DefinitionId), Is.EquivalentTo(
                    definition.UnitIds.Select(id => catalog.Unit(id).UpgradeId)));
                for (int i = 0; i < 125; i++)
                {
                    var s = session.Snapshot;
                    session.TryOrder(new SiegeOrder { SessionId = s.SessionId, Turn = s.Turn }, true, out _, out _);
                    session.Advance(50);
                }
                _view.gameObject.SetActive(false);
                _view.BindEncounter(session, AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath),
                    () => { }, () => { }, session.SetPaused);
                _view.gameObject.SetActive(true);
                yield return null;
                var field = _view.GetComponentInChildren<SiegeBattlefield>();
                Canvas.ForceUpdateCanvases();
                Assert.That(field.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(4000));
                var canvas = _view.GetComponent<Canvas>();
                var camera = UnityEngine.Object.FindObjectOfType<Camera>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var target = new RenderTexture(768, 1024, 24); camera.targetTexture = target;
                yield return null; yield return null; yield return null;
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../SiegeContentEvidence"));
                Directory.CreateDirectory(folder);
                Capture(camera, target, Path.Combine(folder, encounter + "-all-upgrades.png"));
                camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
            }
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator HoldWarningPressMergeFreezeAndReleaseMatchTheRealControls()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
            yield return null;
            _view = UnityEngine.Object.FindObjectOfType<SiegeTrialView>();
            var data = JsonUtility.FromJson<SiegeCatalogData>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath).text);
            foreach (var pad in data.map.pads) pad.initial = false;
            data.rules.animalBuilds = 0; data.rules.animalRepairs = 0;
            data.rules.sweepPush = 0; data.rules.slapPush = 0;
            foreach (var unit in data.contentUnits) { unit.speed = 6; unit.hp = 100; }
            // Extend only the test hold so a real player window can exercise the merge controls.
            data.encounters[0].holdDurationMs = 6500;
            var catalog = new SiegeCatalog(data);
            var session = new SiegeSession(catalog, 132, "calm", "hold-controls", "cat");
            _view.gameObject.SetActive(false);
            _view.BindEncounter(session, AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath),
                () => { }, () => { }, session.SetPaused);
            _view.gameObject.SetActive(true);
            var canvas = _view.GetComponent<Canvas>();
            var camera = UnityEngine.Object.FindObjectOfType<Camera>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var target = new RenderTexture(540, 1170, 24); camera.targetTexture = target;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../SiegeHoldEvidence"));
            Directory.CreateDirectory(folder);
            bool warning = false;
            int loops = 0;
            while (!(session.Snapshot.AnimalState.HeldUnitId != 0 && session.Phase == BattlePhase.Decision)
                && loops++ < 3000)
            {
                if (session.Phase == BattlePhase.Decision)
                {
                    if (session.Snapshot.AnimalState.Intent == IntentKind.Hold)
                    {
                        AssertBounds(canvas);
                        Capture(camera, target, Path.Combine(folder, "hold-warning.png")); warning = true;
                    }
                    int deployed = session.Snapshot.Stats.Deployed;
                    if (deployed < 3)
                    {
                        Assert.That(session.Snapshot.Hand[0].DefinitionId, Is.EqualTo("fish"));
                        Cell("_handRoot", 0);
                        Click(deployed == 0 ? "Mid" : deployed == 1 ? "Top" : "Bottom");
                    }
                    Click("Submit");
                }
                else session.Advance(50);
                yield return null;
            }
            Assert.That(loops, Is.LessThan(3000), "time=" + session.ElapsedMs + " phase=" + session.Phase
                + " holds=" + session.Snapshot.Stats.Holds + " misses=" + session.Snapshot.Stats.HoldMisses);
            Assert.That(warning, Is.True);
            int end = session.Snapshot.AnimalState.HoldUntilMs;
            int heldId = session.Snapshot.AnimalState.HeldUnitId;
            Assert.That(session.Snapshot.Stats.Holds, Is.EqualTo(1));
            Click("Materials");
            foreach (var size in new[] { new Vector2Int(375, 667), new Vector2Int(540, 1170),
                new Vector2Int(768, 1024) })
            {
                camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
                target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                yield return null; yield return null;
                AssertBounds(canvas);
                Capture(camera, target, Path.Combine(folder, "hold-active-" + size.x + ".png"));
            }
            var before = session.Snapshot;
            for (int i = 0; i < before.Units.Count; i++) Cell("_rosterRoot", i);
            Click("Merge"); Click("Submit");
            var merged = session.Snapshot.Units.Single();
            Assert.That(merged.HeldUntilMs, Is.EqualTo(end));
            Assert.That(merged.Id, Is.Not.EqualTo(heldId));
            Assert.That(session.Snapshot.AnimalState.HeldUnitId, Is.EqualTo(merged.Id));
            yield return null;
            Click("Pause");
            var field = _view.GetComponentInChildren<SiegeBattlefield>();
            Canvas.ForceUpdateCanvases();
            var vertices = field.canvasRenderer.GetMesh().vertices;
            int time = session.ElapsedMs;
            yield return null; yield return null;
            session.Advance(1000); Canvas.ForceUpdateCanvases();
            Assert.That(session.ElapsedMs, Is.EqualTo(time));
            Assert.That(field.canvasRenderer.GetMesh().vertices, Is.EqualTo(vertices));
            Capture(camera, target, Path.Combine(folder, "hold-merged-paused.png"));
            Click("Pause");
            for (int i = 0; session.Snapshot.AnimalState.HeldUnitId != 0 && i < 300; i++)
            {
                if (session.Phase == BattlePhase.Decision) Click("Submit");
                else session.Advance(50);
                yield return null;
            }
            Assert.That(session.Snapshot.AnimalState.HeldUnitId, Is.Zero);
            Assert.That(session.Snapshot.Units.Single().HoldProtectedUntilMs, Is.GreaterThan(session.ElapsedMs));
            Capture(camera, target, Path.Combine(folder, "hold-released.png"));
            camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CarryShowsPickupRouteLandingAndFreezesWithRealControls()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
            yield return null;
            _view = UnityEngine.Object.FindObjectOfType<SiegeTrialView>();
            var data = JsonUtility.FromJson<SiegeCatalogData>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath).text);
            foreach (var pad in data.map.pads) pad.initial = false;
            foreach (var profile in data.animalStates) profile.periodMs = 1000;
            foreach (var unit in data.contentUnits) { unit.speed = 24; unit.hp = 100; }
            data.rules.playerPeriodMs = 400; data.rules.turnEnergy = 6;
            data.rules.animalBuilds = 0; data.rules.animalRepairs = 0;
            data.rules.slapPush = 0; data.rules.sweepPush = 0;
            // Keep a real strategy window inside the test carry; production remains 1600ms.
            data.encounters[1].carryDurationMs = 2400;
            var session = new SiegeSession(new SiegeCatalog(data), 132, "calm", "carry-controls", "dog");
            _view.gameObject.SetActive(false);
            _view.BindEncounter(session, AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath),
                () => { }, () => { }, session.SetPaused);
            _view.gameObject.SetActive(true);
            var canvas = _view.GetComponent<Canvas>();
            var camera = UnityEngine.Object.FindObjectOfType<Camera>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var target = new RenderTexture(540, 1170, 24); camera.targetTexture = target;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../SiegeCarryEvidence"));
            Directory.CreateDirectory(folder);
            bool warned = false;
            int attempts = 0;
            while (!(session.Snapshot.AnimalState.CarriedUnitId != 0 && session.Phase == BattlePhase.Decision)
                && attempts++ < 1400)
            {
                if (session.Phase == BattlePhase.Decision)
                {
                    if (session.Snapshot.AnimalState.Intent == IntentKind.Carry)
                    {
                        yield return null;
                        AssertBounds(canvas);
                        Capture(camera, target, Path.Combine(folder, "carry-warning.png")); warned = true;
                    }
                    int deployed = session.Snapshot.Stats.Deployed;
                    if (deployed < 3)
                    {
                        Cell("_handRoot", 0);
                        Click(deployed == 0 ? "Mid" : deployed == 1 ? "Top" : "Bottom");
                    }
                    Click("Submit");
                }
                else session.Advance(50);
                yield return null;
            }
            Assert.That(attempts, Is.LessThan(1400));
            Assert.That(warned, Is.True);
            // Pose is applied in LateUpdate; inspect it after the frozen window has rendered.
            yield return null; yield return null;
            var before = session.Snapshot;
            int id = before.AnimalState.CarriedUnitId;
            var field = _view.GetComponentInChildren<SiegeBattlefield>();
            var overlay = _view.GetComponentInChildren<SiegeCarryOverlay>();
            Canvas.ForceUpdateCanvases();
            Assert.That(overlay.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(50));
            var positionOnLane = session.Position(before.Units.Single(x => x.Id == id));
            float scale = Mathf.Min(field.rectTransform.rect.width / (float)session.Catalog.Width,
                field.rectTransform.rect.height / (float)session.Catalog.Height);
            var expected = field.rectTransform.rect.center + new Vector2(
                (float)(positionOnLane.X - session.Catalog.Width / 2),
                (float)(session.Catalog.Height / 2 - positionOnLane.Y)) * scale;
            var mouth = (Vector2)field.transform.InverseTransformPoint(
                overlay.transform.TransformPoint(overlay.MouthPoint));
            Assert.That(Vector2.Distance(mouth, expected), Is.LessThan(.01f),
                "mouth=" + mouth + " expected=" + expected + " overlayHeight=" + overlay.rectTransform.rect.height);
            Click("Materials");
            var roster = (RectTransform)new SerializedObject(_view).FindProperty("_rosterRoot").objectReferenceValue;
            int index = before.Units.ToList().FindIndex(x => x.Id == id);
            Assert.That(roster.GetChild(index).GetComponentInChildren<Button>().interactable, Is.False);
            foreach (var size in new[] { new Vector2Int(375, 667), new Vector2Int(540, 1170),
                new Vector2Int(768, 1024) })
            {
                camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
                target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                yield return null; yield return null;
                AssertBounds(canvas);
                Capture(camera, target, Path.Combine(folder, "carry-active-" + size.x + ".png"));
            }
            Click("Pause");
            var animal = _view.GetComponentsInChildren<Image>().Single(x => x.name == "Cat").rectTransform;
            Canvas.ForceUpdateCanvases();
            var vertices = field.canvasRenderer.GetMesh().vertices;
            var position = animal.anchoredPosition;
            session.Advance(1000); yield return null; yield return null;
            Assert.That(session.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Assert.That(field.canvasRenderer.GetMesh().vertices, Is.EqualTo(vertices));
            Assert.That(animal.anchoredPosition, Is.EqualTo(position));
            Click("Pause");
            int end = before.Units.Single(x => x.Id == id).CarriedUntilMs;
            for (int i = 0; session.Snapshot.AnimalState.CarriedUnitId != 0 && i < 100; i++)
            {
                if (session.Phase == BattlePhase.Decision) Click("Submit");
                else session.Advance(50);
                yield return null;
            }
            var landed = session.Snapshot.Units.Single(x => x.Id == id);
            Assert.That(landed.IsCarried, Is.False);
            // The real view also advances the clock between rendered frames after landing.
            double resumedDistance = session.Catalog.Unit(landed.DefinitionId).Speed
                * (session.ElapsedMs - end) / 1000.0;
            Assert.That(landed.Distance, Is.EqualTo(landed.CarryToDistance + resumedDistance).Within(.000001));
            Assert.That(landed.HoldProtectedUntilMs, Is.EqualTo(end + data.encounters[1].carryProtectionMs));
            Assert.That(session.Snapshot.Stats.Carries, Is.EqualTo(1));
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(overlay.canvasRenderer.GetMesh().vertexCount, Is.Zero);
            AssertBounds(canvas);
            Capture(camera, target, Path.Combine(folder, "carry-landed.png"));
            camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
        }

        private void AssertBounds(Canvas canvas)
        {
            var corners = new Vector3[4];
            var bounds = ((RectTransform)canvas.transform).rect;
            foreach (var button in _view.GetComponentsInChildren<Button>())
            {
                var rect = (RectTransform)button.transform;
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(88), button.name);
                rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = canvas.transform.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(bounds.xMin - 1, bounds.xMax + 1), button.name);
                    Assert.That(local.y, Is.InRange(bounds.yMin - 1, bounds.yMax + 1), button.name);
                }
            }
            foreach (var label in _view.GetComponentsInChildren<Text>())
            {
                var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                float height = new TextGenerator().GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                Assert.That(height, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name);
                foreach (char c in label.text.Where(x => x >= 0x3000))
                    Assert.That(label.font.HasCharacter(c), Is.True, "Missing font glyph " + c);
            }
        }

        private void Capture(Camera camera, RenderTexture target, string path)
        {
            Canvas.ForceUpdateCanvases();
            var old = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                Assert.That(texture.GetPixels32().Count(x => x.r < 220 || x.g < 220 || x.b < 220),
                    Is.GreaterThan(target.width * target.height / 50));
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { RenderTexture.active = old; UnityEngine.Object.Destroy(texture); }
        }
    }
}
#endif
