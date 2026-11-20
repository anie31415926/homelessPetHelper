using System;
using System.Linq;
using HomelessPetHelper.Game;
using NUnit.Framework;
using UnityEditor;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class MainAssetTests
    {
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
            Assert.That(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path),
                Is.EqualTo(new[] { "Assets/Scenes/Main.unity" }));
        }
    }
}
