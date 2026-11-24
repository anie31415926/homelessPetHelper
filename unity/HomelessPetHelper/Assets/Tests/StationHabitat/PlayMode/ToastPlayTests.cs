#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.StationHabitat.PlayMode
{
    public sealed class ToastPlayTests
    {
        private GameObject _root;
        private ToastView _toast;
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Toast fixture", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(1080, 1920);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Widgets/Widget_Toast.prefab");
            _toast = Object.Instantiate(prefab, _root.transform).GetComponent<ToastView>();
            _toast.BindBounds((RectTransform)_root.transform);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [UnityTest]
        public IEnumerator NoticeFadesInAndOutWithoutBlockingAndRepeatedTextRestarts()
        {
            _toast.Show("护理完成", .1f);
            Assert.That(_toast.Opacity, Is.Zero);
            yield return null;
            Assert.That(_toast.Opacity, Is.InRange(0.001f, .999f));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(_toast.Opacity, Is.EqualTo(1));
            Assert.That(_toast.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(_toast.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(_toast.Opacity, Is.InRange(0.001f, .999f));
            _toast.Show("护理完成", .1f);
            Assert.That(_toast.Opacity, Is.Zero);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(_toast.IsShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator LongTextPagesWithoutTruncationAndNarrowResizeKeepsBodySize()
        {
            string message = "进度保存失败，已停止操作。请退出后重新读取记录。";
            ((RectTransform)_root.transform).sizeDelta = new Vector2(600, 1000);
            _toast.Show(message, .1f);
            string seen = "", last = "";
            for (int frame = 0; frame < 500 && _toast.IsShowing; frame++)
            {
                if (_toast.CurrentLine != last)
                { last = _toast.CurrentLine; seen += last; }
                var label = _toast.GetComponentInChildren<Text>();
                Assert.That(label.fontSize, Is.EqualTo(48));
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1));
                yield return null;
            }
            Assert.That(seen, Is.EqualTo(message));
            _toast.Show("完成"); _toast.Show("更新后的提醒");
            Assert.That(_toast.CurrentLine, Is.EqualTo("更新后的提醒"));
            _toast.SendMessage("OnApplicationFocus", false);
            Assert.That(_toast.IsShowing, Is.False);
            _toast.Show("完成"); _toast.gameObject.SetActive(false); _toast.gameObject.SetActive(true);
            Assert.That(_toast.IsShowing, Is.False);
        }

        [Test]
        public void SlightOverflowKeepsOneLineBySteppingDownTypeRoles()
        {
            // 22 字在 1080 安全区、正文 48 下溢出；缩到次级/辅助字号后必须整句单行，不留下只有标点的第二页。
            string message = "进度记录已变化，本次结算未通过，进度已保留。";
            _toast.Show(message, 1f);
            var label = _toast.GetComponentInChildren<Text>();
            Assert.That(_toast.CurrentLine, Is.EqualTo(message));
            Assert.That(label.fontSize, Is.LessThan(48));
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(36));
            Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1));
        }

        [UnityTest]
        public IEnumerator PagedTextSplitsEvenlyInsteadOfTrailingPunctuationOnly()
        {
            ((RectTransform)_root.transform).sizeDelta = new Vector2(600, 1000);
            string message = "进度保存失败，已停止操作。请退出后重新读取记录。";
            _toast.Show(message, .1f);
            var pages = new List<string>();
            string last = "";
            for (int frame = 0; frame < 500 && _toast.IsShowing; frame++)
            {
                if (_toast.CurrentLine != last) { last = _toast.CurrentLine; pages.Add(last); }
                yield return null;
            }
            Assert.That(string.Concat(pages), Is.EqualTo(message));
            Assert.That(pages.Count, Is.GreaterThan(1));
            Assert.That(pages.Min(page => page.Length) * 10,
                Is.GreaterThanOrEqualTo(pages.Max(page => page.Length) * 6));
        }
    }
}
#endif
