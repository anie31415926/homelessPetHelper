using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Preview.PetFur;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PetFur
{
    public sealed class PetFurPreviewTests
    {
        private const string Folder = "Assets/EditorScenes/PetFur/";
        private const string Scene = Folder + "SC_PetFurPreview.unity";
        private const string Profile = "Assets/Art/Station/Doudou/DoudouFur.json";
        private const int Width = 390, Height = 844;

        [Test]
        public void PreviewIsNotAPlayerSceneAndDoesNotChangeOldAnimals()
        {
            foreach (var builtScene in EditorBuildSettings.scenes.Where(scene => scene.enabled))
            {
                Assert.That(builtScene.path, Is.Not.EqualTo(Scene));
                Assert.That(AssetDatabase.GetDependencies(builtScene.path, true).Any(path => path.StartsWith(Folder)), Is.False, builtScene.path);
            }
            var profile = FurProfile.Read(AssetDatabase.LoadAssetAtPath<TextAsset>(Profile));
            Assert.That(profile.patches.Length, Is.EqualTo(62));
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "T_DoudouFurPreview.png");
            Assert.That((source.width, source.height), Is.EqualTo((profile.width, profile.height)));
            Assert.That(AssetDatabase.LoadAssetAtPath<Shader>(Folder + "Sh_PetFurPreview.shader").isSupported, Is.True);
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/PetRound-Game.ttf");
            Assert.That("抚摸梳理复位".All(font.HasCharacter), Is.True);
        }

        [Test]
        public void GroomingAndContinuousPettingRespectTheirSeparateState()
        {
            var profile = FurProfile.Read(AssetDatabase.LoadAssetAtPath<TextAsset>(Profile));
            var pose = new FurPose(profile.patches);
            var root = new Vector2(161, 243);
            var tip = new Vector2(161, 331);
            Assert.That(pose.Stroke(root, tip), Is.True);
            var styled = Enumerable.Range(0, pose.Count).Select(i => pose[i].Roughness).ToArray();
            Assert.That(styled.Count(value => value < .8f), Is.GreaterThan(3));
            pose.Settle(FurMode.Pet);
            Assert.That(pose.Stroke(tip, root), Is.True);
            Assert.That(Enumerable.Range(0, pose.Count).Count(i => pose[i].Flick >= .10f), Is.EqualTo(17));
            var split = new FurPose(profile.patches);
            split.Settle(FurMode.Pet);
            for (int y = 331; y > 243; y -= 4) split.Stroke(new Vector2(161, y), new Vector2(161, y - 4));
            for (int i = 0; i < pose.Count; i++) Assert.That(split[i].Flick, Is.EqualTo(pose[i].Flick).Within(.00001f));
            Assert.That(pose.Advance(16), Is.True);
            for (int i = 0; i < 100; i++) pose.Advance(16);
            Assert.That(pose.Advance(16), Is.False);
            Assert.That(Enumerable.Range(0, pose.Count).All(i => pose[i].Lift == 0), Is.True);
            pose.Settle(FurMode.Brush);
            Assert.That(Enumerable.Range(0, pose.Count).Select(i => pose[i].Roughness), Is.EqualTo(styled));
            Assert.That(pose.Stroke(tip, root), Is.True);
            Assert.That(Enumerable.Range(0, pose.Count).Any(i => pose[i].Roughness > styled[i]), Is.True);
        }

        [UnityTest]
        public IEnumerator SavedSceneRendersAndHandlesGestures()
        {
            EditorSceneManager.OpenScene(Scene);
            yield return new EnterPlayMode();
            var preview = Object.FindObjectOfType<PetFurPreview>();
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.PatchCount, Is.EqualTo(62));
            Assert.That(preview.VertexCount, Is.EqualTo(248));
            var camera = preview.PreviewCamera;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var output = System.Environment.GetEnvironmentVariable("PET_FUR_PREVIEW_REPORT");
            camera.targetTexture = target;
            camera.aspect = (float)Width / Height;
            yield return null;

            var rough = Capture(camera, target, output, "rough");
            var chest = ReportRect(preview, new Vector2(102, 239), new Vector2(247, 348));
            var face = ReportRect(preview, new Vector2(125, 145), new Vector2(270, 208));
            var feet = ReportRect(preview, new Vector2(95, 422), new Vector2(423, 498));
            Click("Pet");
            Assert.That(preview.Mode, Is.EqualTo(FurMode.Pet));
            Assert.That(preview.VertexCount, Is.Zero);
            var flat = Capture(camera, target, output, "flat");
            Assert.That(Difference(rough, flat, chest), Is.GreaterThan(120));
            Assert.That(Difference(rough, flat, face), Is.Zero);
            Assert.That(Difference(rough, flat, feet), Is.Zero);

            yield return Swipe(preview, new Vector2(161, 331), new Vector2(161, 243));
            yield return new WaitForSecondsRealtime(.10f);
            Assert.That(preview.VertexCount, Is.InRange(12, 244));
            var pet = Capture(camera, target, output, "pet");
            Assert.That(Difference(flat, pet, chest), Is.GreaterThan(45));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(preview.VertexCount, Is.Zero);
            int updates = preview.MeshUpdates;
            yield return null;
            yield return null;
            Assert.That(preview.MeshUpdates, Is.EqualTo(updates));

            Click("Brush");
            yield return Swipe(preview, new Vector2(161, 243), new Vector2(161, 331));
            yield return Swipe(preview, new Vector2(161, 243), new Vector2(161, 331));
            yield return new WaitForSecondsRealtime(.65f);
            var brushed = Capture(camera, target, output, "brushed");
            Assert.That(Difference(rough, brushed, chest), Is.GreaterThan(90));
            Assert.That(Difference(flat, brushed, chest), Is.LessThan(Difference(flat, rough, chest)));
            Click("Reset");
            Assert.That(Difference(rough, Capture(camera, target, null, null), chest), Is.Zero);
            if (output != null)
            {
                var canvas = Object.FindObjectOfType<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Capture(camera, target, output, "controls");
            }
            camera.targetTexture = null;
            Object.Destroy(target);
            yield return new ExitPlayMode();
        }

        private static void Click(string name)
        {
            var button = GameObject.Find(name).GetComponent<Button>();
            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, click, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private static IEnumerator Swipe(PetFurPreview preview, Vector2 from, Vector2 to)
        {
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 4, position = preview.ToScreen(from) };
            ExecuteEvents.Execute(preview.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            for (int i = 1; i <= 4; i++)
            {
                pointer.position = preview.ToScreen(Vector2.Lerp(from, to, i / 4f));
                ExecuteEvents.Execute(preview.gameObject, pointer, ExecuteEvents.dragHandler);
                yield return null;
            }
            ExecuteEvents.Execute(preview.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        }

        private static Color32[] Capture(Camera camera, RenderTexture target, string outDir, string name)
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                if (outDir != null) File.WriteAllBytes(Path.Combine(outDir, name + ".png"), texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(texture);
            }
        }

        private static RectInt ReportRect(PetFurPreview preview, Vector2 min, Vector2 max)
        {
            var a = preview.ToScreen(min);
            var b = preview.ToScreen(max);
            return new RectInt((int)a.x, (int)b.y, (int)(b.x - a.x), (int)(a.y - b.y));
        }

        private static int Difference(Color32[] a, Color32[] b, RectInt region)
        {
            int changed = 0;
            for (int y = region.yMin; y < region.yMax; y++)
                for (int x = region.xMin; x < region.xMax; x++)
                {
                    int i = y * Width + x;
                    int delta = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
                    if (delta > 15) changed++;
                }
            return changed;
        }
    }
}
