using System.Collections;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class SaveMenuPlayTests
    {
        private readonly MemorySaveStorage[] _slots = new MemorySaveStorage[3];
        private MainGameRoot _root;

        private void Configure(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Main") return;
            _root = Object.FindObjectOfType<MainGameRoot>();
            _root.ConfigureSaveSlots(slot => _slots[slot]);
            _root.ConfigurePreferenceStorage(new MemorySaveStorage(), new MemorySaveStorage());
        }

        [UnityTest]
        public IEnumerator NewGamesUseEmptySlotsAndManualSaveOnlyWritesSelectedSlot()
        {
            for (int slot = 0; slot < _slots.Length; slot++) _slots[slot] = new MemorySaveStorage();
            SceneManager.sceneLoaded += Configure;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main");
                yield return null;
                Assert.That(_root.Presenter, Is.Null);
                Click("开始新游戏");
                Assert.That(HasText("存档 1 · 空位"), Is.True);
                Click("选择此档");
                Assert.That(_slots[0].Writes, Is.EqualTo(1));
                var clock = _root.Presenter.Session.Clock;
                Assert.That(clock.TryAdvance(clock.Current.Version, 9, out _, out _), Is.True);
                Save();
                yield return WaitForSave();
                Click("小小救助站");
                Click("开始新游戏");
                Assert.That(HasText("存档 2 · 空位"), Is.True);
                Click("选择此档");
                Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.Zero);
                Save();
                yield return WaitForSave();
                Click("小小救助站");
                Click("加载存档");
                Assert.That(HasText("存档 1 · 有记录"), Is.True);
                Click("选择此档");
                Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(9));
                Assert.That(_slots.Select(slot => slot.Writes), Is.EqualTo(new[] { 2, 2, 0 }));
            }
            finally { SceneManager.sceneLoaded -= Configure; }
        }

        [UnityTest]
        public IEnumerator FailedManualWriteKeepsOriginalAndReturnsToSlotSelection()
        {
            for (int slot = 0; slot < _slots.Length; slot++) _slots[slot] = new MemorySaveStorage();
            SceneManager.sceneLoaded += Configure;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main");
                yield return null;
                Click("开始新游戏");
                Click("选择此档");
                var originals = _slots[0].Slots.ToArray();
                _slots[0].FailWrites = true;
                Save();
                Assert.That(HasText("正在存档…"), Is.True);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (_root.Presenter.Progress.PersistenceError == null && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(_root.Presenter.Progress.PersistenceError, Is.Not.Null);
                Assert.That(HasText("存档完成！"), Is.False);
                Assert.That(_slots[0].Slots, Is.EqualTo(originals));
                Click("返回");
                Click("加载存档");
                Click("选择此档");
                Assert.That(_root.Presenter.Progress.CanContinue, Is.True);
            }
            finally { SceneManager.sceneLoaded -= Configure; }
        }

        private static IEnumerator WaitForSave()
        {
            Assert.That(HasText("正在存档…"), Is.True);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!HasText("存档完成！") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(HasText("存档完成！"), Is.True);
        }

        private static void Save()
        {
            Object.FindObjectOfType<Modules.Station.UI.StationPanelView>().RequestSettings();
            Click("手动存档");
        }

        private static bool HasText(string text) => Object.FindObjectsOfType<Text>().Any(label => label.text == text);

        private static void Click(string text) => Object.FindObjectsOfType<Button>().Single(button =>
            button.interactable && button.GetComponentsInChildren<Text>().Any(label => label.text == text))
            .onClick.Invoke();
    }
}
