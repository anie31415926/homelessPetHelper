#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class CityMapPanPlayTests
    {
        private MainSceneStorageScope _storage;
        private CityMapView _view;
        private CityMapScrollRect _map;
        [SetUp] public void SetUp() => _storage = new MainSceneStorageScope();
        [TearDown] public void TearDown() => _storage.Dispose();

        [UnityTest]
        public IEnumerator MapCoversViewportPansVerticallyAndClampsEveryEdgeLeavingDetailsAndActionsFixed()
        {
            yield return Load();
            Assert.That(_map.content.rect.width, Is.GreaterThanOrEqualTo(_map.viewport.rect.width - .5f),
                "Map art must cover the viewport width instead of being upscaled past the screen.");
            Assert.That(_map.content.rect.height, Is.GreaterThanOrEqualTo(_map.viewport.rect.height - .5f));
            var details = _view.GetComponentsInChildren<ScrollRect>(true).Single(scroll => scroll != _map);
            Vector2 detailPosition = details.content.anchoredPosition;
            Vector3 actionPosition = Button("Primary").transform.position;
            _map.normalizedPosition = new Vector2(.5f, .5f);
            Vector2 before = _map.content.anchoredPosition;
            var pointer = Pointer(1);
            _map.OnBeginDrag(pointer);
            pointer.position += new Vector2(70, 90);
            _map.OnDrag(pointer);
            Assert.That(_map.content.anchoredPosition, Is.Not.EqualTo(before),
                "The fullscreen map pans on whichever axis exceeds the viewport.");
            foreach (Vector2 move in new[] { new Vector2(-100000, -100000), new Vector2(100000, -100000),
                new Vector2(100000, 100000), new Vector2(-100000, 100000) })
            {
                pointer.position = move;
                _map.OnDrag(pointer);
                AssertCovered();
                Assert.That(_map.normalizedPosition.x, Is.InRange(-.001f, 1.001f));
                Assert.That(_map.normalizedPosition.y, Is.InRange(-.001f, 1.001f));
            }
            _map.OnEndDrag(pointer);
            _map.StopMovement();
            Assert.That(details.content.anchoredPosition, Is.EqualTo(detailPosition));
            Assert.That(Button("Primary").transform.position, Is.EqualTo(actionPosition));
            // Let ScrollRect settle its own edge clamp before isolating the details-scroll input.
            yield return null;
            Vector2 mapPosition = _map.content.anchoredPosition;
            details.verticalNormalizedPosition = 0;
            yield return null;
            Assert.That(_map.content.anchoredPosition, Is.EqualTo(mapPosition));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DragStartingOnLandmarkCancelsClickWhileTapStillSelects()
        {
            yield return Load();
            Button yard = Button("Yard");
            _map.FocusOn((RectTransform)yard.transform);
            yield return null;
            var pointer = Pointer(2, (RectTransform)yard.transform);
            var hits = new List<RaycastResult>();
            _view.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(yard));
            pointer.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject);
            Assert.That(pointer.pointerDrag, Is.SameAs(_map.gameObject));
            pointer.pointerPress = yard.gameObject;
            pointer.rawPointerPress = yard.gameObject;
            pointer.eligibleForClick = true;
            ExecuteEvents.Execute(yard.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.initializePotentialDrag);
            string heading = Heading();
            Vector2 before = _map.content.anchoredPosition;
            // Exercise uGUI's real threshold/click cancellation path, not a direct button callback.
            pointer.position += Vector2.one; pointer.delta = Vector2.one;
            ProcessDrag(pointer);
            Assert.That(pointer.eligibleForClick, Is.True);
            Assert.That(pointer.dragging, Is.False);
            pointer.position += new Vector2(50, 60); pointer.delta = new Vector2(50, 60);
            ProcessDrag(pointer);
            pointer.position += new Vector2(50, 60);
            ProcessDrag(pointer);
            Assert.That(pointer.dragging, Is.True);
            Assert.That(pointer.eligibleForClick, Is.False);
            Assert.That(pointer.pointerPress, Is.Null);
            Assert.That(_map.content.anchoredPosition, Is.Not.EqualTo(before));
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.endDragHandler);
            Assert.That(Heading(), Is.EqualTo(heading));
            var tap = Pointer(3, (RectTransform)yard.transform);
            ExecuteEvents.Execute(yard.gameObject, tap, ExecuteEvents.pointerClickHandler);
            Assert.That(Heading(), Is.Not.EqualTo(heading));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SelectionFocusesOnceAndPauseFocusLossAndReopenReleaseGestures()
        {
            yield return Load();
            Button("Yard").onClick.Invoke();
            _view.CloseDetails();
            yield return null;
            Vector2 selected = _map.content.anchoredPosition;
            _map.normalizedPosition = new Vector2(.9f, .9f);
            Vector2 dragged = _map.content.anchoredPosition;
            _view.SendMessage("OnApplicationPause", true);
            Assert.That(_map.enabled, Is.False);
            _view.SendMessage("OnApplicationPause", false);
            yield return null;
            Assert.That(_map.content.anchoredPosition, Is.EqualTo(dragged), "Refresh must not steal the map.");
            var first = Pointer(4); _map.OnBeginDrag(first);
            var second = Pointer(5); _map.OnBeginDrag(second);
            second.position += new Vector2(120, 90); _map.OnDrag(second); _map.OnEndDrag(second);
            Assert.That(_map.content.anchoredPosition, Is.EqualTo(dragged));
            first.position += new Vector2(-40, -40); _map.OnDrag(first);
            Assert.That(_map.content.anchoredPosition, Is.Not.EqualTo(dragged));
            _map.velocity = new Vector2(200, 300);
            _view.SendMessage("OnApplicationFocus", false);
            Assert.That(_map.velocity, Is.EqualTo(Vector2.zero));
            _view.SendMessage("OnApplicationFocus", true);
            Vector2 resumed = _map.content.anchoredPosition;
            first.position += Vector2.one * 200; _map.OnDrag(first);
            Assert.That(_map.content.anchoredPosition, Is.EqualTo(resumed), "Old finger must not resume dragging.");
            _view.SendMessage("OnApplicationPause", true);
            Assert.That(_map.enabled, Is.False);
            _view.SendMessage("OnApplicationPause", false);
            _view.gameObject.SetActive(false); _view.gameObject.SetActive(true);
            yield return null;
            Assert.That((_map.content.anchoredPosition - selected).magnitude, Is.LessThan(1));
            Assert.That(_map.velocity, Is.EqualTo(Vector2.zero));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MainUsesSavedPannableMapAndDetailChangesReturnToTop()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            Object.FindObjectOfType<StationPanelView>().GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Restart").onClick.Invoke();
            Bind(); yield return null;
            var details = _view.GetComponentsInChildren<ScrollRect>(true).Single(scroll => scroll != _map);
            details.verticalNormalizedPosition = 0;
            Button("Yard").onClick.Invoke();
            yield return null;
            Assert.That(details.verticalNormalizedPosition, Is.EqualTo(1).Within(.01f));
            _view.CloseDetails();
            var pointer = Pointer(6); _map.OnBeginDrag(pointer);
            Vector2 before = _map.content.anchoredPosition;
            pointer.position += new Vector2(-50, 70); _map.OnDrag(pointer); _map.OnEndDrag(pointer);
            Assert.That(_map.content.anchoredPosition, Is.Not.EqualTo(before));
            AssertCovered();
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/EditorScenes/SC_CityMap.unity",
                new LoadSceneParameters());
            yield return null; Bind(); yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator WideMapSpriteKeepsAspectAndCoversViewportWithoutTouchingLandmarkAnchors()
        {
            yield return Load();
            Image image = _map.content.GetComponent<Image>();
            Sprite original = image.sprite;
            var texture = new Texture2D(2048, 1024);
            var wide = Sprite.Create(texture, new Rect(0, 0, 2048, 1024), new Vector2(.5f, .5f));
            var landmark = (RectTransform)Button("Yard").transform;
            Vector2 min = landmark.anchorMin, max = landmark.anchorMax;
            try
            {
                image.sprite = wide;
                LayoutRebuilder.ForceRebuildLayoutImmediate(_map.viewport);
                yield return null;
                _map.normalizedPosition = new Vector2(0, 0);
                AssertCovered();
                _map.normalizedPosition = new Vector2(1, 1);
                AssertCovered();
                Assert.That(landmark.anchorMin, Is.EqualTo(min));
                Assert.That(landmark.anchorMax, Is.EqualTo(max));
            }
            finally
            {
                image.sprite = original;
                Object.Destroy(wide); Object.Destroy(texture);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private void Bind()
        {
            _view = Object.FindObjectOfType<CityMapView>();
            _map = _view.GetComponentInChildren<CityMapScrollRect>();
            Assert.That(_map, Is.Not.Null);
        }

        private PointerEventData Pointer(int id, RectTransform target = null)
        {
            var rect = target == null ? _map.viewport : target;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            return new PointerEventData(EventSystem.current) { pointerId = id, position = screen,
                pressPosition = screen, button = PointerEventData.InputButton.Left };
        }

        private static void ProcessDrag(PointerEventData pointer) => typeof(PointerInputModule)
            .GetMethod("ProcessDrag", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(EventSystem.current.currentInputModule, new object[] { pointer });

        private void AssertCovered()
        {
            var corners = new Vector3[4]; _map.content.GetWorldCorners(corners);
            Vector3 min = _map.viewport.InverseTransformPoint(corners[0]);
            Vector3 max = _map.viewport.InverseTransformPoint(corners[2]);
            Assert.That(min.x, Is.LessThanOrEqualTo(_map.viewport.rect.xMin + 1));
            Assert.That(min.y, Is.LessThanOrEqualTo(_map.viewport.rect.yMin + 1));
            Assert.That(max.x, Is.GreaterThanOrEqualTo(_map.viewport.rect.xMax - 1));
            Assert.That(max.y, Is.GreaterThanOrEqualTo(_map.viewport.rect.yMax - 1));
            var image = _map.content.GetComponent<Image>();
            Assert.That(_map.content.rect.width / _map.content.rect.height,
                Is.EqualTo(image.sprite.rect.width / image.sprite.rect.height).Within(.001f));
        }

        private Button Button(string name) => _view.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        private string Heading() => _view.GetComponentsInChildren<Text>(true).Single(t => t.name == "Heading").text;
    }
}
#endif
