using System.Collections;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Modules.Rescue;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class BootstrapTests
    {
        private GameBootstrap _bootstrap;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            GameBootstrap[] bootstraps = Object.FindObjectsOfType<GameBootstrap>();
            Assert.That(bootstraps.Length, Is.EqualTo(1));
            _bootstrap = bootstraps[0];
            _bootstrap.SendMessage("OnApplicationFocus", true);
        }

        [UnityTest]
        public IEnumerator MainSceneInitializesStationWithoutUi()
        {
            Assert.That(_bootstrap.Flow, Is.Not.Null);
            Assert.That(_bootstrap.Flow.Stage, Is.EqualTo(RescueStage.Station));
            Assert.That(Object.FindObjectsOfType<Canvas>(), Is.Empty);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Camera.main.orthographic, Is.True);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FocusAndPauseMustBothRecoverBeforeResuming()
        {
            Assert.That(_bootstrap.Flow.TryAdvance(RescueSignal.LeaveStation, out _), Is.True);
            _bootstrap.SendMessage("OnApplicationFocus", false);
            _bootstrap.SendMessage("OnApplicationPause", true);
            _bootstrap.SendMessage("OnApplicationFocus", true);
            Assert.That(_bootstrap.Flow.IsSuspended, Is.True);
            Assert.That(_bootstrap.Flow.TryAdvance(RescueSignal.ApproachCompleted, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(TransitionFailure.Suspended));
            _bootstrap.SendMessage("OnApplicationPause", false);
            Assert.That(_bootstrap.Flow.IsSuspended, Is.False);
            Assert.That(_bootstrap.Flow.Stage, Is.EqualTo(RescueStage.Approach));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReloadCreatesFreshSessionWithoutStaticResidue()
        {
            RescueFlow previous = _bootstrap.Flow;
            Assert.That(previous.TryAdvance(RescueSignal.LeaveStation, out _), Is.True);
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            Assert.That(_bootstrap == null, Is.True);
            GameBootstrap[] bootstraps = Object.FindObjectsOfType<GameBootstrap>();
            Assert.That(bootstraps.Length, Is.EqualTo(1));
            Assert.That(bootstraps[0].Flow, Is.Not.SameAs(previous));
            Assert.That(bootstraps[0].Flow.Stage, Is.EqualTo(RescueStage.Station));
        }
    }
}
