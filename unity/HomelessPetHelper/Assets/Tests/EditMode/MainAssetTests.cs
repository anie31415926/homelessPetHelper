using System;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Rescue.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class MainAssetTests
    {
        [Test]
        public void ApproachPrefabHasCompleteStaticBindingsAndSharedScreenAdaptation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/Panel_Approach.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Has.None.Null);
            Assert.That(prefab.GetComponent<ApproachPanelView>(), Is.Not.Null);
            var view = new SerializedObject(prefab.GetComponent<ApproachPanelView>());
            var legacy = (ApproachBoardView)view.FindProperty("_board").objectReferenceValue;
            var scene = (ApproachBoardView)view.FindProperty("_sceneBoard").objectReferenceValue;
            Assert.That(scene, Is.Not.SameAs(legacy));
            foreach (var board in new[] { legacy, scene })
            {
                Assert.That(board, Is.Not.Null);
                Assert.That(board.CellCount, Is.EqualTo(new ApproachCatalog(JsonUtility.FromJson<ApproachCatalogData>(
                    AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Game/ApproachConfig.json").text))
                    .Get(ApproachMode.Cleanup).CellCount));
                Assert.That(board.GetComponentsInChildren<ApproachCellView>(true).Length, Is.EqualTo(board.CellCount));
                Assert.That(Enumerable.Range(0, board.CellCount).Select(board.Cell).Distinct().Count(),
                    Is.EqualTo(board.CellCount));
            }
            Assert.That(prefab.GetComponentsInChildren<ApproachBoardView>(true).Length, Is.EqualTo(2));
            var field = prefab.GetComponent<OutingFieldView>();
            Assert.That(field, Is.Not.Null);
            var background = (Image)new SerializedObject(field).FindProperty("_background").objectReferenceValue;
            var safe = prefab.GetComponentInChildren<SafeAreaView>(true);
            Assert.That(background.transform.parent, Is.SameAs(prefab.transform));
            Assert.That(background.transform.GetSiblingIndex(), Is.EqualTo(safe.transform.GetSiblingIndex() - 1),
                "The full-screen artwork must cover legacy fill and remain behind all safe-area controls.");
            var terrain = prefab.GetComponentInChildren<OutingFieldGraphic>(true);
            Assert.That(terrain.GetComponent<CanvasRenderer>(), Is.Not.Null);
            Assert.That(terrain.raycastTarget, Is.False);
            Assert.That(prefab.GetComponentInChildren<SafeAreaView>(true), Is.Not.Null);
            var scaler = prefab.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1080, 1920)));
            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(value => value.GetType().Namespace == typeof(ApproachPanelView).Namespace))
            {
                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(property.objectReferenceValue, Is.Not.Null,
                            component.name + ": " + property.propertyPath);
            }
        }

        [Test]
        public void MainUsesRuntimeContentAndNoPreviewAssetsOrEditorTypes()
        {
            var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            Assert.That(dependencies.Any(path => path.StartsWith("Assets/EditorScenes/", StringComparison.Ordinal)),
                Is.False);
            Assert.That(dependencies, Does.Contain("Assets/Settings/Game/GameContent.asset"));
            Assert.That(dependencies, Does.Contain("Assets/Settings/Siege/SiegeTrialData.json"));
            Assert.That(dependencies, Does.Contain("Assets/Art/PetMotion/T_Pet_Atlas.png"));
            Assert.That(dependencies.Any(path => path.Contains("Panel_RescueCase")), Is.False);
            Assert.That(typeof(MainGameRoot).Assembly.GetReferencedAssemblies()
                .Any(assembly => assembly.Name.Contains("Editor")), Is.False);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            Assert.DoesNotThrow(content.Validate);
            Assert.That(content.Font.HasCharacter('救'), Is.True);
            Assert.That(content.Font.HasCharacter('助'), Is.True);
            Assert.That(dependencies, Does.Contain("Assets/Settings/Game/ApproachConfig.json"));
            Assert.That(dependencies, Does.Contain("Assets/Settings/Game/YardFieldConfig.json"));
            Assert.That(dependencies, Does.Contain("Assets/Art/Outing/T_Outing_DogNatural.png"));
            Assert.That(dependencies, Does.Contain("Assets/Art/Outing/T_Outing_CatNatural.png"));
            foreach (TextAsset source in new[] { content.ApproachText, content.MapText, content.SiegeText })
                foreach (char character in source.text)
                    if (character > 127 && !char.IsWhiteSpace(character))
                        Assert.That(content.Font.HasCharacter(character), Is.True,
                            source.name + " glyph: " + character);
            var stationText = new OptionPreviewText(content.StationText);
            foreach (var key in new[] { "save.loadFailed", "save.writeFailed", "save.recovered", "loop.dog",
                "save.outingBoundary", "save.careBoundary" })
                foreach (char character in stationText.Get(key, 1))
                    if (!char.IsWhiteSpace(character))
                        Assert.That(content.Font.HasCharacter(character), Is.True, key + ": " + character);
            Assert.That(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path),
                Is.EqualTo(new[] { "Assets/Scenes/SC_Loading.unity", "Assets/Scenes/Main.unity" }));
        }
    }
}
