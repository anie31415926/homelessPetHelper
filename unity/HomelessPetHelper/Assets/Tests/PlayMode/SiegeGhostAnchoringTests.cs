using HomelessPetHelper.Modules.Siege.UI;
using NUnit.Framework;
using UnityEngine;

namespace HomelessPetHelper.Tests.PlayMode
{
    /// <summary>
    /// 锁住拖牌残影的落点换算。
    /// anchoredPosition 以节点的 anchor 点为原点，而局部坐标以父节点 pivot 为原点；
    /// 直接混用会让残影按「父 pivot → anchor」向量偏移（本 prefab 约半个面板）。
    /// </summary>
    public sealed class SiegeGhostAnchoringTests
    {
        private GameObject _root;
        private RectTransform _container;
        private RectTransform _ghost;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ghost-container", typeof(RectTransform));
            _container = (RectTransform)_root.transform;
            _container.anchorMin = _container.anchorMax = Vector2.zero;
            _container.anchoredPosition = Vector2.zero;
            _container.sizeDelta = new Vector2(1080f, 2400f);
            _container.pivot = new Vector2(.5f, .5f);

            var ghostGo = new GameObject("ghost", typeof(RectTransform));
            _ghost = (RectTransform)ghostGo.transform;
            _ghost.SetParent(_container, false);
            _ghost.sizeDelta = new Vector2(110f, 110f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [TestCase(0f, 0f)]
        [TestCase(.5f, .5f)]
        [TestCase(1f, 1f)]
        public void GhostLocalPositionTracksPointerForAnyCornerAnchor(float anchorX, float anchorY)
        {
            _ghost.anchorMin = _ghost.anchorMax = new Vector2(anchorX, anchorY);
            var pointer = new Vector2(120f, -300f);

            _ghost.anchoredPosition =
                SiegeFieldView.ToAnchoredPosition(pointer, _container.rect, _ghost.anchorMin, 0f);

            Assert.That(_ghost.localPosition.x, Is.EqualTo(pointer.x).Within(.01f), "x 与指针不一致");
            Assert.That(_ghost.localPosition.y, Is.EqualTo(pointer.y).Within(.01f), "y 与指针不一致");
        }

        [Test]
        public void AssigningLocalPointDirectlyDriftsByHalfPanelAtBottomLeftAnchor()
        {
            _ghost.anchorMin = _ghost.anchorMax = Vector2.zero;
            var pointer = new Vector2(120f, -300f);

            // 修复前的写法：把父 pivot 坐标系里的值直接当成 anchoredPosition。
            _ghost.anchoredPosition = pointer;

            var drift = (Vector2)_ghost.localPosition - pointer;
            Assert.That(drift.x, Is.EqualTo(-540f).Within(.01f), "应向左偏半个容器宽");
            Assert.That(drift.y, Is.EqualTo(-1200f).Within(.01f), "应向下偏半个容器高");
        }

        [Test]
        public void LiftRaisesGhostAbovePointer()
        {
            _ghost.anchorMin = _ghost.anchorMax = new Vector2(.5f, .5f);
            var pointer = new Vector2(120f, -300f);

            _ghost.anchoredPosition =
                SiegeFieldView.ToAnchoredPosition(pointer, _container.rect, _ghost.anchorMin, 90f);

            Assert.That(_ghost.localPosition.x, Is.EqualTo(pointer.x).Within(.01f));
            Assert.That(_ghost.localPosition.y, Is.EqualTo(pointer.y + 90f).Within(.01f));
        }
    }
}
