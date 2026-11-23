using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.Audio.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>Imports this small shared page and applies its reusable nine-slice surfaces in Prefab Mode.</summary>
    internal sealed class LevelOverlaySkin
    {
        internal const string TexturePath = "Assets/Art/UI/T_UI_LevelControls.png";
        private const string ManifestPath = "Assets/Art/UI/LevelControlsSlices.json";
        internal static readonly Color Ink = new Color32(66, 89, 74, 255);
        internal static readonly Color Cream = new Color32(255, 251, 235, 255);
        private readonly Dictionary<string, Sprite> _sprites;

        [Serializable] private sealed class Slices { public int Height; public Slice[] Sprites; }
        [Serializable] private sealed class Slice
        {
            public string Name;
            public int X, Y, Width, Height;
            public Vector4 Border;
        }

        internal LevelOverlaySkin()
        {
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100;
            var slices = JsonUtility.FromJson<Slices>(File.ReadAllText(ManifestPath));
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().ToDictionary(rect => rect.name);
            var rects = slices.Sprites.Select(slice => new SpriteRect
            {
                name = slice.Name,
                rect = new Rect(slice.X, slices.Height - slice.Y - slice.Height, slice.Width, slice.Height),
                pivot = new Vector2(.5f, .5f), alignment = SpriteAlignment.Center,
                border = slice.Border,
                spriteID = existing.TryGetValue(slice.Name, out var previous) ? previous.spriteID : GUID.Generate()
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            _sprites = AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);
        }

        internal Sprite Sprite(string name) => _sprites[name];

        internal void Surface(Image image, string name, bool hit = false)
        {
            image.sprite = _sprites[name]; image.type = Image.Type.Sliced;
            image.color = Color.white; image.raycastTarget = hit;
        }

        internal Image Icon(Transform parent, string name, Color color)
        {
            var image = Rect("SkinIcon", parent).gameObject.AddComponent<Image>();
            image.sprite = _sprites[name]; image.preserveAspect = true;
            image.color = color; image.raycastTarget = false;
            return image;
        }

        internal void Button(Button button, bool primary, string icon = null, int size = 42)
        {
            Surface(button.GetComponent<Image>(), primary ? "Action" : "Secondary", true);
            var metrics = AssetDatabase.LoadAssetAtPath<UiLayoutMetrics>("Assets/Settings/UI/LayoutMetrics.asset");
            var audio = button.GetComponent<ButtonAudio>() ?? button.gameObject.AddComponent<ButtonAudio>();
            audio.CueId = "ui.tap";
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = metrics.buttonHover;
            colors.pressedColor = metrics.buttonPressed;
            colors.disabledColor = Color.white;
            button.colors = colors;
            var state = button.GetComponent<PaperButtonState>() ?? button.gameObject.AddComponent<PaperButtonState>();
            MainSceneAuthor.Bind(state, "_control", button);
            MainSceneAuthor.Bind(state, "_surface", button.GetComponent<Image>());
            MainSceneAuthor.Bind(state, "_metrics", metrics);
            var label = button.GetComponentInChildren<Text>(true);
            Text(label, size, TextAnchor.MiddleCenter);
            label.color = metrics.ink;
            var layout = Horizontal(button.transform, 16);
            layout.padding = new RectOffset(24, 24, 8, 8);
            layout.childForceExpandWidth = false;
            var labelSize = Element(label.transform, 0);
            labelSize.flexibleWidth = 1;
            labelSize.preferredWidth = 0;
            if (icon != null)
            {
                var image = Icon(button.transform, icon, Color.white);
                image.transform.SetAsFirstSibling();
                var iconSize = Element(image.transform, 0);
                iconSize.preferredWidth = 44;
                iconSize.flexibleWidth = 0;
            }
            var element = Element(button.transform, 116);
            element.minWidth = element.preferredWidth = 0;
            element.flexibleWidth = 1;
        }

        internal static void Text(Text text, int size, TextAnchor alignment)
        {
            text.fontSize = size; text.resizeTextForBestFit = false;
            text.alignment = alignment; text.color = Ink;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1.12f;
        }

        internal static LayoutElement Element(Transform target, float height)
        {
            var element = target.GetComponent<LayoutElement>();
            if (element == null) element = target.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            if (height > 0) element.preferredHeight = height;
            element.flexibleHeight = 0;
            return element;
        }

        internal static HorizontalLayoutGroup Horizontal(Transform target, float spacing)
        {
            var layout = target.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return layout;
        }

        internal static VerticalLayoutGroup Content(RectTransform target, int horizontal, int vertical)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(horizontal, horizontal, vertical, vertical);
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = target.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return layout;
        }
    }
}
