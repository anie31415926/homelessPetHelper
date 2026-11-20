using System.Collections;
using System.Linq;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.Audio.PlayMode
{
    public sealed class ButtonAudioTests
    {
        private GameObject _root;
        private AudioClip _clip;
        private AudioScope _scope;
        private AudioService _audio;
        private ProbeOutput _output;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            _root = new GameObject("ButtonAudioTests", typeof(AudioScope));
            _scope = _root.GetComponent<AudioScope>();
            _clip = AudioClip.Create("TestClipOnly", 44100, 1, 44100, false);
            _output = new ProbeOutput();
            _audio = CreateAudio(_output);
            _scope.Bind(_audio);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _audio.Dispose();
            Object.Destroy(_root);
            Object.Destroy(_clip);
            yield return null;
        }

        private AudioService CreateAudio(ProbeOutput output) => new AudioService(output, new[]
        {
            new AudioCue(GameAudioCues.Tap, AudioChannel.Effects, _clip,
                new AudioCuePolicy(minIntervalSeconds: 0)),
            new AudioCue(GameAudioCues.Notice, AudioChannel.Effects, _clip,
                new AudioCuePolicy(minIntervalSeconds: 0))
        }, AudioPreferences.Default, () => 1);

        private Button CreateButton(bool attachAudio = true)
        {
            var node = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            node.transform.SetParent(_root.transform, false);
            if (attachAudio) node.AddComponent<ButtonAudio>();
            return node.GetComponent<Button>();
        }

        private static void Click(Button button, PointerEventData.InputButton input = PointerEventData.InputButton.Left)
        {
            button.OnPointerClick(new PointerEventData(EventSystem.current) { button = input });
        }

        [UnityTest]
        public IEnumerator OnlyAcceptedPointerAndSubmitEventsPlayAndReenableDoesNotDuplicate()
        {
            Button button = CreateButton();
            Click(button, PointerEventData.InputButton.Right);
            button.interactable = false;
            Click(button);
            button.interactable = true;
            var group = _root.AddComponent<CanvasGroup>();
            group.interactable = false;
            Click(button);
            Assert.That(_output.Starts, Is.Zero);
            group.interactable = true;
            Click(button);
            Assert.That(_output.Starts, Is.EqualTo(1));
            button.OnSubmit(new BaseEventData(EventSystem.current));
            Assert.That(_output.Starts, Is.EqualTo(2));
            for (int i = 0; i < 3; i++)
            {
                button.gameObject.SetActive(false);
                button.gameObject.SetActive(true);
            }
            Click(button);
            Assert.That(_output.Starts, Is.EqualTo(3));
            button.GetComponent<ButtonAudio>().enabled = false;
            Click(button);
            Assert.That(_output.Starts, Is.EqualTo(3), "Do not keep a disabled component's callback.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BusinessListenerCanClosePanelBeforeFeedbackWithoutLosingAcceptedClick()
        {
            Button button = CreateButton(false);
            button.onClick.AddListener(() => _root.SetActive(false));
            button.gameObject.AddComponent<ButtonAudio>();
            Click(button);
            Assert.That(_root.activeSelf, Is.False);
            Assert.That(_output.Starts, Is.EqualTo(1));
            button.onClick.Invoke();
            Assert.That(_output.Starts, Is.EqualTo(1), "Disabled view must have removed its listener.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverridesSilenceMuteSuspensionAndUnbindingUseSamePlayer()
        {
            Button button = CreateButton();
            var feedback = button.GetComponent<ButtonAudio>();
            feedback.Silent = true;
            Click(button);
            feedback.Silent = false;
            feedback.CueId = "missing";
            Click(button);
            Assert.That(_output.Starts, Is.Zero);
            feedback.CueId = GameAudioCues.Notice;
            _audio.ApplyPreferences(new AudioPreferences(new AudioBusSettings(1), new AudioBusSettings(1),
                new AudioBusSettings(1, true)));
            Click(button);
            _audio.ApplyPreferences(AudioPreferences.Default);
            _audio.SetSuspended(AudioPauseReason.ExternalInterruption, true);
            Click(button);
            _audio.SetSuspended(AudioPauseReason.ExternalInterruption, false);
            Assert.That(_output.Starts, Is.Zero);
            Click(button);
            Assert.That(_output.Starts, Is.EqualTo(1));
            _scope.Unbind();
            Click(button);
            Assert.That(_scope.TryPlayEffect(GameAudioCues.Tap), Is.EqualTo(AudioPlayResult.NotBound));
            Assert.That(_output.Starts, Is.EqualTo(1));
            _scope.Bind(_audio);
            Click(button);
            Assert.That(_output.Starts, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClonedAndReparentedPooledButtonUsesNewLocalScope()
        {
            Button button = CreateButton();
            var second = new GameObject("SecondScope", typeof(AudioScope));
            var otherOutput = new ProbeOutput();
            var other = CreateAudio(otherOutput);
            try
            {
                second.GetComponent<AudioScope>().Bind(other);
                Button clone = Object.Instantiate(button, _root.transform);
                Click(clone);
                Assert.That(_output.Starts, Is.EqualTo(1));
                clone.gameObject.SetActive(false);
                clone.transform.SetParent(second.transform, false);
                clone.gameObject.SetActive(true);
                Click(clone);
                Assert.That(_output.Starts, Is.EqualTo(1));
                Assert.That(otherOutput.Starts, Is.EqualTo(1));
                Object.Destroy(clone.gameObject);
            }
            finally
            {
                other.Dispose();
                Object.Destroy(second);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainHasStaticScopesRealClipsAndButtonFeedbackWithoutAdditionalSources()
        {
            var main = Object.FindObjectOfType<MainGameRoot>();
            var scopes = Object.FindObjectsOfType<AudioScope>(true).Where(scope => scope != _scope).ToArray();
            Assert.That(scopes.Length, Is.EqualTo(3));
            Assert.That(scopes.All(scope => scope.IsBound), Is.True);
            var host = Object.FindObjectOfType<AudioHost>();
            host.SendMessage("OnApplicationFocus", true);
            var feedback = scopes.SelectMany(scope => scope.GetComponentsInChildren<ButtonAudio>(true))
                .First(item => item.isActiveAndEnabled && item.GetComponent<Button>().IsInteractable());
            Click(feedback.GetComponent<Button>());
            Assert.That(Object.FindObjectsOfType<AudioSource>().Length, Is.EqualTo(9));
            Assert.That(Object.FindObjectsOfType<AudioSource>().Any(source =>
                source.clip != null && source.clip.name == "SFX_UI_Tap"), Is.True);
            foreach (string cue in new[] { GameAudioCues.Merge, GameAudioCues.RescueSuccess,
                GameAudioCues.Reward, GameAudioCues.Notice, GameAudioCues.Open })
            {
                main.Audio.Playback.StopEffects();
                Assert.That(main.Audio.Playback.PlayEffect(cue), Is.EqualTo(AudioPlayResult.Played));
            }
            var previous = main.Audio.Playback;
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null; // Main's explicitly owned audio session is created by Start.
            Assert.That(previous.IsDisposed, Is.True);
            Assert.That(Object.FindObjectsOfType<AudioSource>().Length, Is.EqualTo(9));
            LogAssert.NoUnexpectedReceived();
        }

        private sealed class ProbeOutput : IAudioOutput
        {
            public int Starts;
            public int EffectVoiceCount => 2;
            public bool IsMusicPlaying => false;
            public bool IsEffectPlaying(int voice) => false;
            public void PlayEffect(int voice, AudioClip clip, float gain) => Starts++;
            public void PlayMusic(AudioClip clip, bool loop, float gain) { }
            public void SetMusicGain(float gain) { }
            public void SetMusicPaused(bool paused) { }
            public void SetEffectGain(int voice, float gain) { }
            public void StopMusic() { }
            public void StopEffect(int voice) { }
            public void Dispose() { }
        }
    }
}
