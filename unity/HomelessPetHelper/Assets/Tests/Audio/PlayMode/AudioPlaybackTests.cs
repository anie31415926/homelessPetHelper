using System.Collections;
using System.Linq;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HomelessPetHelper.Tests.Audio.PlayMode
{
    public sealed class AudioPlaybackTests
    {
        private GameObject _root;
        private AudioClip _clip;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            _root = new GameObject("AudioPlaybackTest");
            _clip = AudioClip.Create("GeneratedTestClipOnly", 441000, 1, 44100, false);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            if (_clip != null) Object.Destroy(_clip);
            yield return null;
        }

        private AudioHost CreateHost()
        {
            var host = _root.AddComponent<AudioHost>();
            host.SendMessage("OnApplicationFocus", true);
            host.Initialize(new[]
            {
                new AudioCue("music", AudioChannel.Music, _clip, new AudioCuePolicy(loop: true)),
                new AudioCue("effect", AudioChannel.Effects, _clip, new AudioCuePolicy(minIntervalSeconds: 0))
            }, AudioPreferences.Default);
            return host;
        }

        [UnityTest]
        public IEnumerator MainOwnsOneAudioSessionListenerAndFixedSourcePoolAcrossReload()
        {
            var root = Object.FindObjectOfType<MainGameRoot>();
            Assert.That(root.Audio, Is.Not.Null);
            var previous = root.Audio.Playback;
            Assert.That(Object.FindObjectsOfType<AudioHost>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<AudioListener>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<AudioSource>().Length, Is.EqualTo(9));
            Assert.That(previous.PlayEffect("unassigned"), Is.EqualTo(AudioPlayResult.UnknownCue));
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            Assert.That(previous.IsDisposed, Is.True);
            Assert.That(Object.FindObjectsOfType<AudioHost>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<AudioListener>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<AudioSource>().Length, Is.EqualTo(9));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RealSourcesUseSeparateVolumesAndPauseOnlyOwnedMusicWithoutEffectReplay()
        {
            var host = CreateHost();
            var service = host.Service;
            Assert.That(service.PlayMusic("music"), Is.EqualTo(AudioPlayResult.Played));
            Assert.That(service.PlayEffect("effect"), Is.EqualTo(AudioPlayResult.Played));
            yield return null;
            var sources = _root.GetComponentsInChildren<AudioSource>();
            Assert.That(sources.Length, Is.EqualTo(9));
            var music = sources.Single(source => source.loop);
            var effect = sources.Single(source => !source.loop && source.clip != null);
            Assert.That(music.isPlaying, Is.True);
            Assert.That(effect.isPlaying, Is.True);
            Assert.That(sources.All(source => !source.playOnAwake && source.spatialBlend == 0), Is.True);
            service.ApplyPreferences(new AudioPreferences(new AudioBusSettings(0.5f), new AudioBusSettings(0.4f),
                new AudioBusSettings(0.8f)));
            Assert.That(music.volume, Is.EqualTo(0.2f).Within(0.00001));
            Assert.That(effect.volume, Is.EqualTo(0.4f).Within(0.00001));
            music.timeSamples = 88200;
            host.SendMessage("OnApplicationFocus", false);
            host.SendMessage("OnApplicationPause", true);
            host.SendMessage("OnApplicationFocus", true);
            Assert.That(music.isPlaying, Is.False);
            Assert.That(effect.clip, Is.Null);
            service.SetSuspended(AudioPauseReason.GameplayPaused, true);
            host.SendMessage("OnApplicationPause", false);
            Assert.That(service.IsSuspended, Is.True);
            service.SetSuspended(AudioPauseReason.GameplayPaused, false);
            yield return null;
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(88200), "Resume must not restart at sample zero.");
            Assert.That(effect.clip, Is.Null);
            Assert.That(AudioListener.pause, Is.False, "No global audio pause mutation.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DisableAndDestroyDoNotLeakOrOverrideAnotherPauseReason()
        {
            var host = CreateHost();
            var service = host.Service;
            service.PlayMusic("music");
            service.SetSuspended(AudioPauseReason.ExternalInterruption, true);
            host.enabled = false;
            host.enabled = true;
            Assert.That(service.IsSuspended, Is.True);
            service.SetSuspended(AudioPauseReason.ExternalInterruption, false);
            Assert.That(service.IsSuspended, Is.False);
            Object.Destroy(_root);
            yield return null;
            Assert.That(service.IsDisposed, Is.True);
            Assert.That(service.PlayEffect("effect"), Is.EqualTo(AudioPlayResult.Disposed));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InactiveInitializationAndOwnerReactivationPreserveExplicitPauseReasons()
        {
            _root.SetActive(false);
            var host = _root.AddComponent<AudioHost>();
            host.Initialize(new[]
            {
                new AudioCue("music", AudioChannel.Music, _clip, new AudioCuePolicy(loop: true))
            }, AudioPreferences.Default);
            Assert.That(host.Service.IsSuspended, Is.True);
            _root.SetActive(true);
            host.SendMessage("OnApplicationFocus", true);
            Assert.That(host.Service.IsSuspended, Is.False);
            Assert.That(host.Service.PlayMusic("music"), Is.EqualTo(AudioPlayResult.Played));
            yield return null;
            var music = _root.GetComponentsInChildren<AudioSource>().Single(source => source.loop);
            Assert.That(music.isPlaying, Is.True);
            _root.SetActive(false);
            yield return null;
            Assert.That(host.Service.IsSuspended, Is.True);
            _root.SetActive(true);
            yield return null;
            Assert.That(host.Service.IsSuspended, Is.False);
            Assert.That(music.isPlaying, Is.True);
            host.Service.SetSuspended(AudioPauseReason.ExternalInterruption, true);
            _root.SetActive(false);
            _root.SetActive(true);
            Assert.That(host.Service.IsSuspended, Is.True);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
