using System;
using System.Reflection;
using HomelessPetHelper.Game.CityMap.Travel;
using NUnit.Framework;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class MapTravelTests
    {
        [Test]
        public void CompositePartsMoveIndependentlyAndRestoreOriginalPose()
        {
            var root = new GameObject("Visual", typeof(RectTransform), typeof(MapPointVisual));
            try
            {
                var cart = new GameObject("Cart", typeof(RectTransform), typeof(CanvasGroup));
                cart.transform.SetParent(root.transform, false);
                var rect = (RectTransform)cart.transform;
                rect.anchoredPosition = new Vector2(20, 30);
                var visual = root.GetComponent<MapPointVisual>();
                var partType = typeof(MapPointVisual).GetNestedType("Part", BindingFlags.NonPublic);
                var part = Activator.CreateInstance(partType);
                partType.GetField("target").SetValue(part, rect);
                partType.GetField("offset").SetValue(part, new Vector2(100, 0));
                partType.GetField("opacity").SetValue(part, cart.GetComponent<CanvasGroup>());
                partType.GetField("endOpacity").SetValue(part, 0f);
                var parts = Array.CreateInstance(partType, 1); parts.SetValue(part, 0);
                Set(visual, "_enter", parts);
                visual.Begin(MapTravelDirection.Enter);
                visual.Sample(1);
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(120, 30)));
                Assert.That(cart.GetComponent<CanvasGroup>().alpha, Is.Zero);
                visual.Restore();
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(20, 30)));
                Assert.That(cart.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                visual.Restore();
                Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidDurationIsRejectedBeforePlayback(float seconds)
        {
            var step = new MapTravelStep(); Set(step, "_durationSeconds", seconds);
            var method = typeof(MapTravelStep).GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic);
            var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(step, null));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
