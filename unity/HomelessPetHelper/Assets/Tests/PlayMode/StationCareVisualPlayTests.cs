#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Station.Cleaning;
using HomelessPetHelper.Modules.Station.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class StationCareVisualPlayTests
    {
        [UnityTest]
        public IEnumerator MainPrefabUsesTheRescuedCatAndDogAndEmitsOnlyOnEffectiveStrokes()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/Panel_StationLoop.prefab");
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var panel = Object.Instantiate(prefab).GetComponent<StationPanelView>();
            panel.gameObject.SetActive(true);
            var system = new GameObject("CareTestInput", typeof(EventSystem));
            var camera = new GameObject("CareTestCamera", typeof(Camera)).GetComponent<Camera>();
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            camera.clearFlags = CameraClearFlags.SolidColor;
            try
            {
                foreach (bool dog in new[] { false, true })
                {
                    string species = dog ? "Dog" : "Cat";
                    var legacy = content.Artwork.Get(dog, out var mask);
                    panel.ShowPage(true, false);
                    panel.ShowSummary(species, legacy);
                    yield return null;
                    var portrait = panel.GetComponentInChildren<PetPortraitView>();
                    var portraitFrame = portrait.GetComponentInChildren<RawImage>();
                    Assert.That(portraitFrame.texture.name,
                        Is.EqualTo(dog ? "T_Station_ShibaMotion" : "T_CatPortraitMotion"));
                    Rect firstFrame = portraitFrame.uvRect;
                    yield return new WaitForSecondsRealtime(.35f);
                    Assert.That(portraitFrame.uvRect, Is.Not.EqualTo(firstFrame));
                    var actual = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        dog ? "Assets/Art/PetSit/T_Station_ShibaMotion.png" : "Assets/Art/PetMotion/T_Pet_original.png");
                    foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844),
                        new Vector2Int(432, 960), new Vector2Int(768, 1024) })
                    {
                        using var shot = new Shot(camera, size);
                        yield return Exercise(panel, legacy, actual, mask, shot, species, dog);
                    }
                }
            }
            finally
            {
                Object.Destroy(panel.gameObject);
                Object.Destroy(camera.gameObject);
                Object.Destroy(system);
            }
        }

        private static IEnumerator Exercise(StationPanelView panel, Sprite legacy, Texture2D actual, byte[] mask,
            Shot shot, string species, bool groom)
        {
            var dirt = CreateField(mask, false, groom ? 0 : 65);
            var fur = CreateField(mask, true, 55);
            panel.ShowPage(false, true, true);
            panel.ShowDetail(groom ? "狗狗" : "猫咪", string.Empty, groom ? "梳理 30%" : "清洁 30%",
                legacy, dirt, true, true, new StationCareAvailability(true, true), false, fur);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var surface = panel.Surface;
            surface.SendMessage("OnApplicationFocus", true);
            var pictures = surface.GetComponentsInChildren<Image>(true);
            Assert.That(actual, Is.Not.Null, species + " source texture is missing");
            var picture = pictures.SingleOrDefault(item => item.sprite != null && item.sprite.texture == actual);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actual, out string guid, out long fileId);
            Assert.That(picture, Is.Not.Null, $"{species} expected {actual.name} ({guid}/{fileId}); saw " +
                string.Join(", ", pictures.Select(item => item.name + "=" + item.sprite?.name)));
            var fx = surface.GetComponentInChildren<CareBubbleView>();
            Assert.That(fx.mainTexture.name, Is.EqualTo("T_Station_CareEffects"));
            Rect coat = picture.rectTransform.rect;
            Assert.That(coat.width / coat.height,
                Is.EqualTo(picture.sprite.rect.width / picture.sprite.rect.height).Within(.01));
            Assert.That(surface.GetComponentsInChildren<RawImage>().All(image =>
                Vector2.Distance(image.rectTransform.rect.size, coat.size) < .1f), Is.True);
            var active = groom ? fur : dirt;
            using var stroke = new ActionStroke(surface, active, legacy, dirt, fur);
            long amount = active.RemainingAmount;
            Tap(surface, (RectTransform)surface.transform, Vector2.zero, shot.Camera);
            Assert.That(active.RemainingAmount, Is.EqualTo(amount));
            Assert.That(fx.ActiveCount, Is.Zero);
            var contact = FindContact(surface, picture.rectTransform, shot.Camera, active, amount, species);
            Assert.That(active.RemainingAmount, Is.LessThan(amount));
            Assert.That(fx.ActiveCount, Is.EqualTo(3));
            yield return new WaitForSecondsRealtime(.13f);
            shot.Save(species + (groom ? "-groom" : "-wash"));
            surface.SetCleaning(false);
            Assert.That(fx.ActiveCount, Is.Zero);
            Tap(surface, picture.rectTransform, contact, shot.Camera);
            Assert.That(fx.ActiveCount, Is.Zero);
            panel.ShowPage(false, false, false);
        }

        private static DirtField CreateField(byte[] mask, bool fur, int amount)
        {
            int size = AnimalCoatPattern.Resolution;
            var samples = new byte[mask.Length];
            for (int index = 0; index < samples.Length; index++)
                samples[index] = AnimalCoatPattern.Sample(index % size, index / size, 17, fur, amount);
            return new DirtField(size, size, mask, samples);
        }

        private static Vector2 FindContact(DirtSurfaceView surface, RectTransform coat, Camera camera,
            DirtField field, long initial, string species)
        {
            foreach (float y in new[] { .54f, .62f, .46f, .38f })
            foreach (float x in new[] { .52f, .43f, .62f, .35f, .72f })
            {
                var point = new Vector2(x, y);
                Tap(surface, coat, point, camera);
                if (field.RemainingAmount < initial) return point;
            }
            Assert.Fail(species + ": no visible coat contact registered.");
            return default;
        }

        private static void Tap(DirtSurfaceView surface, RectTransform rect, Vector2 uv, Camera camera)
        {
            var point = Rect.NormalizedToPoint(rect.rect, uv);
            var input = new PointerEventData(EventSystem.current) { pointerId = 1 };
            input.pointerPressRaycast = new RaycastResult { module = surface.GetComponentInParent<GraphicRaycaster>() };
            input.position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(point));
            surface.OnPointerDown(input);
            surface.OnPointerUp(input);
        }

        private sealed class ActionStroke : System.IDisposable
        {
            private readonly DirtSurfaceView _surface;
            private readonly DirtField _active, _dirt, _fur;
            private readonly Sprite _art;

            internal ActionStroke(DirtSurfaceView surface, DirtField active, Sprite art, DirtField dirt, DirtField fur)
            {
                _surface = surface;
                _active = active;
                _art = art;
                _dirt = dirt;
                _fur = fur;
                _surface.StrokeRequested += Stroke;
            }

            private void Stroke(Vector2 point, int amount)
            {
                Assert.That(_active.ContainsCoat(point.x, point.y), Is.True);
                if (_active.TryErase(point.x, point.y, .12f, amount)) _surface.Refresh(_art, _dirt, _fur);
            }

            public void Dispose() => _surface.StrokeRequested -= Stroke;
        }

        private sealed class Shot : System.IDisposable
        {
            private readonly RenderTexture _target;
            internal Camera Camera { get; }

            internal Shot(Camera camera, Vector2Int size)
            {
                Camera = camera;
                _target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = _target;
            }

            internal void Save(string name)
            {
                Canvas.ForceUpdateCanvases();
                Camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = _target;
                var image = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
                    image.Apply();
                    Directory.CreateDirectory("StationCareVisualEvidence");
                    File.WriteAllBytes($"StationCareVisualEvidence/{name}-{_target.width}x{_target.height}.png",
                        image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.Destroy(image);
                }
            }

            public void Dispose()
            {
                Camera.targetTexture = null;
                _target.Release();
                Object.Destroy(_target);
            }
        }
    }
}
#endif
