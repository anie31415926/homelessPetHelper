using System;
using System.Linq;
using HomelessPetHelper.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>Refreshes the saved skin and audio rail while preserving business controls.</summary>
    public static class LevelOverlayRefresh
    {
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated LevelUiRefresh.");
            var skin = new LevelOverlaySkin();
            foreach (string path in new[] { "Assets/Prefabs/Widgets/Widget_LevelOverlays.prefab",
                "Assets/Prefabs/Panels/Panel_Approach.prefab", "Assets/Prefabs/Panels/Panel_SiegeTrial.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    RefreshSkin(root, skin);
                    if (root.GetComponent<LevelOverlayView>() != null) RefreshAudio(root, skin);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        private static void RefreshSkin(GameObject root, LevelOverlaySkin skin)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null || AssetDatabase.GetAssetPath(image.sprite) != LevelOverlaySkin.TexturePath)
                    continue;
                image.sprite = skin.Sprite(image.sprite.name);
                image.color = Color.white;
                if (image.sprite.name == "Paper") image.pixelsPerUnitMultiplier = .8f;
                else if (image.sprite.name == "Action" || image.sprite.name == "Secondary")
                    image.pixelsPerUnitMultiplier = .62f;
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var background = button.GetComponent<Image>();
                if (background == null || background.sprite == null ||
                    AssetDatabase.GetAssetPath(background.sprite) != LevelOverlaySkin.TexturePath) continue;
                var colors = button.colors;
                colors.normalColor = colors.selectedColor = Color.white;
                colors.highlightedColor = new Color32(245, 255, 243, 255);
                colors.pressedColor = new Color32(218, 238, 214, 255);
                colors.disabledColor = new Color32(180, 185, 179, 160);
                button.colors = colors;
                button.GetComponentInChildren<Text>(true).color = LevelOverlaySkin.Ink;
            }
            if (root.GetComponent<LevelOverlayView>() == null) return;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                if (text.name != "SettingsStatus") text.color = LevelOverlaySkin.Ink;
            root.GetComponentsInChildren<Image>(true).Single(image => image.name == "Modal")
                .color = new Color32(45, 65, 49, 88);
        }

        private static void RefreshAudio(GameObject root, LevelOverlaySkin skin)
        {
            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
            {
                var track = slider.GetComponentsInChildren<Image>(true).Single(image => image.name == "SkinTrack");
                var fill = slider.fillRect.GetComponent<Image>();
                var fillArea = (RectTransform)slider.fillRect.parent;
                var handleArea = (RectTransform)slider.handleRect.parent;
                // Zero-height handle rail lets Slider drive its vertical anchors without stretching the thumb.
                Rail(track.rectTransform, 24); Rail(fillArea, 24); Rail(handleArea, 0);
                skin.Surface(track, "VolumeTrack");
                skin.Surface(fill, "VolumeFill");
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                Place(slider.fillRect, 0, 0, 1, 1);
                var thumb = slider.handleRect;
                thumb.anchorMin = Vector2.zero; thumb.anchorMax = Vector2.up;
                thumb.anchoredPosition = Vector2.zero; thumb.sizeDelta = new Vector2(80, 80);
                var knob = thumb.GetComponent<Image>();
                knob.sprite = skin.Sprite("VolumeThumb"); knob.type = Image.Type.Simple;
                knob.color = Color.white; knob.preserveAspect = true; knob.raycastTarget = false;
                slider.targetGraphic = knob;
                var colors = slider.colors;
                colors.normalColor = colors.selectedColor = Color.white;
                colors.highlightedColor = new Color32(246, 255, 245, 255);
                colors.pressedColor = new Color32(216, 237, 211, 255);
                slider.colors = colors;
                // Refresh Slider's cached fill image after assigning the horizontal fill mode.
                var fillRect = slider.fillRect; slider.fillRect = null; slider.fillRect = fillRect;
                slider.SetValueWithoutNotify(slider.value);
            }
            foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
            {
                var mark = (Image)toggle.graphic;
                mark.sprite = skin.Sprite("Paw"); mark.type = Image.Type.Simple;
                mark.preserveAspect = true; mark.color = Color.white;
            }
        }

        private static void Rail(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0, .5f); rect.anchorMax = new Vector2(1, .5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(-80, height);
        }
    }
}
