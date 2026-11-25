using System;
using System.Collections;
using System.Linq;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class MainMusicTests
    {
        private MainSceneStorageScope _storage;
        private MainGameRoot _main;
        private MemorySaveStorage _audioStorage;

        [UnitySetUp]
        public IEnumerator Load()
        {
            _audioStorage = new MemorySaveStorage();
            _storage = new MainSceneStorageScope(createAudioStorage: () => _audioStorage);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            _main = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
            FocusAudio(true);
            yield return null;
        }

        [TearDown] public void ReleaseStorage() => _storage?.Dispose();

        private static void FocusAudio(bool focused) =>
            UnityEngine.Object.FindObjectOfType<AudioHost>().SendMessage("OnApplicationFocus", focused);

        private AudioSource Source() => _main.GetComponentsInChildren<AudioSource>(true)
            .Single(source => source.clip != null && source.clip.name.StartsWith("BGM_", StringComparison.Ordinal));

        private IEnumerator WaitMusic(string id)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (_main.Audio.Playback.CurrentMusicId != id && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            var content = Resources.FindObjectsOfTypeAll<GameContent>().Single(item => item.name == "GameContent");
            string readiness = string.Join(",", content.MusicCatalog.CreateCues().Select(cue =>
                cue.Id + ":" + cue.Clip.loadState));
            Assert.That(_main.Audio.Playback.CurrentMusicId, Is.EqualTo(id),
                "last=" + _main.Music.LastPlayResult + ";audioPaused=" + _main.Audio.Playback.IsSuspended +
                ";flowPaused=" + _main.Presenter.Session.Flow.IsSuspended + ";clips=" + readiness);
            Assert.That(Source().loop, Is.True);
            Assert.That(Source().isPlaying, Is.True);
            Assert.That(_main.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(9));
        }

        [UnityTest]
        public IEnumerator RealMainRoutesStationMapOpeningBattleAndReturnWithoutRestartingSharedFieldMusic()
        {
            yield return WaitMusic(GameAudioCues.StationMusic);
            var station = UnityEngine.Object.FindObjectOfType<StationPanelView>();
            MainApproachDriver.Click(station, "Restart");
            yield return WaitMusic(GameAudioCues.ExploreMusic);
            MainApproachDriver.Click(UnityEngine.Object.FindObjectOfType<CityMapView>(), "Primary");
            yield return WaitMusic(GameAudioCues.RescueMusic);
            var clip = Source().clip;
            int sample = Source().timeSamples;
            MainApproachDriver.Complete();
            yield return null;
            Assert.That(Source().clip, Is.SameAs(clip));
            Assert.That(Source().timeSamples, Is.GreaterThanOrEqualTo(sample), "Same field cue must not restart.");
            _main.Presenter.Overlays.OpenPause();
            yield return null;
            Assert.That(_main.Audio.Playback.IsSuspended, Is.False, "pause menu must not stop music.");
            Assert.That(Source().isPlaying, Is.True, "music keeps playing on the pause menu.");
            FocusAudio(false);
            yield return null;
            Assert.That(Source().isPlaying, Is.False, "lost focus still stops music.");
            _main.Presenter.Overlays.Resume();
            yield return null;
            Assert.That(Source().isPlaying, Is.False, "Gameplay resume must not clear lost focus.");
            FocusAudio(true);
            yield return WaitMusic(GameAudioCues.RescueMusic);
            // A popup return now steps back to the map (the outing stays alive), so the shared
            // field cue must keep playing instead of being restarted or swapped for the station cue.
            _main.Presenter.Overlays.RequestReturn();
            _main.Presenter.Overlays.Confirm();
            yield return WaitMusic(GameAudioCues.RescueMusic);
            Assert.That(_main.Audio.Playback.IsSuspended, Is.False,
                "the field-to-map return keeps the shared field cue alive.");
            Assert.That(Source().isPlaying, Is.True, "music keeps playing after returning to the map.");
            Assert.That(Source().clip, Is.SameAs(clip), "the same field cue survives the return chain.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PauseAndSettingsOverlaysKeepMusicPlayingWithoutRestart()
        {
            yield return WaitMusic(GameAudioCues.StationMusic);
            MainApproachDriver.Click(UnityEngine.Object.FindObjectOfType<StationPanelView>(), "Restart");
            yield return WaitMusic(GameAudioCues.ExploreMusic);
            MainApproachDriver.Click(UnityEngine.Object.FindObjectOfType<CityMapView>(), "Primary");
            yield return WaitMusic(GameAudioCues.RescueMusic);
            MainApproachDriver.Complete();
            yield return null;

            var overlays = _main.Presenter.Overlays;
            Assert.That(overlays, Is.Not.Null, "battle must own the shared overlay.");
            overlays.OpenPause();
            yield return null;
            Assert.That(overlays.Top, Is.EqualTo(LevelPopupKind.Pause));
            Assert.That(_main.Presenter.Session.Flow.IsSuspended, Is.True, "pause menu must still suspend gameplay.");
            Assert.That(_main.Audio.Playback.IsSuspended, Is.False, "pause menu must not stop music.");
            Assert.That(Source().isPlaying, Is.True, "music keeps playing on the pause menu.");
            int pauseSample = Source().timeSamples;

            overlays.OpenSettings();
            yield return null;
            yield return null;
            Assert.That(overlays.Top, Is.EqualTo(LevelPopupKind.Settings));
            Assert.That(overlays.IsSettingsOpen, Is.True);
            Assert.That(_main.Presenter.Session.Flow.IsSuspended, Is.True, "settings must keep gameplay paused.");
            Assert.That(_main.Audio.Playback.IsSuspended, Is.False, "settings must keep music audible for preview.");
            Assert.That(Source().isPlaying, Is.True, "music keeps playing so volume changes can be heard.");
            Assert.That(Source().timeSamples, Is.GreaterThanOrEqualTo(pauseSample),
                "settings must not restart music.");

            // Changing volume in settings is applied live without stopping the loop.
            _main.Audio.Playback.ApplyPreferences(new AudioPreferences(
                new AudioBusSettings(0.5f), new AudioBusSettings(0.25f), new AudioBusSettings(0.7f)));
            yield return null;
            Assert.That(Source().isPlaying, Is.True);
            Assert.That(Source().volume, Is.EqualTo(0.5f * 0.25f * 0.65f).Within(0.001f));

            overlays.CancelSettings();
            yield return null;
            Assert.That(overlays.Top, Is.EqualTo(LevelPopupKind.Pause), "settings cancel returns to pause page.");
            Assert.That(_main.Audio.Playback.IsSuspended, Is.False, "back to pause keeps music playing.");
            Assert.That(Source().isPlaying, Is.True, "music never stops across the overlay chain.");

            overlays.Resume();
            yield return null;
            Assert.That(_main.Audio.Playback.IsSuspended, Is.False, "resume keeps the uninterrupted music.");
            Assert.That(Source().isPlaying, Is.True, "music plays after leaving the pause page.");
            Assert.That(_main.Audio.Playback.CurrentMusicId, Is.EqualTo(GameAudioCues.RescueMusic));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MusicUsesExistingPreferencesMuteAndReloadWithoutLeakingVoices()
        {
            yield return WaitMusic(GameAudioCues.StationMusic);
            var preferences = new AudioPreferences(new AudioBusSettings(0.8f), new AudioBusSettings(0.25f),
                new AudioBusSettings(0.7f));
            _main.Audio.Playback.ApplyPreferences(preferences);
            Assert.That(Source().volume, Is.EqualTo(0.8f * 0.25f * 0.65f).Within(0.001f));
            _main.Audio.Playback.ApplyPreferences(new AudioPreferences(preferences.Master,
                new AudioBusSettings(0.25f, true), preferences.Effects));
            Assert.That(Source().volume, Is.Zero);
            Assert.That(Source().isPlaying, Is.True, "Mute retains loop position.");
            _main.Audio.Playback.ApplyPreferences(preferences);
            Assert.That(_main.Audio.TrySavePreferences(out _), Is.True);
            var previousMusic = _main.Music;
            var previousPlayer = _main.Audio.Playback;
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            _main = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
            FocusAudio(true);
            yield return WaitMusic(GameAudioCues.StationMusic);
            Assert.That(previousMusic.IsDisposed && previousPlayer.IsDisposed, Is.True);
            Assert.That(Source().volume, Is.EqualTo(0.13f).Within(0.001f));
            Assert.That(UnityEngine.Object.FindObjectsOfType<AudioSource>().Length, Is.EqualTo(9));
        }

        [UnityTest]
        public IEnumerator BrokenProgressKeepsMusicSilentWithoutDereferencingAnAbsentSession()
        {
            _storage.Dispose();
            var broken = new MemorySaveStorage();
            broken.Slots[0] = "preserved broken progress";
            _storage = new MainSceneStorageScope(() => broken);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            _main = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
            FocusAudio(true);
            yield return null;
            Assert.That(_main.Presenter.Progress.CanContinue, Is.False);
            Assert.That(_main.Presenter.Session, Is.Null);
            Assert.That(_main.Audio.Playback.CurrentMusicId, Is.Null);
            Assert.That(_main.Audio.Playback.IsSuspended, Is.True);
            Assert.That(broken.Slots[0], Is.EqualTo("preserved broken progress"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NativeMusicActuallyWrapsItsLoopWithoutRestartingSelection()
        {
            yield return WaitMusic(GameAudioCues.StationMusic);
            var source = Source();
            source.timeSamples = source.clip.samples - source.clip.frequency / 2;
            Assert.That(source.timeSamples, Is.GreaterThan(source.clip.samples - source.clip.frequency));
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (source.timeSamples > source.clip.samples / 2 && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(source.timeSamples, Is.LessThan(source.clip.samples / 2));
            Assert.That(source.isPlaying, Is.True);
            Assert.That(_main.Audio.Playback.CurrentMusicId, Is.EqualTo(GameAudioCues.StationMusic));
        }

        [UnityTest]
        public IEnumerator SelectionRetriesAfterInterruptionAndNeverRestartsSameFieldCue()
        {
            var content = Resources.FindObjectsOfTypeAll<GameContent>().Single(item => item.name == "GameContent");
            var output = new ProbeOutput();
            using var audio = new AudioService(output, content.MusicCatalog.CreateCues(),
                AudioPreferences.Default, () => 1);
            using var music = new MainMusicSession(audio, content.MusicCatalog);
            audio.SetSuspended(AudioPauseReason.ExternalInterruption, true);
            music.Refresh(RescueStage.Map, false);
            Assert.That(music.LastPlayResult, Is.EqualTo(AudioPlayResult.Suspended));
            Assert.That(output.Starts, Is.Zero);
            music.Refresh(RescueStage.Approach, true);
            audio.SetSuspended(AudioPauseReason.ExternalInterruption, false);
            Assert.That(audio.IsSuspended, Is.True);
            music.Refresh(RescueStage.Approach, false);
            music.Refresh(RescueStage.Negotiation, false);
            music.Refresh(RescueStage.Rescued, false);
            Assert.That(output.Starts, Is.EqualTo(1));
            Assert.That(audio.CurrentMusicId, Is.EqualTo(GameAudioCues.RescueMusic));
            music.Refresh(RescueStage.Care, false);
            Assert.That(audio.CurrentMusicId, Is.EqualTo(GameAudioCues.StationMusic));
            Assert.That(output.Starts, Is.EqualTo(2));
            music.Dispose();
            music.Refresh(RescueStage.Map, false);
            Assert.That(audio.CurrentMusicId, Is.Null);
            Assert.That(output.Starts, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnloadedStreamIsPreparedOnDemandWithoutLosingTheDesiredMusic()
        {
            var content = Resources.FindObjectsOfTypeAll<GameContent>().Single(item => item.name == "GameContent");
            var cues = content.MusicCatalog.CreateCues();
            var clip = cues.Single(cue => cue.Id == GameAudioCues.ExploreMusic).Clip;
            using var audio = new AudioService(new ProbeOutput(), cues, AudioPreferences.Default, () => 1);
            using var music = new MainMusicSession(audio, content.MusicCatalog);
            Assert.That(clip.UnloadAudioData(), Is.True);
            music.Refresh(RescueStage.Map, false);
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            music.Refresh(RescueStage.Map, false);
            Assert.That(audio.CurrentMusicId, Is.EqualTo(GameAudioCues.ExploreMusic));
        }

        private sealed class ProbeOutput : IAudioOutput
        {
            public int Starts;
            public int EffectVoiceCount => 1;
            public bool IsMusicPlaying { get; private set; }
            public bool IsEffectPlaying(int voice) => false;
            public void PlayMusic(AudioClip clip, bool loop, float gain) { Starts++; IsMusicPlaying = true; }
            public void SetMusicGain(float gain) { }
            public void SetMusicPaused(bool paused) { }
            public void StopMusic() => IsMusicPlaying = false;
            public void PlayEffect(int voice, AudioClip clip, float gain) { }
            public void SetEffectGain(int voice, float gain) { }
            public void StopEffect(int voice) { }
            public void Dispose() { }
        }
    }
}
