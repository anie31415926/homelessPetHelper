#if UNITY_EDITOR
using System.Linq;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.Siege.PlayMode
{
    public sealed class SiegeFieldInputTests
    {
        private SiegeSession Session()
        {
            var data=JsonUtility.FromJson<SiegeCatalogData>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Siege/SiegeTrialData.json").text);
            data.rules.playerPeriodMs=200; data.rules.turnEnergy=6; data.rules.openingEnergy=6;
            foreach(var unit in data.contentUnits) unit.speed=.01;
            foreach(var profile in data.animalStates) profile.periodMs=100000;
            return new SiegeSession(new SiegeCatalog(data),132,"calm","field-input","cat");
        }
        private static void Commit(SiegeSession session,SiegeOrder order)
        {
            var state=session.Snapshot; order.SessionId=state.SessionId; order.Turn=state.Turn;
            Assert.That(session.TryOrder(order,true,out var reason,out _),Is.True,reason);
        }
        private static void Next(SiegeSession session)
        {
            for(int i=0;i<100 && session.Phase==SiegeState.BattlePhase.Running;i++)session.Advance(50);
            Assert.That(session.Phase,Is.EqualTo(SiegeState.BattlePhase.Decision));
        }
        private static int Card(SiegeSession session,string type)
        {
            for(int i=0;i<60;i++)
            {
                var state=session.Snapshot; var card=state.Hand.FirstOrDefault(item=>item.DefinitionId==type);
                if(card!=null)return card.Id;
                Commit(session,new SiegeOrder{DiscardCardId=state.Hand[0].Id}); Next(session);
            }
            Assert.Fail("Missing supply "+type); return 0;
        }
        [TestCase("feather",true)] [TestCase("yarn",false)]
        public void CrossLaneMergeIncludesPlannedNewUnitAndOnlyActualAbilityAllowsRerouting(string type,bool reroutes)
        {
            var session=Session(); int a,b;
            Commit(session,new SiegeOrder{DeployCardId=Card(session,type),DeployLaneId="top"});
            a=session.Snapshot.Units.Last().Id; Next(session);
            Commit(session,new SiegeOrder{DeployCardId=Card(session,type),DeployLaneId="mid"});
            b=session.Snapshot.Units.Last().Id; Next(session);
            var plan=new SiegeFieldPlan(); plan.SelectCard(Card(session,type)); plan.ChooseLane("bottom");
            var before=session.Snapshot; plan.Focus(a,session.Catalog,before); plan.BeginMerge(session.Catalog,before);
            plan.Focus(b,session.Catalog,before); plan.Focus(SiegeOrder.NewUnit,session.Catalog,before);
            var order=plan.Order(before);
            Assert.That(order.AnchorId,Is.EqualTo(a));
                Assert.That(order.MergeIds,Is.EquivalentTo(new[]{a,b,SiegeOrder.NewUnit}));
            Assert.That(session.TryOrder(order,false,out var reason,out _),Is.True,reason);
            Assert.That(session.Snapshot.Energy,Is.EqualTo(before.Energy));
            Assert.That(session.Snapshot.Units.Count,Is.EqualTo(2));
            Commit(session,order); Assert.That(session.Snapshot.Stats.Merges,Is.EqualTo(1)); Next(session);
            var upgraded=session.Snapshot.Units.Single(); plan.Clear();
                plan.Focus(upgraded.Id,session.Catalog,session.Snapshot);
            Assert.That(plan.CanReroute(session.Snapshot),Is.EqualTo(reroutes));
            plan.BeginReroute(session.Snapshot);
            Assert.That(plan.ShowsEntrances,Is.EqualTo(reroutes));
            if(!reroutes)return;
            plan.ChooseLane("bottom"); plan.CancelSelection();
            var reroute=plan.Order(session.Snapshot);
            Assert.That(reroute.RerouteUnitId,Is.EqualTo(upgraded.Id),"Dismissing context retains planned target.");
            Commit(session,reroute); Assert.That(session.Snapshot.Units.Single().LaneId,Is.EqualTo("bottom"));
        }
        [Test]
        public void DraftCancelAndInvalidRouteNeverSpendOrDeploy()
        {
            var session=Session(); var plan=new SiegeFieldPlan(); var before=session.Snapshot;
            plan.SelectCard(before.Hand[0].Id); plan.ChooseLane("unknown");
            Assert.That(session.TryOrder(plan.Order(before),false,out _,out _),Is.False);
            Assert.That(session.Snapshot.Energy,Is.EqualTo(before.Energy));
                Assert.That(session.Snapshot.Units,Is.Empty);
            plan.Clear(); Assert.That(plan.HasAction,Is.False); Assert.That(plan.Order(before).DeployCardId,Is.Null);
        }
        [Test]
        public void DragCancelAndDisabledCardRejectLatePointerRelease()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab");
            var root=Object.Instantiate(prefab); root.SetActive(true);
            try
            {
                var cell=root.GetComponent<SiegeChoiceCell>(); int drops=0,cancels=0;
                cell.Bind(7,"",false,true,_=>{});
                cell.SetDragHandlers((_,__)=>{},(_,__)=>drops++,()=>cancels++);
                var pointer=new PointerEventData(null){pointerId=1,button=PointerEventData.InputButton.Left};
                cell.OnBeginDrag(pointer); cell.OnCancel(pointer); cell.OnEndDrag(pointer);
                Assert.That(drops,Is.Zero); Assert.That(cancels,Is.EqualTo(1));
                cell.OnBeginDrag(pointer); cell.Bind(7,"",false,false,_=>{}); cell.OnEndDrag(pointer);
                Assert.That(drops,Is.Zero); Assert.That(cancels,Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
