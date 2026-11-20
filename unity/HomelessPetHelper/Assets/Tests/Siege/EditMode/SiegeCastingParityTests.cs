using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HomelessPetHelper.Editor.Siege;
using HomelessPetHelper.Modules.Siege;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Tests.Siege.EditMode
{
    public sealed class SiegeCastingParityTests
    {
        [TestCase("cat", "calm", "0e9b238c6fdec2c6ec477db566a7e0d230bbe2d08915c6cff4bcc4a4e0a15e72")]
        [TestCase("cat", "alert", "1a69d3049cee3a3c1da23c75013edbb4f645cca7a3af270a6198a8e74d1109ba")]
        [TestCase("cat", "tired", "189606f9f12d58292b99e5d2fd9023201c450615cbabb06505891bd3150a2cae")]
        [TestCase("dog", "calm", "f3a11c640d461d9cfd460454b7a5b8cf6bebfa9b8b5c251b6964bdc3edb44757")]
        [TestCase("dog", "alert", "7f229cb64f5607d039041a945f04e6a14e2bfb5ba5648f4dab88ab9c66bc4c4d")]
        [TestCase("dog", "tired", "8f38aaf5833ae010b457074d2970df7ffc38e3ee5ae2bb1a489040335a0f433e")]
        public void ExistingCatDogTickTraceIsUnchanged(string encounter, string profile, string expected)
        {
            var data = JsonUtility.FromJson<SiegeCatalogData>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(SiegeTrialAssets.DataPath).text);
            var session = new SiegeSession(new SiegeCatalog(data), 132, profile, "cast-parity", encounter);
            var trace = new StringBuilder();
            for (int tick = 0; tick < 3000; tick++)
            {
                var state = session.Snapshot;
                Append(trace, state);
                if (state.Phase == BattlePhase.Won || state.Phase == BattlePhase.Timeout) break;
                if (state.Phase == BattlePhase.Decision)
                {
                    var order = Plan(session, state);
                    Assert.That(session.TryOrder(order, true, out string reason, out _), Is.True, reason);
                }
                session.Advance(50);
            }
            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(trace.ToString())))
                    .Replace("-", string.Empty).ToLowerInvariant();
                TestContext.Out.WriteLine(encounter + "/" + profile + " " + actual);
                Assert.That(actual, Is.EqualTo(expected));
            }
        }

        private SiegeOrder Plan(SiegeSession session, SiegeState state)
        {
            var order = new SiegeOrder { SessionId = state.SessionId, Turn = state.Turn };
            if (state.Hand.Count > 0 && state.Energy >= 2)
            {
                order.DeployCardId = state.Hand[0].Id;
                order.DeployLaneId = new[] { "mid", "top", "bottom" }[state.Turn % 3];
            }
            var group = state.Units.Where(x => x.TransitRemainingMs == 0
                    && !string.IsNullOrEmpty(session.Catalog.Unit(x.DefinitionId).UpgradeId))
                .GroupBy(x => x.DefinitionId).FirstOrDefault(x => x.Count() >= 3);
            if (group != null && state.Energy >= (order.DeployCardId.HasValue ? 3 : 1))
            {
                order.MergeIds = group.Take(3).Select(x => x.Id).ToArray();
                order.AnchorId = group.Take(3).OrderByDescending(x => x.Distance).First().Id;
            }
            return order;
        }

        private void Row(StringBuilder trace, params object[] values)
        {
            foreach (var value in values)
                trace.Append(value is double number ? number.ToString("R", CultureInfo.InvariantCulture)
                    : Convert.ToString(value, CultureInfo.InvariantCulture)).Append('|');
            trace.Append('\n');
        }

        private void Append(StringBuilder trace, SiegeState s)
        {
            Row(trace, s.ElapsedMs, s.Phase, s.Turn, s.ResolvedTurn, s.Energy, s.Progress, s.PlayerChargeMs);
            var a = s.AnimalState;
            Row(trace, a.Charge, a.Actions, a.Intent, a.IntentLane, a.IntentPad, a.IntentUnitId, a.SeenTurn,
                a.GuardLane, a.GuardUntilMs, a.RecoveryUntilMs, a.SweepCenter.X, a.SweepCenter.Y, a.SweepRadius,
                a.LeapStartMs, a.ImpactAtMs, a.BuildsLeft, a.RepairsLeft, a.RepairTowerId, a.RepairReadyAtMs,
                a.NextRepairMs, a.HeldUnitId, a.HoldStartedMs, a.HoldUntilMs, a.NextHoldMs);
            foreach (var card in s.Hand) Row(trace, "card", card.Id, card.DefinitionId);
            foreach (var u in s.Units)
                Row(trace, "unit", u.Id, u.DefinitionId, u.LaneId, u.Distance, u.Hp, u.Shield, u.Brace,
                    u.Reroutes, u.TransitRemainingMs, u.HeldUntilMs, u.HoldProtectedUntilMs, u.AbilityCharges);
            foreach (var t in s.Towers)
                Row(trace, "tower", t.Id, t.DefinitionId, t.Status, t.Hp, t.ReadyAtMs, t.WindingUp,
                    t.WarningReadyMs, t.SeenTurn, t.LockedPoint.X, t.LockedPoint.Y);
            foreach (var z in s.SlowZones) Row(trace, "zone", z.Center.X, z.Center.Y, z.Radius, z.Factor, z.UntilMs);
            foreach (var e in s.Effects)
                Row(trace, "fx", e.Kind, e.From.X, e.From.Y, e.To.X, e.To.Y, e.Radius, e.StartMs, e.UntilMs);
            Row(trace, string.Join(",", s.Log));
            Row(trace, s.Stats.Deployed, s.Stats.Merges, s.Stats.Towers, s.Stats.Delivered,
                s.Stats.Lost, s.Stats.Repairs, s.Stats.Holds, s.Stats.HoldMisses);
        }
    }
}
