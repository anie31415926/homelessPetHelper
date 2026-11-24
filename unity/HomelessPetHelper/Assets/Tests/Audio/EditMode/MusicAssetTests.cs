using System.Linq;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.Audio.EditMode
{
    public sealed class MusicAssetTests
    {
        [Test]
        public void ThreeTemporaryLoopsHaveExplicitNativeAndWebglImportsSeparateFromEffects()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            Assert.That(content.MusicCatalog, Is.Not.Null.And.Not.SameAs(content.AudioCatalog));
            Assert.That(content.AudioCatalog.CreateCues().All(cue => cue.Channel == AudioChannel.Effects), Is.True);
            var cues = content.MusicCatalog.CreateCues();
            Assert.That(cues.Select(cue => cue.Id), Is.EquivalentTo(new[] { GameAudioCues.StationMusic,
                GameAudioCues.ExploreMusic, GameAudioCues.RescueMusic }));
            var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            foreach (var cue in cues)
            {
                Assert.That(cue.Channel, Is.EqualTo(AudioChannel.Music));
                Assert.That(cue.Policy.Loop, Is.True);
                Assert.That(cue.Policy.Gain, Is.EqualTo(0.65f));
                Assert.That(cue.Clip, Is.Not.Null);
                Assert.That(cue.Clip.channels, Is.EqualTo(2));
                Assert.That(cue.Clip.frequency, Is.EqualTo(32000));
                Assert.That(cue.Clip.length, Is.EqualTo(32).Within(0.02));
                string path = AssetDatabase.GetAssetPath(cue.Clip);
                Assert.That(dependencies, Does.Contain(path));
                Assert.That(path, Does.Contain("PlaceholderMusic/BGM_").And.EndsWith("_Placeholder.wav"));
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.userData, Does.Contain("temporary;not-final"));
                Assert.That(importer.forceToMono, Is.False);
                Assert.That(importer.loadInBackground, Is.False);
                CheckSettings(importer.defaultSampleSettings, AudioClipLoadType.CompressedInMemory);
                Assert.That(importer.ContainsSampleSettingsOverride("Standalone"), Is.True);
                CheckSettings(importer.GetOverrideSampleSettings("Standalone"), AudioClipLoadType.Streaming);
                Assert.That(importer.ContainsSampleSettingsOverride("WebGL"), Is.False);
            }
        }

        private static void CheckSettings(AudioImporterSampleSettings settings, AudioClipLoadType loadType)
        {
            Assert.That(settings.preloadAudioData, Is.True);
            Assert.That(settings.loadType, Is.EqualTo(loadType));
            Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis));
            Assert.That(settings.sampleRateSetting, Is.EqualTo(AudioSampleRateSetting.OverrideSampleRate));
            Assert.That(settings.sampleRateOverride, Is.EqualTo(32000));
            Assert.That(settings.quality, Is.EqualTo(0.30f).Within(0.001f));
        }
    }
}
