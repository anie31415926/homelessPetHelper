using System;
using HomelessPetHelper.Framework.Audio;
using NUnit.Framework;
using UnityEngine;

namespace HomelessPetHelper.Tests.Audio.EditMode
{
    public sealed class AudioServiceTests
    {
        private AudioClip _clip;
        private AudioOutputStub _output;
        private AudioService _service;
        private double _time;

        [SetUp]
        public void SetUp()
        {
            _clip = AudioClip.Create("AudioUnitTestOnly", 44100, 1, 44100, false);
            _output = new AudioOutputStub();
            _time = 0;
            _service = Create(new AudioCue("music", AudioChannel.Music, _clip, new AudioCuePolicy(loop: true)),
                new AudioCue("effect", AudioChannel.Effects, _clip, new AudioCuePolicy(0.5f, 2, 0.1)),
                new AudioCue("other", AudioChannel.Effects, _clip, new AudioCuePolicy(1, 1, 0)),
                new AudioCue("missing", AudioChannel.Effects, null));
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            UnityEngine.Object.DestroyImmediate(_clip);
        }

        private AudioService Create(params AudioCue[] cues) =>
            new AudioService(_output, cues, AudioPreferences.Default, () => _time);

        [Test]
        public void IdLookupDistinguishesUnknownWrongChannelAndMissingClip()
        {
            Assert.That(_service.PlayEffect("unknown"), Is.EqualTo(AudioPlayResult.UnknownCue));
            Assert.That(_service.PlayEffect(null), Is.EqualTo(AudioPlayResult.UnknownCue));
            Assert.That(_service.PlayEffect("music"), Is.EqualTo(AudioPlayResult.WrongChannel));
            Assert.That(_service.PlayMusic("effect"), Is.EqualTo(AudioPlayResult.WrongChannel));
            Assert.That(_service.PlayEffect("missing"), Is.EqualTo(AudioPlayResult.MissingClip));
            Assert.That(_output.EffectStarts + _output.MusicStarts, Is.Zero);
        }

        [Test]
        public void PlayingTheCurrentMusicDoesNotRestartButCompletedMusicCanReplay()
        {
            Assert.That(_service.PlayMusic("music"), Is.EqualTo(AudioPlayResult.Played));
            Assert.That(_service.PlayMusic("music"), Is.EqualTo(AudioPlayResult.AlreadyPlaying));
            Assert.That(_output.MusicStarts, Is.EqualTo(1));
            _output.MusicPlaying = false;
            Assert.That(_service.PlayMusic("music"), Is.EqualTo(AudioPlayResult.Played));
            Assert.That(_output.MusicStarts, Is.EqualTo(2));
        }

        [Test]
        public void UnknownTrackDoesNotStopTheCurrentMusic()
        {
            _service.PlayMusic("music");
            Assert.That(_service.PlayMusic("unknown"), Is.EqualTo(AudioPlayResult.UnknownCue));
            Assert.That(_service.CurrentMusicId, Is.EqualTo("music"));
            Assert.That(_output.MusicPlaying, Is.True);
        }

        [Test]
        public void CooldownUsesInjectedRealSecondsAndDoesNotConsumeRejectedRequests()
        {
            Assert.That(_service.PlayEffect("effect"), Is.EqualTo(AudioPlayResult.Played));
            _time = 0.099;
            Assert.That(_service.PlayEffect("effect"), Is.EqualTo(AudioPlayResult.Cooldown));
            _time = 0.1;
            Assert.That(_service.PlayEffect("effect"), Is.EqualTo(AudioPlayResult.Played));
            Assert.That(_output.EffectStarts, Is.EqualTo(2));
        }

        [Test]
        public void GlobalPoolIsBoundedAndCompletedVoiceCanBeReused()
        {
            _service.PlayEffect("effect");
            Assert.That(_service.PlayEffect("other"), Is.EqualTo(AudioPlayResult.Played));
            _time = 1;
            Assert.That(_service.PlayEffect("effect"), Is.EqualTo(AudioPlayResult.VoiceLimit));
            _output.Effects[0] = false;
            Assert.That(_service.PlayEffect("effect"), Is.EqualTo(AudioPlayResult.Played));
            Assert.That(_output.EffectStarts, Is.EqualTo(3));
            Assert.That(_output.Effects.Length, Is.EqualTo(2));
        }

        [Test]
        public void PerCueVoiceLimitAppliesEvenWithUnusedGlobalSlots()
        {
            Assert.That(_service.PlayEffect("other"), Is.EqualTo(AudioPlayResult.Played));
            Assert.That(_service.PlayEffect("other"), Is.EqualTo(AudioPlayResult.VoiceLimit));
            Assert.That(_output.Effects[1], Is.False);
        }

        [Test]
        public void MutingDropsExistingEffectsAndNeverReplaysThemOnUnmute()
        {
            _service.PlayEffect("effect");
            _service.ApplyPreferences(new AudioPreferences(new AudioBusSettings(1), new AudioBusSettings(1),
                new AudioBusSettings(0.6f, true)));
            Assert.That(_output.Effects, Is.All.False);
            Assert.That(_service.PlayEffect("effect"), Is.EqualTo(AudioPlayResult.Muted));
            _service.ApplyPreferences(AudioPreferences.Default);
            Assert.That(_output.Effects, Is.All.False);
            Assert.That(_output.EffectStarts, Is.EqualTo(1));
        }

