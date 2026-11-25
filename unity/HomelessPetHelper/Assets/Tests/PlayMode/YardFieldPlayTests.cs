#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class YardFieldPlayTests
    {
        private MainSceneStorageScope _storage;
        private int _seed;
        private Camera _inputCamera;
        private RenderTexture _inputTarget;
        [SetUp] public void SetUp()
        { _storage=new MainSceneStorageScope(); SceneManager.sceneLoaded+=Configure; }
        [TearDown] public void TearDown()
        {
            SceneManager.sceneLoaded-=Configure; _storage.Dispose();
            if (_inputCamera != null) Object.DestroyImmediate(_inputCamera.gameObject);
            if (_inputTarget != null) { _inputTarget.Release(); Object.DestroyImmediate(_inputTarget); }
        }
        private void Configure(Scene scene,LoadSceneMode mode)
        {
            if(scene.name=="Main")scene.GetRootGameObjects()
                .SelectMany(root=>root.GetComponentsInChildren<MainGameRoot>(true))
                .Single().ConfigureOutingSeedSource(()=>_seed);
        }
        [UnityTest]
        public IEnumerator CleanupAndPuzzleShareFinishedSceneWithActualRescueInputAndPause()
        {
            foreach(int seed in new[]{0,1})
            {
                _seed=seed; yield return SceneManager.LoadSceneAsync("Main"); yield return null;
                var root=Object.FindObjectOfType<MainGameRoot>();
                var station=Object.FindObjectOfType<StationPanelView>(true);
                var map=Object.FindObjectOfType<CityMapView>(true);
                var approach=Object.FindObjectOfType<ApproachPanelView>(true);
                var siege=Object.FindObjectOfType<SiegeTrialView>(true);
                MainApproachDriver.Click(station,"Restart"); MainApproachDriver.Click(map,"Yard");
                MainApproachDriver.Click(map,"Primary");
                Assert.That(approach.FieldScene.Layout,Is.Not.Null);
                yield return Capture(approach,"opening-"+seed);
                MainApproachDriver.Click(approach,"Pause");
                Assert.That(approach.Session.IsSuspended,Is.True);
                Assert.That(MainApproachDriver.Button(approach,"Resume").gameObject.activeInHierarchy,Is.True);
                if (seed == 1) yield return Capture(approach,"opening-guide");
                MainApproachDriver.Click(approach,"Resume");
                Assert.That(approach.Session.IsSuspended,Is.False);
                MainApproachDriver.Solve(approach);
                var openingSnapshot=approach.FieldScene.Snapshot;
                yield return Capture(approach,"cleared-"+seed);
                var animalMotion=approach.FieldScene.Animal.GetComponent<FieldPetMotion>();
                var atlas=animalMotion.Surface.texture;
                Assert.That(atlas.name, Is.EqualTo("T_Station_ShibaMotion"));
                MainApproachDriver.Click(approach,"Continue"); yield return null;
                var field=siege.GetComponent<OutingFieldView>();
                Assert.That(field.Snapshot.Boxes,Is.EqualTo(openingSnapshot.Boxes));
                Assert.That(field.Snapshot.Remaining,Is.EqualTo(openingSnapshot.Remaining));
                Assert.That(field.Snapshot.Bridge,Is.EqualTo(openingSnapshot.Bridge));
                yield return null;
                Assert.That(field.Animal.GetComponent<FieldPetMotion>().Surface.texture,Is.SameAs(atlas));
                var activity=(SiegeRescueActivity)root.Presenter.Session.Activity;
                var before=activity.Battle.Snapshot;
                if (_inputCamera != null) Object.DestroyImmediate(_inputCamera.gameObject);
                if (_inputTarget != null) { _inputTarget.Release(); Object.DestroyImmediate(_inputTarget); }
                _inputCamera = StrategyUiPlayTests.PortraitInput(siege.GetComponent<Canvas>(), out _inputTarget);
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First().GetComponent<Button>().onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                Assert.That(MainApproachDriver.Button(siege,"Top").gameObject.activeInHierarchy,Is.True);
                yield return Capture(siege,"choose-route-"+seed);
                MainApproachDriver.Click(siege,"Top");
                Assert.That(activity.Battle.Snapshot.Energy,Is.EqualTo(before.Energy));
                Assert.That(activity.Battle.Snapshot.Units,Is.Empty);
                MainApproachDriver.Click(siege,"Clear");
                Assert.That(siege.Scene.Plan.HasAction,Is.False);
                var card=siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First();
                var pointer=new PointerEventData(EventSystem.current) {pointerId=21,
                    button=PointerEventData.InputButton.Left,position=Vector2.zero};
                card.OnBeginDrag(pointer);
                pointer.position=new Vector2(-100,-100); card.OnEndDrag(pointer);
                Assert.That(siege.Scene.Plan.HasAction,Is.False,"Drop outside any portal cancels without spending.");
                Assert.That(activity.Battle.Snapshot.Energy,Is.EqualTo(before.Energy));
                card.OnBeginDrag(pointer); Canvas.ForceUpdateCanvases();
                var portal=(RectTransform)MainApproachDriver.Button(siege,"Mid").transform;
                pointer.position=RectTransformUtility.WorldToScreenPoint(_inputCamera,
                    portal.TransformPoint(portal.rect.center));
                card.OnDrag(pointer);
                MainApproachDriver.Click(siege,"Clear");
                card.OnDrag(pointer); card.OnEndDrag(pointer);
                Assert.That(siege.Scene.Plan.HasAction,Is.False,"Clear revokes the captured drag.");
                Assert.That(siege.Scene.Plan.SelectedCard,Is.Null,"Stale drag cannot select a card again.");
                Assert.That(activity.Battle.Snapshot.Energy,Is.EqualTo(before.Energy));
                Assert.That(activity.Battle.Snapshot.Units,Is.Empty);
                card.OnBeginDrag(pointer); Canvas.ForceUpdateCanvases();
                pointer.position=RectTransformUtility.WorldToScreenPoint(_inputCamera,
                    portal.TransformPoint(portal.rect.center));
                card.OnDrag(pointer); card.OnEndDrag(pointer);
                Assert.That(siege.Scene.Plan.DeployLane,Is.EqualTo("mid"));
                Assert.That(activity.Battle.Snapshot.Units,Is.Empty,"Drag only plans the action.");
                MainApproachDriver.Click(siege,"Submit");
                Assert.That(activity.Battle.Snapshot.Units.Count,Is.EqualTo(1));
                Assert.That(activity.Battle.Snapshot.Units[0].LaneId,Is.EqualTo("mid"));
                MainApproachDriver.Click(siege,"Pause"); int elapsed=activity.Battle.ElapsedMs;
                activity.Battle.Advance(1000); Assert.That(activity.Battle.ElapsedMs,Is.EqualTo(elapsed));
                yield return Capture(siege,"paused-"+seed);
                MainApproachDriver.Click(siege,"Resume");
                siege.SendMessage("OnApplicationFocus",false);
                siege.SendMessage("OnApplicationFocus",true);
                Assert.That(activity.Battle.Paused,Is.True); MainApproachDriver.Click(siege,"Resume");
                yield return Capture(siege,"rescue-"+seed);
                MainApproachDriver.Click(siege,"Pause"); MainApproachDriver.Click(siege,"Retry");
                Assert.That(station.gameObject.activeInHierarchy,Is.True);
                Assert.That(activity.IsClosed,Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }
        private static IEnumerator Capture(Component view,string name)
        {
            var canvas=view.GetComponent<Canvas>();
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            var camera=new GameObject("YardCapture",typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.white;
            camera.cullingMask=1<<canvas.gameObject.layer;
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10;
            string folder=Path.GetFullPath("YardFieldEvidence"); Directory.CreateDirectory(folder);
            var safeView=view.GetComponentInChildren<SafeAreaView>();
            var safeRect=(RectTransform)safeView.transform;
            var sizes=new[]{new Vector2Int(432,960),new Vector2Int(390,844),new Vector2Int(540,1080),
                new Vector2Int(540,960),new Vector2Int(768,1024),new Vector2Int(390,844)};
            for (int index=0;index<sizes.Length;index++)
            {
                var size=sizes[index]; bool cutout=index==sizes.Length-1;
                var target=new RenderTexture(size.x,size.y,24); camera.targetTexture=target;
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                Vector2 originalMin=safeRect.anchorMin,originalMax=safeRect.anchorMax;
                if(cutout)
                {
                    safeView.enabled=false;
                    safeRect.anchorMin=new Vector2(0,.045f); safeRect.anchorMax=new Vector2(1,.91f);
                    Canvas.ForceUpdateCanvases();
                }
                CheckBounds(view);
                camera.Render(); var previous=RenderTexture.active; RenderTexture.active=target;
                var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,size.x,size.y),0,0); image.Apply(); RenderTexture.active=previous;
                File.WriteAllBytes(Path.Combine(folder,name+"-"+size.x+"x"+size.y+
                    (cutout?"-cutout":"")+".png"),image.EncodeToPNG());
                Object.Destroy(image); camera.targetTexture=null; target.Release(); Object.Destroy(target);
                safeRect.anchorMin=originalMin; safeRect.anchorMax=originalMax; safeView.enabled=true;
            }
            canvas.renderMode=oldMode; canvas.worldCamera=oldCamera;
            Object.Destroy(camera.gameObject); yield return null; Canvas.ForceUpdateCanvases(); yield return null;
        }
        private static void CheckBounds(Component view)
        {
            var safe=(RectTransform)view.GetComponentInChildren<SafeAreaView>().transform;
            var corners=new Vector3[4];
            foreach(var button in view.GetComponentsInChildren<Button>().Where(button=>button.IsInteractable()))
            {
                ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach(var corner in corners)
                {
                    var local=safe.InverseTransformPoint(corner);
                    Assert.That(local.x,Is.InRange(safe.rect.xMin-2,safe.rect.xMax+2),button.name);
                    Assert.That(local.y,Is.InRange(safe.rect.yMin-2,safe.rect.yMax+2),button.name);
                }
            }
            if (view is SiegeTrialView rescue && MainApproachDriver.Button(rescue, "Top").isActiveAndEnabled)
            {
                var targets = new[] { "Top", "Mid", "Bottom" }.Select(name =>
                    (RectTransform)MainApproachDriver.Button(rescue, name).transform).ToArray();
                for (int i = 0; i < targets.Length; i++)
                for (int j = i + 1; j < targets.Length; j++)
                {
                    targets[i].GetWorldCorners(corners);
                    var first = new Rect(corners[0], corners[2] - corners[0]);
                    targets[j].GetWorldCorners(corners);
                    var second = new Rect(corners[0], corners[2] - corners[0]);
                    Assert.That(first.Overlaps(second), Is.False, "Route hit targets must remain separate.");
                }
            }
            var background=view.GetComponentsInChildren<Image>().Single(item=>item.name=="LocationBackground");
            background.rectTransform.GetWorldCorners(corners);
            var canvas=(RectTransform)view.GetComponent<Canvas>().transform;
            Assert.That(canvas.InverseTransformPoint(corners[0]).x,Is.LessThanOrEqualTo(canvas.rect.xMin+1));
            Assert.That(canvas.InverseTransformPoint(corners[0]).y,Is.LessThanOrEqualTo(canvas.rect.yMin+1));
            Assert.That(canvas.InverseTransformPoint(corners[2]).x,Is.GreaterThanOrEqualTo(canvas.rect.xMax-1));
            Assert.That(canvas.InverseTransformPoint(corners[2]).y,Is.GreaterThanOrEqualTo(canvas.rect.yMax-1));
        }
    }
}
#endif
