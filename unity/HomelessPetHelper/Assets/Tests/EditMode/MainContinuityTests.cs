using System;
using System.IO;
using System.Linq;
using System.Text;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Progress;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.Cleaning;
using HomelessPetHelper.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class MainContinuityTests
    {
        private GameContent _content;
        private CityMapPreviewConfig _config;
        [SetUp] public void LoadContent()
        {
            _content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            _config = _content.CreateMapConfig();
        }
        private MainProgressSession Open(ISaveStorage storage) => new MainProgressSession(_config,
            _content.Artwork, storage);

        [TestCase(0, 300)]
        [TestCase(1, 240)]
        [TestCase(5, 180)]
        public void DispatchWaitsForWorldTimeAndPersistsTheSameNotice(int capacity, int waitMinutes)
        {
            var storage = new MemorySaveStorage();
            Guid noticeId;
            using (var progress = OpenDispatch(storage, capacity))
            {
                var session = progress.Session;
                var initial = session.Requests.Requests.Single(request => request.PointId == "parking");
                Assert.That(session.Requests.TryTransition(initial.RequestId, RequestSignal.HelpedElsewhere, out _),
                    Is.True);
                AdvanceDispatchTime(progress, waitMinutes - 1);
                Assert.That(session.Requests.Requests.Count, Is.EqualTo(4));
                if (capacity == 0)
                {
                    Assert.That(session.Station.TryExpandTemporaryCapacity(session.StationSessionId, 1, out _),
                        Is.True);
                    Assert.That(progress.TryCheckpoint(), Is.True);
                    Assert.That(session.Requests.Requests.Count, Is.EqualTo(4), "Expansion must not send a notice.");
                }
                AdvanceDispatchTime(progress, 1);
                noticeId = session.Requests.Requests.Last().RequestId;
                Assert.That(session.Map.TrySelectPoint("parking", out var notice, out _), Is.True);
                Assert.That(notice.RequestId, Is.EqualTo(noticeId));
            }

            using var restored = OpenDispatch(storage, capacity);
            Assert.That(restored.TryCheckpoint(), Is.True, restored.PersistenceError?.ToString());
            Assert.That(restored.Session.Requests.Requests.Count, Is.EqualTo(5));
            Assert.That(restored.Session.Map.TrySelectPoint("parking", out var same, out _), Is.True);
            Assert.That(same.RequestId, Is.EqualTo(noticeId));
        }

        [Test]
        public void FullStationCanReceiveNoticeAndExpiredNoticeDoesNotEmptyPlayableMapAfterExpansion()
        {
            using var progress = OpenDispatch(new MemorySaveStorage(), 0);
            var session = progress.Session;
            foreach (var request in session.Requests.Requests.Where(request => request.ReunionPlan == null))
                Assert.That(session.Requests.TryTransition(request.RequestId, RequestSignal.HelpedElsewhere, out _),
                    Is.True);
            AdvanceDispatchTime(progress, 300);
            var notice = session.Requests.Requests.Last();
            Assert.That(notice.State, Is.EqualTo(RequestState.Available));
            Assert.That(session.Map.TryPlanDeparture(notice.PointId, 0, out _, out var blocked), Is.False);
            Assert.That(blocked, Is.EqualTo(OutingFailure.CapacityNotAvailable));
            Assert.That(session.Station.TryExpandTemporaryCapacity(session.StationSessionId, 5, out _), Is.True);
            Assert.That(progress.TryCheckpoint(), Is.True);
            Assert.That(session.Requests.Requests.Count, Is.EqualTo(5));

            AdvanceDispatchTime(progress, 360);
            Assert.That(notice.State, Is.EqualTo(RequestState.HelpedElsewhere));
            var fallback = session.Requests.Requests.Last();
            Assert.That(fallback.RequestId, Is.Not.EqualTo(notice.RequestId));
            Assert.That(fallback.PointId, Is.Not.EqualTo(notice.PointId));
            Assert.That(session.Map.TryPlanDeparture(fallback.PointId, 5, out _, out _), Is.True);
            Assert.That(session.Station.Animals, Is.Empty);
        }

        [Test]
        public void DispatchNoticeSurvivesRescueTravelAndStillExpiresAtTheStation()
        {
            var storage = new MemorySaveStorage();
            using var progress = OpenDispatch(storage, 2, 120);
            var session = progress.Session;
            var initial = session.Requests.Requests.Single(request => request.PointId == "parking");
            Assert.That(session.Requests.TryTransition(initial.RequestId, RequestSignal.HelpedElsewhere, out _),
                Is.True);
            AdvanceDispatchTime(progress, 240);
            var notice = session.Requests.Requests.Last();
            Assert.That(notice.PointId, Is.EqualTo("parking"));
            Assert.That(notice.State, Is.EqualTo(RequestState.Available));

            Rescue(progress, "ordinary-dog");
            Assert.That(progress.TryCheckpoint(), Is.True, progress.PersistenceError?.ToString());
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.GreaterThanOrEqualTo(390));
            Assert.That(notice.State, Is.EqualTo(RequestState.Available),
                "A long rescue must not age the offer it could not answer.");

            AdvanceDispatchTime(progress, 120);
            Assert.That(notice.State, Is.EqualTo(RequestState.HelpedElsewhere),
                "Station time must still expire an unanswered offer.");
        }

        [Test]
        public void DispatchPaysAtMostTwoNoticesAfterOneLongJump()
        {
            using var progress = OpenDispatch(new MemorySaveStorage(), 5);
            var session = progress.Session;
            foreach (var request in session.Requests.Requests.Where(request => request.ReunionPlan == null))
                Assert.That(session.Requests.TryTransition(request.RequestId, RequestSignal.HelpedElsewhere,
                    out _), Is.True);
            AdvanceDispatchTime(progress, 361);
            var created = session.Requests.Requests.Skip(4).ToArray();
            Assert.That(created.Length, Is.EqualTo(2), "Two missed intervals, not an unbounded backlog.");
            Assert.That(created[0].PointId, Is.Not.EqualTo(created[1].PointId));
            AdvanceDispatchTime(progress, 1);
            Assert.That(session.Requests.Requests.Count, Is.EqualTo(6));
        }

        private MainProgressSession OpenDispatch(ISaveStorage storage, int capacity, int lifetimeMinutes = 0) =>
            new MainProgressSession(WithInitialCapacity(capacity, lifetimeMinutes), _content.Artwork, storage,
                _content.CreateFieldProgressContent(_content.CreateFields()));

        private static void AdvanceDispatchTime(MainProgressSession progress, long minutes)
        {
            var clock = progress.Session.Clock;
            Assert.That(clock.TryAdvance(clock.Current.Version, minutes, out _, out _), Is.True);
            Assert.That(progress.TryCheckpoint(), Is.True, progress.PersistenceError?.ToString());
        }

        private CityMapPreviewConfig WithInitialCapacity(int capacity, int lifetimeMinutes = 0)
        {
            var data = JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Game/CityMapConfig.json").text);
            data["initialCapacity"] = capacity;
            if (lifetimeMinutes > 0) data["dispatch"]["lifetimeMinutes"] = lifetimeMinutes;
            var source = new TextAsset(data.ToString());
            try { return new CityMapPreviewConfig(source); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [TestCase(0, 0, 5)]
        [TestCase(1, 4, 0)]
        [TestCase(1, 4, 20)]
        [TestCase(2, 2, 20)]
        [TestCase(2, int.MaxValue, 2)]
        public void SavedCapacityIsIndependentOfNewGameDefaults(int initial, int capacity, int nextInitial)
        {
            var storage = new MemorySaveStorage();
            _config = WithInitialCapacity(initial);
            using (var first = Open(storage))
            {
                if (capacity > initial)
                    Assert.That(first.Session.Station.TryExpandTemporaryCapacity(
                        first.Session.StationSessionId, capacity, out _), Is.True);
                Assert.That(first.TryCheckpoint(), Is.True, first.PersistenceError?.ToString());
            }
            int writes = storage.Writes;
            _config = WithInitialCapacity(nextInitial);
            using var restored = Open(storage);
            Assert.That(restored.CanContinue, Is.True, restored.PersistenceError?.ToString());
            Assert.That(restored.Session.Station.TemporaryCapacity, Is.EqualTo(capacity));
            Assert.That(restored.Session.Station.AvailableTemporaryPlaces, Is.EqualTo(capacity));
            Assert.That(storage.Writes, Is.EqualTo(writes), "Loading must not rewrite the saved capacity.");
            Assert.That(restored.TryCheckpoint(), Is.True);
            using var again = Open(storage);
            Assert.That(again.Session.Station.TemporaryCapacity, Is.EqualTo(capacity));
            using var fresh = Open(new MemorySaveStorage());
            Assert.That(fresh.Session.Station.TemporaryCapacity, Is.EqualTo(nextInitial));
        }

        [Test]
        public void FullStationCanExpandSaveReloadAndRescueIntoTheNewPlace()
        {
            var storage = new MemorySaveStorage();
            _config = WithInitialCapacity(1);
            Guid cat;
            using (var first = Open(storage))
            {
                cat = Rescue(first, "ordinary");
                Check(first, cat);
                var station = first.Session.Station;
                Assert.That(station.IsFull, Is.True);
                Assert.That(first.Session.Map.TryPlanDeparture("yard", station.AvailableTemporaryPlaces,
                    out _, out var failure), Is.False);
                Assert.That(failure, Is.EqualTo(OutingFailure.CapacityNotAvailable));
                Assert.That(station.TryExpandTemporaryCapacity(first.Session.StationSessionId, 2, out _), Is.True);
                Assert.That(first.TryCheckpoint(), Is.True, first.PersistenceError?.ToString());
            }
            using (var restored = Open(storage))
            {
                Assert.That(restored.CanContinue, Is.True, restored.PersistenceError?.ToString());
                Assert.That(restored.Session.Station.AvailableTemporaryPlaces, Is.EqualTo(1));
                Assert.That(restored.Session.Station.Animals.Single().AnimalId, Is.EqualTo(cat));
                var dog = Rescue(restored, "ordinary-dog");
                Check(restored, dog);
                Assert.That(restored.Session.Station.IsFull, Is.True);
                Assert.That(restored.TryCheckpoint(), Is.True);
            }
            using var again = Open(storage);
            Assert.That(again.Session.Station.TemporaryCapacity, Is.EqualTo(2));
            Assert.That(again.Session.Station.Animals.Count, Is.EqualTo(2));
            Assert.That(again.Session.Station.IsFull, Is.True);
        }

        [Test]
        public void ConfirmedAdoptionFreesTheOnlyBedAndSurvivesAnotherRescue()
        {
            var storage = new MemorySaveStorage();
            _config = WithInitialCapacity(1);
            Guid cat, dog;
            using (var first = Open(storage))
            {
                cat = Rescue(first, "ordinary");
                int applicant = (cat.ToByteArray()[0] % 3 + 1) % 3;
                Assert.That(first.TryPublishAdoption(cat), Is.False, "An unchecked animal cannot be listed.");
                Check(first, cat);
                Assert.That(first.TrySendForAdoption(cat, "Milo", applicant), Is.False);
                Assert.That(first.TryPublishAdoption(cat), Is.True);
                Assert.That(first.TryPublishAdoption(cat), Is.False);
                Assert.That(first.Session.Station.IsFull, Is.True);
                Assert.That(first.TrySendForAdoption(cat, "Milo", -1), Is.False);
                long elapsed = first.Session.Clock.Current.ElapsedMinutes;
                Assert.That(first.TrySendForAdoption(cat, "Milo", applicant), Is.True,
                    first.PersistenceError?.ToString());
                Assert.That(first.TrySendForAdoption(cat, "Milo", applicant), Is.False);
                Assert.That(first.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(elapsed));
                Assert.That(first.Session.Station.AvailableTemporaryPlaces, Is.EqualTo(1));
                dog = Rescue(first, "ordinary-dog");
                Check(first, dog);
                Assert.That(first.TryCheckpoint(), Is.True);
            }
            using var restored = Open(storage);
            Assert.That(restored.CanContinue, Is.True, restored.PersistenceError?.ToString());
            Assert.That(restored.Session.Station.Animals.Single().AnimalId, Is.EqualTo(dog));
            Assert.That(restored.Session.Station.TryGetCurrentAnimal(cat, out _), Is.False);
            var raw = new SaveService(storage);
            var section = new SaveSection<JObject>("main.station", 6, () => new JObject(), _ => null);
            raw.Register(section);
            Assert.That(raw.TryOpen(out _, out _), Is.True);
            Assert.That(raw.TryRead(section, out var data, out _), Is.True);
            var memorial = data["Memorials"].Single();
            Assert.That((string)memorial["Name"], Is.EqualTo("Milo"));
            Assert.That((string)memorial["Appearance"], Is.EqualTo("cat.shy"));
            Assert.That(data.ToString(), Does.Not.Contain(cat.ToString("N")));
        }

        [Test]
        public void RenameIsDurableAndSaveFailureDoesNotPublishTheDraft()
        {
            var storage = new MemorySaveStorage();
            Guid cat;
            using (var first = Open(storage))
            {
                cat = Rescue(first, "ordinary");
                Assert.That(first.TryRenameResident(cat, string.Empty), Is.False);
                Assert.That(first.TryRenameResident(cat, "123"), Is.False);
                Assert.That(first.TryRenameResident(cat, "<b>Milo</b>"), Is.False);
                Assert.That(first.TryRenameResident(cat, "12345678A"), Is.False);
                Assert.That(first.TryRenameResident(cat, "Milo"), Is.True, first.PersistenceError?.ToString());
                int writes = storage.Writes;
                Assert.That(first.TryRenameResident(cat, "Milo"), Is.True);
                Assert.That(storage.Writes, Is.EqualTo(writes));
                storage.FailWrites = true;
                Assert.That(first.TryRenameResident(cat, "Poppy"), Is.False);
                Assert.That(first.GetCustomName(cat), Is.EqualTo("Milo"));
            }
            storage.FailWrites = false;
            using var restored = Open(storage);
            Assert.That(restored.CanContinue, Is.True, restored.PersistenceError?.ToString());
            Assert.That(restored.GetCustomName(cat), Is.EqualTo("Milo"));
        }

        [Test]
        public void ContentMapsOrdinaryAnimalsAndKeepsOwnerReunionSeparate()
        {
            _content.Validate();
            var requests = _config.CreateRequestPool().Requests;
            var ordinary = requests.Where(request => request.ReunionPlan == null).ToArray();
            Assert.That(ordinary.Length, Is.EqualTo(3));
            Assert.That(ordinary.Select(request => _config.GetRescue(request.TemplateId).EncounterId),
                Is.EquivalentTo(new[] { "cat", "cat", "dog" }));
            Assert.That(requests.Single(request => request.ReunionPlan != null).TemplateId, Is.EqualTo("reunion"));
            var badSource = new TextAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Game/CityMapConfig.json").text.Replace(
                    "\"encounterId\": \"dog\"",
                    "\"encounterId\": \"missing\""));
            try
            {
                Assert.Throws<ArgumentException>(() =>
                    new CityMapPreviewConfig(badSource).ValidateRescues(_content.CreateSiegeCatalog()));
            }
            finally { UnityEngine.Object.DestroyImmediate(badSource); }
        }

        [Test]
        public void OpeningAndDisposalNeverWriteAndRuntimeIdsAreFreshOnReload()
        {
            var storage = new MemorySaveStorage();
            Guid stationId;
            Guid requestId;
            using (var progress = Open(storage))
            {
                Assert.That(storage.Writes, Is.Zero);
                stationId = progress.Session.StationSessionId;
                requestId = progress.Session.Requests.Requests[0].RequestId;
                Assert.That(progress.TryCheckpoint(), Is.True, progress.PersistenceError?.ToString());
            }
            Assert.That(storage.Writes, Is.EqualTo(1));
            using var restored = Open(storage);
            Assert.That(restored.Session.StationSessionId, Is.Not.EqualTo(stationId));
            Assert.That(restored.Session.Requests.Requests[0].RequestId, Is.EqualTo(requestId));
            Assert.That(storage.Writes, Is.EqualTo(1));
        }

        [Test]
        public void UncheckedArrivalSurvivesReloadAndCannotBypassTheCareGate()
        {
            var storage = new MemorySaveStorage();
            Guid animalId;
            Guid outingId;
            using (var first = Open(storage))
            {
                animalId = Rescue(first, "ordinary");
                outingId = first.Session.LastArrival.Arrival.OutingId;
                Assert.That(first.TryCheckpoint(), Is.True, first.PersistenceError?.ToString());
            }
            using var restored = Open(storage);
            var arrival = restored.Session.Station.Animals.Single();
            Assert.That(arrival.AnimalId, Is.EqualTo(animalId));
            Assert.That(arrival.OutingId, Is.EqualTo(outingId));
            Assert.That(restored.Session.TryOpenMap(Guid.NewGuid(), _config.Seed, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.BasicAssessmentRequired));
            Check(restored, animalId);
            Assert.That(restored.Session.TryOpenMap(outingId, _config.Seed, out _), Is.False,
                "An admitted outing ID must not be reused after restoration.");
        }

        [Test]
        public void TwoRealRescuesAndPartialWashGroomRestoreAllSamplesHistoryAndSettledTime()
        {
            var storage = new MemorySaveStorage();
            Guid cat;
            Guid dog;
            long minutes;
            StationCoatCareState catCare;
            StationCoatCareState dogCare;
            using (var first = Open(storage))
            {
                cat = Rescue(first, "ordinary");
                Check(first, cat);
                Work(first.Care.Get(cat), false);
                Work(first.Care.Get(cat), true);
                catCare = first.Care.Get(cat).Capture();
                dog = Rescue(first, "ordinary-dog");
                Check(first, dog);
                Work(first.Care.Get(dog), false);
                dogCare = first.Care.Get(dog).Capture();
                minutes = first.Session.Clock.Current.ElapsedMinutes;
                Assert.That(cat, Is.Not.EqualTo(dog));
                Assert.That(first.Session.Station.Animals[0].AnimalAtArrival.Coat.Dirtiness, Is.EqualTo(65));
                Assert.That(first.TryCheckpoint(), Is.True, first.PersistenceError?.ToString());
            }
            int maximumSlotBytes = storage.Slots.Where(slot => slot != null).Max(Encoding.UTF8.GetByteCount);
            Assert.That(maximumSlotBytes, Is.LessThan(900 * 1024), "Must fit the current WeChat adapter slot budget.");
            TestContext.Out.WriteLine("Two-animal checkpoint maximum slot bytes: " + maximumSlotBytes);
            using var restored = Open(storage);
            Assert.That(restored.CanContinue, Is.True, restored.PersistenceError?.ToString());
            Assert.That(restored.Session.Station.Animals.Select(item => item.AnimalId), Is.EqualTo(new[] { cat, dog }));
            Assert.That(restored.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
            AssertFields(catCare, restored.Care.Get(cat).Capture());
            AssertFields(dogCare, restored.Care.Get(dog).Capture());
            Assert.That(restored.Session.Station.Animals[0].AnimalAtArrival.Coat.Dirtiness, Is.EqualTo(65));
            Assert.That(restored.Session.LastArrival.Arrival.AnimalId, Is.EqualTo(dog));
            foreach (var id in new[] { cat, dog })
            {
                var care = restored.Care.Get(id);
                Assert.That(care.TryBegin(false, out _), Is.True);
                Assert.That(care.TryEnd(out long repeatedMinutes, out _), Is.True);
                Assert.That(repeatedMinutes, Is.Zero, "Reopening care cannot charge old work again.");
            }
            Assert.That(restored.Session.Requests.Requests.Count(request =>
                request.State == RequestState.Resolved), Is.EqualTo(2));
            Assert.That(restored.TryCheckpoint(), Is.True);
            using var again = Open(storage);
            Assert.That(again.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
            Assert.That(again.Session.Station.Animals.Count, Is.EqualTo(2));
        }

        [Test]
        public void MidOutingAndUnendedCareReturnToLastExplicitCheckpointWithoutTimeCatchup()
        {
            var storage = new MemorySaveStorage();
            using var first = Open(storage);
            var cat = Rescue(first, "ordinary");
            Check(first, cat);
            Assert.That(first.TryCheckpoint(), Is.True);
            var care = first.Care.Get(cat);
            var before = care.Capture();
            Assert.That(care.TryBegin(false, out _), Is.True);
            Stroke(care);
            long time = first.Session.Clock.Current.ElapsedMinutes;
            first.Session.SetSuspended(true);
            Assert.That(care.TryEnd(out _, out _), Is.False);
            using (var restored = Open(storage))
            {
                AssertFields(before, restored.Care.Get(cat).Capture());
                Assert.That(restored.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(time));
                BeginRescue(restored, "ordinary-dog");
                Assert.That(restored.Session.Requests.Requests.Single(request =>
                    request.TemplateId == "ordinary-dog").State,
                    Is.EqualTo(RequestState.Accepted));
            }
            using var afterOuting = Open(storage);
            Assert.That(afterOuting.Session.Activity, Is.Null);
            Assert.That(afterOuting.Session.Station.Animals.Count, Is.EqualTo(1));
            Assert.That(afterOuting.Session.Requests.Requests.Single(request =>
                request.TemplateId == "ordinary-dog").State,
                Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void BrokenSlotsAndReadErrorsArePreservedAndBlockGameInsteadOfStartingOver()
        {
            var storage = new MemorySaveStorage();
            storage.Slots[0] = "damaged original";
            using (var broken = Open(storage))
            {
                Assert.That(broken.CanContinue, Is.False);
                Assert.That(broken.Session, Is.Null);
                Assert.That(broken.TryCheckpoint(), Is.False);
                Assert.That(storage.Slots[0], Is.EqualTo("damaged original"));
                Assert.That(storage.Writes, Is.Zero);
            }
            storage.Slots[0] = null;
            storage.FailReads = true;
            using var unreadable = Open(storage);
            Assert.That(unreadable.PersistenceError.Code, Is.EqualTo(SaveErrorCode.StorageReadFailed));
            Assert.That(storage.Writes, Is.Zero);
        }

        [Test]
        public void WriteFailureKeepsMemoryAndLastDurableCheckpointAndBlocksFurtherSaves()
        {
            var storage = new MemorySaveStorage();
            using var progress = Open(storage);
            Assert.That(progress.TryCheckpoint(), Is.True);
            var durable = storage.Slots.ToArray();
            var cat = Rescue(progress, "ordinary");
            storage.FailWrites = true;
            Assert.That(progress.TryCheckpoint(), Is.False);
            Assert.That(progress.CanContinue, Is.False);
            Assert.That(progress.Session.Station.Animals.Single().AnimalId, Is.EqualTo(cat));
            Assert.That(storage.Slots, Is.EqualTo(durable));
            storage.FailWrites = false;
            Assert.That(progress.TryCheckpoint(), Is.False);
            using var restored = Open(storage);
            Assert.That(restored.Session.Station.Animals, Is.Empty);
        }

        [Test]
        public void OneBadSlotRecoversAndArchivesOriginalOnNextCommit()
        {
            var storage = new MemorySaveStorage();
            using (var progress = Open(storage)) Assert.That(progress.TryCheckpoint(), Is.True);
            storage.Slots[1] = "broken inactive slot";
            using var recovered = Open(storage);
            Assert.That(recovered.Recovered, Is.True);
            Assert.That(recovered.TryCheckpoint(), Is.True);
            Assert.That(storage.Archives, Does.Contain("broken inactive slot"));
        }

        [TestCase("assessment")]
        [TestCase("emotion")]
        [TestCase("field")]
        [TestCase("request")]
        [TestCase("animal")]
        [TestCase("time")]
        [TestCase("work")]
        [TestCase("negativeCapacity")]
        [TestCase("overcrowded")]
        public void ChecksummedButSemanticallyInvalidSnapshotsAreBlockedWithoutOverwriting(string corruption)
        {
            var storage = new MemorySaveStorage();
            using (var progress = Open(storage))
            {
                var cat = Rescue(progress, "ordinary");
                Check(progress, cat);
                Assert.That(progress.TryCheckpoint(), Is.True);
            }
            var raw = new SaveService(storage);
            var section = new SaveSection<JObject>("main.station", 6, () => new JObject(), _ => null);
            raw.Register(section);
            Assert.That(raw.TryOpen(out _, out _), Is.True);
            Assert.That(raw.TryRead(section, out var data, out _), Is.True);
            var resident = data["Residents"][0];
            switch (corruption)
            {
                case "assessment": resident["Assessment"] = 99; break;
                case "emotion": resident["Current"]["Emotion"] = 99; break;
                case "field": resident["Coat"]["Dirt"]["InitialAmount"] = -1; break;
                case "request": data["Requests"][0]["State"] = (int)RequestState.Accepted; break;
                case "animal": resident["Current"]["Id"] = Guid.NewGuid().ToString("N"); break;
                case "time": data["ElapsedMinutes"] = 0; break;
                case "work": resident["Coat"]["WashSettled"] = 1000; break;
                case "negativeCapacity": data["Capacity"] = -1; break;
                case "overcrowded": data["Capacity"] = 0; break;
            }
            Assert.That(raw.TryCommit(writer => writer.Set(section, data), out _), Is.True);
            var originals = storage.Slots.ToArray();
            using var blocked = Open(storage);
            Assert.That(blocked.CanContinue, Is.False);
            Assert.That(blocked.Session, Is.Null);
            Assert.That(blocked.TryCheckpoint(), Is.False);
            Assert.That(storage.Slots, Is.EqualTo(originals));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DevelopmentLoadKeepsResidentAndArchivesStaleFieldOrRequest(bool staleRequest)
        {
            var storage = new MemorySaveStorage();
            Guid animal;
            using (var progress = Open(storage))
            {
                animal = Rescue(progress, "ordinary");
                Check(progress, animal);
                Assert.That(progress.TryCheckpoint(), Is.True);
            }
            var raw = new SaveService(storage);
            var section = new SaveSection<JObject>("main.station", 6, () => new JObject(), _ => null);
            raw.Register(section);
            Assert.That(raw.TryOpen(out _, out _), Is.True);
            Assert.That(raw.TryRead(section, out var data, out _), Is.True);
            if (staleRequest)
            {
                var pending = data["Requests"].First(item => (int)item["State"] == (int)RequestState.Available);
                pending["PointId"] = "removed";
                var settled = data["Requests"].Single(item => (int)item["State"] == (int)RequestState.Resolved);
                settled["TemplateId"] = "retired.rescue";
                settled["PointId"] = "retired.location";
            }
            else
                data["Fields"]["Active"] = new JObject { ["oldLayout"] = "removed" };
            Assert.That(raw.TryCommit(writer => writer.Set(section, data), out _), Is.True);
            var originals = storage.Slots.ToArray();
            using (var strict = Open(storage)) Assert.That(strict.CanContinue, Is.False);
            using (var recovered = new MainProgressSession(_config, _content.Artwork, storage, reconcileContent: true))
            {
                Assert.That(recovered.CanContinue, Is.True, recovered.PersistenceError?.ToString());
                Assert.That(recovered.ReconciledContent, Is.True);
                Assert.That(recovered.Session.Outing.Session, Is.Null);
                Assert.That(recovered.Session.Station.Animals.Single().AnimalId, Is.EqualTo(animal));
                Assert.That(storage.Slots, Is.EqualTo(originals));
                Assert.That(recovered.TryCheckpoint(), Is.True, recovered.PersistenceError?.ToString());
                Assert.That(storage.Archives, Is.EquivalentTo(originals));
            }
            using var reopened = Open(storage);
            Assert.That(reopened.Session.Station.Animals.Single().AnimalId, Is.EqualTo(animal));
        }

        [Test]
        public void RealFileSlotsRoundTripCheckedAnimalAndCareWithoutPlayerDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "MainContinuity-" + Guid.NewGuid().ToString("N"));
            try
            {
                Guid cat;
                using (var progress = Open(new FileSaveStorage(directory)))
                {
                    cat = Rescue(progress, "ordinary");
                    Check(progress, cat);
                    Work(progress.Care.Get(cat), false);
                    Assert.That(progress.TryCheckpoint(), Is.True, progress.PersistenceError?.ToString());
                }
                using var restored = Open(new FileSaveStorage(directory));
                Assert.That(restored.CanContinue, Is.True, restored.PersistenceError?.ToString());
                Assert.That(restored.Session.Station.Animals.Single().AnimalId, Is.EqualTo(cat));
                Assert.That(restored.Care.Get(cat).Dirt.RemovedFraction, Is.GreaterThan(0));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private SiegeRescueActivity BeginRescue(MainProgressSession progress, string templateId)
        {
            var session = progress.Session;
            Assert.That(session.TryOpenMap(Guid.NewGuid(), _config.Seed, out var failure), Is.True, failure.ToString());
            var request = session.Requests.Requests.Single(item => item.TemplateId == templateId);
            Assert.That(session.Outing.TrySelectAndDepart(session.Outing.Session.SessionId, request.PointId,
                out failure), Is.True, failure.ToString());
            var activity = new SiegeRescueActivity(session.Outing.Session.SessionId, _config.Seed,
                _content.CreateSiegeCatalog(), _config.GetRescue(templateId));
            Assert.That(session.TryEnterRouteFreeRescue(activity, out failure), Is.True, failure.ToString());
            return activity;
        }

        private Guid Rescue(MainProgressSession progress, string templateId)
        {
            var activity = BeginRescue(progress, templateId);
            var session = progress.Session;
            Assert.That(session.TryConfirmRescue(out _), Is.False, "A live battle is not a victory.");
            for (int i = 0; i < 700 && activity.Battle.Phase != SiegeState.BattlePhase.Won &&
                activity.Battle.Phase != SiegeState.BattlePhase.Timeout; i++)
            {
                if (activity.Battle.Phase == SiegeState.BattlePhase.Decision)
                {
                    var s = activity.Battle.Snapshot;
                    var order = new SiegeOrder { SessionId = s.SessionId, Turn = s.Turn };
                    if (s.Hand.Count > 0 && s.Energy >= 2)
                    { order.DeployCardId = s.Hand[0].Id; order.DeployLaneId = "top"; }
                    Assert.That(activity.Battle.TryOrder(order, true, out var reason, out _), Is.True, reason);
                }
                activity.Battle.Advance(1000);
            }
            Assert.That(activity.Battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Won), templateId);
            activity.SynchronizeOutcome();
            Assert.That(session.TryConfirmRescue(out _), Is.True);
            Assert.That(session.TryReturnRescued(new AnimalIntakeCondition(StationHealthAssessment.Unchecked,
                activity.Individual.Current.Coat.Dirtiness, Array.Empty<string>()), out var failure),
                    Is.True, failure.ToString());
            progress.RecordRescuedReturn(session.Outing.Session.SessionId, session.Outing.Session.AcceptedRequestId);
            Assert.That(session.Station.Animals.Last().MatchesCurrentIndividual(activity.Individual), Is.True);
            return activity.Individual.Current.AnimalId;
        }

        private static void Check(MainProgressSession progress, Guid animalId)
        {
            var session = progress.Session;
            Assert.That(session.Station.TryAssessKnownHealthyAnimal(session.StationSessionId, animalId,
                out _), Is.True);
            if (session.Flow.Stage == Modules.Rescue.RescueStage.Care)
                Assert.That(session.Outing.TryCompleteCare(session.Outing.Session.SessionId, out _), Is.True);
        }

        private static void Work(StationCoatCare care, bool grooming)
        {
            Assert.That(care.TryBegin(grooming, out _), Is.True);
            Stroke(care);
            Assert.That(care.TryEnd(out _, out _), Is.True);
        }
        private static void Stroke(StationCoatCare care)
        {
            for (int y = 2; y < 8; y++)
                for (int x = 2; x < 7; x++) care.TryStroke(x / 10f, y / 10f, 100);
        }
        private static void AssertFields(StationCoatCareState expected, StationCoatCareState actual)
        {
            foreach (var pair in new[] { (expected.CopyDirt(), actual.CopyDirt()), (expected.CopyFur(),
                actual.CopyFur()) })
            {
                Assert.That(pair.Item2.CopySamples(), Is.EqualTo(pair.Item1.CopySamples()));
                Assert.That(pair.Item2.CopyMask(), Is.EqualTo(pair.Item1.CopyMask()));
                Assert.That(pair.Item2.InitialAmount, Is.EqualTo(pair.Item1.InitialAmount));
                Assert.That(pair.Item2.RemainingAmount, Is.EqualTo(pair.Item1.RemainingAmount));
            }
            Assert.That(actual.WashSettledMinutes, Is.EqualTo(expected.WashSettledMinutes));
            Assert.That(actual.GroomSettledMinutes, Is.EqualTo(expected.GroomSettledMinutes));
        }
    }
}