        [Test]
        public void MasterChannelAndCueGainMultiplyAndChangesUpdateAlreadyPlayingSources()
        {
            _service.PlayMusic("music");
            _service.PlayEffect("effect");
            _service.ApplyPreferences(new AudioPreferences(new AudioBusSettings(0.5f), new AudioBusSettings(0.4f),
                new AudioBusSettings(0.6f)));
            Assert.That(_output.MusicGain, Is.EqualTo(0.2f).Within(0.00001));
            Assert.That(_output.EffectGains[0], Is.EqualTo(0.15f).Within(0.00001));
            Assert.That(_output.MusicStarts, Is.EqualTo(1));
        }

        [Test]
        public void MutedMusicCanSelectATrackAndUnmuteWithoutRestarting()
        {
            _service.ApplyPreferences(new AudioPreferences(new AudioBusSettings(1), new AudioBusSettings(0.4f, true),
                new AudioBusSettings(1)));
            Assert.That(_service.PlayMusic("music"), Is.EqualTo(AudioPlayResult.Played));
            Assert.That(_output.MusicGain, Is.Zero);
            _service.ApplyPreferences(AudioPreferences.Default);
            Assert.That(_output.MusicGain, Is.EqualTo(1));
            Assert.That(_output.MusicStarts, Is.EqualTo(1));
        }

        [Test]
        public void MultiplePauseReasonsAreIdempotentAndOnlyFinalClearResumesMusic()
        {
            _service.PlayMusic("music");
            _service.PlayEffect("effect");
            _service.SetSuspended(AudioPauseReason.ApplicationPaused, true);
            _service.SetSuspended(AudioPauseReason.ApplicationPaused, true);
            _service.SetSuspended(AudioPauseReason.FocusLost, true);
            Assert.That(_output.MusicPauseChanges, Is.EqualTo(1));
            Assert.That(_output.Effects, Is.All.False);
            Assert.That(_service.PlayEffect("other"), Is.EqualTo(AudioPlayResult.Suspended));
            _service.SetSuspended(AudioPauseReason.ApplicationPaused, false);
            Assert.That(_service.IsSuspended, Is.True);
            Assert.That(_output.MusicPlaying, Is.False);
            _service.SetSuspended(AudioPauseReason.FocusLost, false);
            Assert.That(_output.MusicPlaying, Is.True);
            Assert.That(_output.MusicStarts, Is.EqualTo(1));
            Assert.That(_output.MusicPauseChanges, Is.EqualTo(2));
            Assert.That(_output.Effects, Is.All.False);
        }

        [Test]
        public void StopWhileSuspendedPreventsMusicResurrection()
        {
            _service.PlayMusic("music");
            _service.SetSuspended(AudioPauseReason.GameplayPaused, true);
            Assert.That(_service.PlayMusic("music"), Is.EqualTo(AudioPlayResult.Suspended));
            _service.StopMusic();
            _service.SetSuspended(AudioPauseReason.GameplayPaused, false);
            Assert.That(_service.CurrentMusicId, Is.Null);
            Assert.That(_output.MusicPlaying, Is.False);
        }

        [Test]
        public void DisposalStopsSourcesReleasesOutputOnceAndRejectsFurtherPlayback()
        {
            _service.PlayMusic("music");
            _service.PlayEffect("effect");
            _service.Dispose();
            _service.Dispose();
            Assert.That(_output.DisposalCount, Is.EqualTo(1));
            Assert.That(_output.MusicPlaying, Is.False);
            Assert.That(_output.Effects, Is.All.False);
            Assert.That(_service.PlayEffect("effect"), Is.EqualTo(AudioPlayResult.Disposed));
            Assert.That(_service.PlayMusic("music"), Is.EqualTo(AudioPlayResult.Disposed));
            Assert.Throws<ObjectDisposedException>(() => _service.ApplyPreferences(AudioPreferences.Default));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        public void InvalidVolumeIsRejectedInsteadOfClamped(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AudioBusSettings(value));
        }

        [Test]
        public void InvalidCuePolicyChannelAndDuplicateIdsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new AudioCue(" ", AudioChannel.Effects, _clip));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AudioCue("bad", (AudioChannel)8, _clip));
            Assert.Throws<ArgumentException>(() => new AudioCue("bad", AudioChannel.Effects, _clip,
                new AudioCuePolicy(loop: true)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AudioCuePolicy(maxVoices: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AudioCuePolicy(minIntervalSeconds: double.NaN));
            var cue = new AudioCue("same", AudioChannel.Effects, _clip);
            Assert.Throws<ArgumentException>(() => Create(cue, cue));
        }

        [TestCase(AudioPauseReason.None)]
        [TestCase((AudioPauseReason)256)]
        public void InvalidSuspensionReasonDoesNotChangeState(AudioPauseReason reason)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _service.SetSuspended(reason, true));
            Assert.That(_service.IsSuspended, Is.False);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void InvalidInjectedTimeCannotStartAnEffect(double time)
        {
            _time = time;
            Assert.Throws<InvalidOperationException>(() => _service.PlayEffect("effect"));
            Assert.That(_output.EffectStarts, Is.Zero);
        }

        [Test]
        public void EmptyCatalogIsValidAndMissingContentDoesNotCrash()
        {
            var catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            try { Assert.That(catalog.CreateCues(), Is.Empty); }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }
    }
}
