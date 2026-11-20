using System.Linq;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.Audio.EditMode
{
    public sealed class AudioAssetTests
    {
        [Test]
        public void MainContentHasSixUniquePlayableShortEffectsWithExplicitImportSettings()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            Assert.That(content.AudioCatalog, Is.Not.Null);
            var cues = content.AudioCatalog.CreateCues();
            Assert.That(cues.Select(cue => cue.Id), Is.EquivalentTo(new[] { GameAudioCues.Tap, GameAudioCues.Open,
                GameAudioCues.Merge, GameAudioCues.RescueSuccess, GameAudioCues.Reward, GameAudioCues.Notice }));
            foreach (var cue in cues)
            {
                Assert.That(cue.Channel, Is.EqualTo(AudioChannel.Effects));
                Assert.That(cue.Clip, Is.Not.Null);
                Assert.That(cue.Clip.channels, Is.EqualTo(1));
                Assert.That(cue.Clip.frequency, Is.EqualTo(44100));
                Assert.That(cue.Clip.length, Is.InRange(0.02f, 2f));
                Assert.That(cue.Clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
                Assert.That(cue.Policy.Loop, Is.False);
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(cue.Clip));
                Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.True);
                Assert.That(importer.loadInBackground, Is.False);
                Assert.That(importer.forceToMono, Is.True);
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
                Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM));
            }
            var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            Assert.That(cues.All(cue => dependencies.Contains(AssetDatabase.GetAssetPath(cue.Clip))), Is.True);
        }

        [TestCase("Assets/Prefabs/Panels/Panel_StationLoop.prefab", true)]
        [TestCase("Assets/Prefabs/Panels/Panel_CityMap.prefab", true)]
        [TestCase("Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab", false)]
        public void StaticButtonsArePreparedAndReusableCellDoesNotShadowParentScope(string path, bool panel)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var buttons = root.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Length, Is.GreaterThan(0));
            Assert.That(buttons.All(button => button.GetComponent<ButtonAudio>() != null), Is.True);
            Assert.That(root.GetComponent<AudioScope>() != null, Is.EqualTo(panel));
        }
    }
}
