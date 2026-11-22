using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Field;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class FieldPropArtworkTests
    {
        [Test]
        public void SavedPalettesCoverEveryExistingFieldPropUnitUpgradeAndTower()
        {
            var field = AssetDatabase.LoadAssetAtPath<SceneSpritePalette>(
                "Assets/Settings/Game/FieldPropPalette.asset");
            foreach (string id in new[] { "hedge", "stones", "water", "bridge", "box", "debris", "weeds", "mud" })
                CheckSprite(field, id);
            var siege = AssetDatabase.LoadAssetAtPath<SceneSpritePalette>(
                "Assets/Settings/Siege/SiegePropPalette.asset");
            var game = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset")
                .CreateSiegeCatalog();
            var content = game.Content;
            foreach (var encounter in content.Encounters)
                foreach (string id in encounter.UnitIds)
                {
                    CheckSprite(siege, game.Unit(id).Icon);
                    CheckSprite(siege, game.Unit(game.Unit(id).UpgradeId).Icon);
                }
            foreach (var tower in content.Towers) CheckSprite(siege, tower.Visual);
            CheckSprite(siege, "paw");
            foreach (var palette in new[] { field, siege })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(palette.Texture));
                Assert.That(importer.isReadable || importer.mipmapEnabled, Is.False);
                Assert.That(palette.Texture.width, Is.LessThanOrEqualTo(512));
                Assert.That(palette.Texture.height, Is.LessThanOrEqualTo(512));
            }
        }

        private static void CheckSprite(SceneSpritePalette palette, string id)
        {
            Assert.That(palette, Is.Not.Null);
            Assert.That(palette.TryGet(id, out var sprite), Is.True, id);
            Assert.That(sprite.texture, Is.SameAs(palette.Texture));
            Assert.That(sprite.rect.width, Is.LessThanOrEqualTo(128));
            Assert.That(sprite.rect.height, Is.LessThanOrEqualTo(128));
        }

        [Test]
        public void CurrentPanelsAndPooledCardKeepExplicitSamePageBindings()
        {
            foreach (string path in new[] { "Assets/Prefabs/Panels/Panel_Approach.prefab",
                "Assets/Prefabs/Panels/Panel_SiegeTrial.prefab", "Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab" })
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var graphics = root.GetComponentsInChildren<Graphic>(true).Where(graphic =>
                    graphic is OutingFieldGraphic || graphic.GetType().Name == "SiegeBattlefield" ||
                    graphic.GetType().Name == "SiegeCarryOverlay" || graphic.GetType().Name == "SiegePropIcon");
                Assert.That(graphics, Is.Not.Empty, path);
                foreach (var graphic in graphics)
                {
                    var serialized = new SerializedObject(graphic);
                    var palette = (SceneSpritePalette)serialized.FindProperty("_spritePalette").objectReferenceValue;
                    Assert.That(palette, Is.Not.Null, graphic.name);
                    Assert.That(graphic.mainTexture, Is.SameAs(palette.Texture));
                }
            }
        }
    }
}
