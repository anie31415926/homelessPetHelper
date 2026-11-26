using System.Collections;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Loading;
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
        private MemorySaveStorage _history;
        private int _mainLoads;
        private bool _failNextMainEntry;

        [SetUp]
        public void SetUp()
        {
            for (int slot = 0; slot < _slots.Length; slot++) _slots[slot] = new MemorySaveStorage();
            _history = new MemorySaveStorage();
            _mainLoads = 0;
            _failNextMainEntry = false;
        }

        private void Configure(Scene scene, LoadSceneMode mode)
        {
            if (scene.path == LoadingSceneRoot.ScenePath)
            {
                _slots[0].FailReads = false;
                Object.FindObjectOfType<LoadingSceneRoot>().ConfigureSaveSlots(slot => _slots[slot], () => _history);
                return;
            }
            if (scene.name != "Main") return;
            _mainLoads++;
            _slots[0].FailReads = _failNextMainEntry;
            _failNextMainEntry = false;
            _root = Object.FindObjectOfType<MainGameRoot>();
            _root.ConfigureSaveSlots(slot => _slots[slot]);
            _root.ConfigurePreferenceStorage(new MemorySaveStorage(), new MemorySaveStorage());
        }

        [UnityTest]
        public IEnumerator NewGamesUseEmptySlotsAndManualSaveOnlyWritesSelectedSlot()
        {
            SceneManager.sceneLoaded += Configure;
            try
            {
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                Click("开始新游戏");
                ClickSlot(1);
                yield return WaitForMain();
                Assert.That(_slots[0].Writes, Is.EqualTo(1));
                var clock = _root.Presenter.Session.Clock;
                Assert.That(clock.TryAdvance(clock.Current.Version, 9, out _, out _), Is.True);
                Save();
                yield return WaitForSave();
                Click("小小救助站");
                yield return WaitForCover();
                Click("加载存档");
                var first = Slot(1).GetComponentInChildren<Text>().text;
                Assert.That(first, Does.Contain("累计救助：0").And.Contain("保存时间："));
                Assert.That(first, Does.Not.Contain("未记录"));
                ClickSlot(2);
                yield return WaitForMain();
                Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.Zero);
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                Click("继续游戏");
                yield return WaitForMain();
                Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.Zero);
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                Click("加载存档");
                ClickSlot(1);
                yield return WaitForMain();
                Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(9));
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                Click("继续游戏");
                Assert.That(SlotListOpen(), Is.False);
                yield return WaitForMain();
                Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(9),
                    "The last played slot can have an older save timestamp.");
                Assert.That(_slots.Select(slot => slot.Writes), Is.EqualTo(new[] { 2, 1, 0 }));
            }
            finally { SceneManager.sceneLoaded -= Configure; }
        }

        [UnityTest]
        public IEnumerator FailedManualWriteKeepsOriginalAndReturnsToSlotSelection()
        {
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

        [UnityTest]
        public IEnumerator ContinueFailureReturnsToCoverOnceWithoutOpeningTheList()
        {
            const string failed = "无法打开所选存档，请重试或选择其他档位。";
            SceneManager.sceneLoaded += Configure;
            try
            {
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                Click("进入救助站");
                yield return WaitForMain();
                var original = _slots[0].Slots.ToArray();
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                _failNextMainEntry = true;
                Click("继续游戏");
                float deadline = Time.realtimeSinceStartup + 25f;
                while (_mainLoads < 2 || !HasText(failed))
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                    yield return null;
                }
                Assert.That(SlotListOpen(), Is.False);
                Click("继续游戏");
                yield return null;
                Assert.That(HasText(failed), Is.True);
                Assert.That(SlotListOpen(), Is.False);
                Assert.That(_mainLoads, Is.EqualTo(2));
                Assert.That(_slots[0].Slots, Is.EqualTo(original));
                Click("加载存档");
                Assert.That(SlotListOpen(), Is.True);
                ClickSlot(1);
                yield return WaitForMain();
            }
            finally { SceneManager.sceneLoaded -= Configure; }
        }

        [UnityTest]
        public IEnumerator ConfirmedDeletionOfLastPlayedSlotMakesContinueUseTheRemainingSave()
        {
            SceneManager.sceneLoaded += Configure;
            try
            {
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                Click("进入救助站");
                yield return WaitForMain();
                var clock = _root.Presenter.Session.Clock;
                Assert.That(clock.TryAdvance(clock.Current.Version, 9, out _, out _), Is.True);
                Save();
                yield return WaitForSave();
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                Click("开始新游戏");
                Assert.That(DeleteButtonVisible(1), Is.False);
                ClickSlot(2);
                yield return WaitForMain();
                Save();
                yield return WaitForSave();
                Assert.That(_slots[1].Slots, Has.None.Null);
                var kept = _slots[0].Slots.ToArray();
                var target = _slots[1].Slots.ToArray();
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                Click("加载存档");
                ClickDelete(2);
                Assert.That(HasText("确定删除存档 2？\n救助进度将无法恢复。"), Is.True);
                Assert.That(SlotListOpen(), Is.False);
                Click("取消");
                Assert.That(_slots[1].Slots, Is.EqualTo(target));
                Assert.That(SlotListOpen(), Is.True);
                ClickDelete(2);
                Click("确认删除");
                Assert.That(HasText("存档已删除。"), Is.True);
                Assert.That(_slots[1].Slots, Is.All.Null);
                Assert.That(_slots[0].Slots, Is.EqualTo(kept));
                Assert.That(DeleteButtonVisible(2), Is.False);
                Assert.That(Slot(2).interactable, Is.True);
                Assert.That(Slot(2).GetComponentInChildren<Text>().text, Does.Contain("空位"));
                Click("返回");
                Click("继续游戏");
                yield return WaitForMain();
                Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(9));
            }
            finally { SceneManager.sceneLoaded -= Configure; }
        }

        [UnityTest]
        public IEnumerator ChangedOrUnreadableCheckpointIsNotDeletedAndCanBeRetried()
        {
            SceneManager.sceneLoaded += Configure;
            try
            {
                _slots[1].Slots[0] = "damaged but readable";
                _slots[2].FailReads = true;
                yield return SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                yield return null;
                Click("加载存档");
                Assert.That(DeleteButtonVisible(3), Is.False);
                Assert.That(Slot(2).interactable, Is.False);
                ClickDelete(2);
                _slots[1].Slots[0] = "changed after opening confirmation";
                Click("确认删除");
                Assert.That(HasText("未能确认删除，请检查后重试。"), Is.True);
                Assert.That(_slots[1].Slots[0], Is.EqualTo("changed after opening confirmation"));
                ClickDelete(2);
                _slots[1].FailingDeleteSlot = 0;
                Click("确认删除");
                Assert.That(HasText("未能确认删除，请检查后重试。"), Is.True);
                Assert.That(_slots[1].Slots[0], Is.Not.Null);
                _slots[1].FailingDeleteSlot = null;
                ClickDelete(2);
                Click("确认删除");
                Assert.That(HasText("存档已删除。"), Is.True);
                Assert.That(_slots[1].Slots, Is.All.Null);
            }
            finally { SceneManager.sceneLoaded -= Configure; }
        }

        private static bool DeleteButtonVisible(int slot) => Object.FindObjectsOfType<Button>()
            .Any(button => button.name == "DeleteSlot" + slot);

        private static void ClickDelete(int slot) => Object.FindObjectsOfType<Button>()
            .Single(button => button.name == "DeleteSlot" + slot && button.interactable).onClick.Invoke();

        private static bool SlotListOpen() => Object.FindObjectsOfType<Button>().Any(button => button.name == "SaveSlot1");

        private static Button Slot(int slot) => Object.FindObjectsOfType<Button>().Single(button =>
            button.name == "SaveSlot" + slot);
        private static void ClickSlot(int slot)
        {
            var button = Slot(slot);
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke();
        }

        private IEnumerator WaitForMain()
        {
            float deadline = Time.realtimeSinceStartup + 25f;
            while (SceneManager.GetActiveScene().path != LoadingSceneRoot.MainScenePath || _root.Presenter == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
        }

        private static IEnumerator WaitForCover()
        {
            float deadline = Time.realtimeSinceStartup + 25f;
            while (!HasText("继续游戏"))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
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
