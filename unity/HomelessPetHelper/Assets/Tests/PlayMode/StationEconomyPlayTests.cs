using System;
using System.Collections;
using System.Linq;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using HomelessPetHelper.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class StationEconomyPlayTests
    {
        private MemorySaveStorage _save;
        private MainSceneStorageScope _scope;
        private MainGameRoot Root => Object.FindObjectOfType<MainGameRoot>();
        private StationPanelView Station => Object.FindObjectOfType<StationPanelView>();

        [SetUp] public void SetUp()
        {
            _save = new MemorySaveStorage();
            _scope = new MainSceneStorageScope(createStorage: () => _save);
        }

        [TearDown] public void TearDown() => _scope.Dispose();

        private static IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RescuePaysOnceAndClipReplacesCardOnTheNextOuting()
        {
            yield return LoadMain();
            Assert.That(Root.Presenter.Session.Station.TemporaryCapacity, Is.EqualTo(6));
            var battle = Depart("Yard");
            yield return Win(battle);
            MainApproachDriver.Click(Object.FindObjectOfType<SiegeTrialView>(), "Retry");
            Assert.That(Station, Is.Not.Null);
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.EqualTo(100));
            Assert.That(Station.GetComponentsInChildren<Text>().Any(label => label.text.Contains("+100")), Is.True);
            MainApproachDriver.Click(Station, "Enter");
            Station.Scene.Cells.First(cell => cell.gameObject.activeInHierarchy).SelectButton.onClick.Invoke();
            MainApproachDriver.Click(Station, "Check");
            MainApproachDriver.Click(Station, "Check");
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.EqualTo(150));
            MainApproachDriver.Click(Station, "Back");
            yield return LoadMain();
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.EqualTo(150));
            OpenStore();
            Overlay("OverlayBrowse");
            Overlay("OverlayBrowse");
            yield return ConfirmPurchase();
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.EqualTo(50));
            Overlay("OverlayConfirm");
            Assert.That(Read()["Economy"]["ClipEquipped"].Value<bool>(), Is.True);
            Overlay("OverlayCancel");
            CloseMenu();
            var next = Depart("Treehouse");
            Assert.That(next.Catalog.Unit("clip").Ability, Is.EqualTo("interfere"));
            var supply = next.Catalog.Content.Encounter(next.Snapshot.EncounterId).UnitIds;
            Assert.That(supply, Does.Contain("clip"));
            Assert.That(supply.Count, Is.EqualTo(3));
            var initialHand = next.Snapshot.Hand.Select(card => card.DefinitionId).ToArray();
            yield return LoadMain();
            var restored = (SiegeRescueActivity)Root.Presenter.Session.Activity;
            var restoredState = restored.Battle.Snapshot;
            Assert.That(restored.Battle.Catalog.Content.Encounter(restoredState.EncounterId).UnitIds,
                Is.EqualTo(supply));
            Assert.That(restoredState.Hand.Select(card => card.DefinitionId), Is.EqualTo(initialHand));
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.EqualTo(50));
        }

        [UnityTest]
        public IEnumerator ExpansionAndCleaningSurviveReloadAndFailedPurchaseDoesNotSpend()
        {
            yield return LoadMain();
            Assert.That(Root.Presenter.Progress.TryCheckpoint(), Is.True);
            FundStationFixture(400);
            yield return LoadMain();
            OpenStore();
            _save.FailWrites = true;
            yield return ConfirmPurchase();
            Assert.That(Root.Presenter.Session.Station.TemporaryCapacity, Is.EqualTo(6));
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.EqualTo(400));
            _save.FailWrites = false;
            yield return LoadMain();
            OpenStore();
            yield return ConfirmPurchase();
            Assert.That(Root.Presenter.Session.Station.TemporaryCapacity, Is.EqualTo(8));
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.EqualTo(150));
            Overlay("OverlayBrowse");
            yield return ConfirmPurchase();
            Assert.That(Read()["Economy"]["Cleaning"].Value<bool>(), Is.True);
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.Zero);
            yield return LoadMain();
            Assert.That(Root.Presenter.Session.Station.TemporaryCapacity, Is.EqualTo(8));
            CheckExpandedRoom();
            OpenStore();
            Assert.That(Object.FindObjectsOfType<Button>().Any(button => button.name == "OverlayConfirm"), Is.False);
            Overlay("OverlayCancel");
            CloseMenu();
            yield return Win(Depart("Yard"));
            MainApproachDriver.Click(Object.FindObjectOfType<SiegeTrialView>(), "Retry");
            MainApproachDriver.Click(Station, "Enter");
            Station.Scene.Cells.First(cell => cell.gameObject.activeInHierarchy).SelectButton.onClick.Invoke();
            MainApproachDriver.Click(Station, "Check");
            MainApproachDriver.Click(Station, "Clean");
            MainApproachDriver.Click(Station, "Finish");
            Overlay("OverlayConfirm");
            var progress = Root.Presenter.Progress;
            Guid animal = progress.Session.Station.Animals.Single().AnimalId;
            Assert.That(progress.Care.Get(animal).IsActive, Is.True);
            Assert.That(Station.GetComponentsInChildren<Text>(true)
                .Any(label => label.text.Contains("正在接受梳理")), Is.True);
            MainApproachDriver.Click(Station, "Finish");
            MainApproachDriver.Click(Station, "Back");
            Assert.That(progress.TryPublishAdoption(animal), Is.True);
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.EqualTo(150));
            int applicant = animal.ToByteArray()[0] % 3;
            Assert.That(progress.TrySendForAdoption(animal, "Milo", applicant), Is.True);
            Assert.That(progress.TrySendForAdoption(animal, "Milo", applicant), Is.False);
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.EqualTo(200));
        }

        [UnityTest]
        public IEnumerator VersionFourKeepsExistingBedsAndStartsWithAnEmptyWallet()
        {
            yield return LoadMain();
            Assert.That(Root.Presenter.Progress.TryCheckpoint(), Is.True);
            var legacy = Read();
            legacy.Remove("Economy");
            legacy["Capacity"] = 3;
            SeedLegacy(legacy, 4);
            yield return LoadMain();
            Assert.That(Root.Presenter.Session.Station.TemporaryCapacity, Is.EqualTo(3));
            Assert.That(Root.Presenter.Progress.TryCheckpoint(), Is.True);
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator VersionFiveKeepsCustomAnimalName()
        {
            yield return LoadMain();
            yield return Win(Depart("Yard"));
            MainApproachDriver.Click(Object.FindObjectOfType<SiegeTrialView>(), "Retry");
            var progress = Root.Presenter.Progress;
            var id = progress.Session.Station.Animals.Single().AnimalId;
            Assert.That(progress.TryRenameResident(id, "栗子"), Is.True);
            var legacy = Read();
            legacy.Remove("Economy");
            SeedLegacy(legacy, 5);
            yield return LoadMain();
            Assert.That(Root.Presenter.Progress.GetCustomName(id), Is.EqualTo("栗子"));
            Assert.That(Root.Presenter.Progress.TryCheckpoint(), Is.True);
            Assert.That(Read()["Economy"]["Balance"].Value<int>(), Is.Zero);
        }

        private void SeedLegacy(JObject legacy, int version)
        {
            var section = new SaveSection<JObject>("main.station", version, () => new JObject(), _ => null);
            var seed = new SaveService(_save = new MemorySaveStorage());
            seed.Register(section);
            Assert.That(seed.TryOpen(out _, out var error), Is.True, error?.Message);
            Assert.That(seed.TryCommit(writer => writer.Set(section, legacy), out error), Is.True, error?.Message);
        }

        private void CheckExpandedRoom()
        {
            var art = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
            var scene = Station.Scene;
            try
            {
                var residents = Enumerable.Range(0, 8).Select(index =>
                    new StationSceneResident(Guid.NewGuid(), index, "Resident", art, false, true)).ToArray();
                scene.Show(residents, -1);
                Assert.That(scene.Cells.Select(cell => cell.AnimalId), Is.EqualTo(residents.Select(r => r.AnimalId)));
                Assert.That(scene.Cells.All(cell => cell.gameObject.activeSelf), Is.True);
                var last = scene.Cells.Last();
                scene.Show(residents.Take(2).ToArray(), -1);
                Assert.That(last.gameObject.activeSelf, Is.False);
                scene.Show(residents, -1);
                Assert.That(scene.Cells.Last(), Is.SameAs(last));
            }
            finally
            {
                scene.ClearResidents();
                Object.Destroy(art);
            }
        }

        private SiegeSession Depart(string point)
        {
            MainApproachDriver.Click(Station, "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            MainApproachDriver.Click(map, point);
            MainApproachDriver.Click(map, "Primary");
            if (point == "Treehouse")
            {
                var approach = Object.FindObjectOfType<ApproachPanelView>();
                Canvas.ForceUpdateCanvases();
                foreach (int cell in new[] { 65, 57, 50, 3, 5, 51 }) MainApproachDriver.Clean(approach, cell);
                MainApproachDriver.Click(approach, "Continue");
            }
            else MainApproachDriver.Complete();
            return ((SiegeRescueActivity)Root.Presenter.Session.Activity).Battle;
        }

        private static IEnumerator Win(SiegeSession battle)
        {
            for (int tick = 0; tick < 700 && battle.Phase != SiegeState.BattlePhase.Won; tick++)
            {
                MainApproachDriver.AcknowledgeEnemyIntroduction();
                var state = battle.Snapshot;
                var card = state.Hand.FirstOrDefault(item =>
                    battle.Catalog.Unit(item.DefinitionId).Cost <= state.Energy);
                if (card != null)
                {
                    var lane = battle.Catalog.Lanes[state.Stats.Deployed % battle.Catalog.Lanes.Count];
                    var point = lane.PointAt(lane.Length * .2);
                    if (!SiegeDeployment.CanDeploy(battle.Catalog, point)) point = lane.PointAt(0);
                    battle.TryOrder(new SiegeOrder
                    {
                        SessionId = state.SessionId, Turn = state.Turn,
                        DeployCardId = card.Id, DeployPoint = point
                    }, true, out _, out _);
                }
                battle.Advance(1000);
                yield return null;
            }
            Assert.That(battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Won));
            yield return null;
        }

        private void OpenStore()
        {
            MainApproachDriver.Click(Station, "StationMenu");
            MainApproachDriver.Click(Station, "TabEconomy");
            MainApproachDriver.Click(Station, "EconomyOffer0");
        }

        private void CloseMenu() => MainApproachDriver.Click(Station, "CloseStationMenu");

        private static void Overlay(string name) => Object.FindObjectsOfType<Button>()
            .Single(button => button.name == name && button.IsInteractable()).onClick.Invoke();

        private static IEnumerator ConfirmPurchase()
        {
            yield return new WaitForSecondsRealtime(.4f);
            Overlay("OverlayConfirm");
        }

        private SaveService OpenRaw(out SaveSection<JObject> section)
        {
            // Inspect or fund an isolated test wallet through the real envelope, never through a game cheat.
            section = new SaveSection<JObject>("main.station", 6, () => new JObject(), _ => null);
            var saves = new SaveService(_save);
            saves.Register(section);
            Assert.That(saves.TryOpen(out _, out var error), Is.True, error?.Message);
            return saves;
        }

        private JObject Read()
        {
            var saves = OpenRaw(out var section);
            Assert.That(saves.TryRead(section, out var data, out var error), Is.True, error?.Message);
            return data;
        }

        private void FundStationFixture(int funds)
        {
            var data = Read();
            data["Economy"]["Balance"] = funds;
            var saves = OpenRaw(out var section);
            Assert.That(saves.TryCommit(writer => writer.Set(section, data), out var error), Is.True, error?.Message);
        }
    }
}
