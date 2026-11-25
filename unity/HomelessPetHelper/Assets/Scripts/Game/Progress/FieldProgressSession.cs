using System;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;

namespace HomelessPetHelper.Game.Progress
{
    /// <summary>Owns durable encounter choices and point results. UI and reopening never regenerate them.</summary>
    public sealed class FieldProgressSession
    {
        private readonly FieldProgressContent _content;
        private readonly CityMap.CityMapPreviewConfig _config;
        private readonly WorldFieldData _data;
        private EncounterData _encounter;
        private ApproachSession _opening;
        private SiegeRescueActivity _activity;
        private ActiveOutingData _pendingRestore;
        internal AnimalIndividual Animal { get; private set; }
        internal bool HasActive => _data.Active != null;
        internal ApproachSession Opening => _opening;
        internal int Seed => _encounter.Seed;
        internal OutingFieldLayout Layout => _content.Layout(_encounter.PointId, _encounter.LayoutId);

        internal FieldProgressSession(WorldFieldData data, FieldProgressContent content,
            CityMap.CityMapPreviewConfig config)
        {
            _data = data ?? Empty();
            _content = content;
            _config = config;
        }

        internal static WorldFieldData Empty() => new WorldFieldData
            { Encounters = Array.Empty<EncounterData>(), Locations = Array.Empty<LocationData>() };

        internal ApproachSession Begin(GameLoopSession session, OutingFieldLayout suggested, bool restoring = false)
        {
            var outing = session.Outing.Session;
            string requestId = outing.AcceptedRequestId.ToString("N");
            var location = _data.Locations.SingleOrDefault(item => item.PointId == outing.SelectedPointId);
            _encounter = _data.Encounters.SingleOrDefault(item => item.RequestId == requestId);
            if (_encounter == null)
            {
                string layoutId = location == null ? suggested?.LayoutId : location.LayoutId;
                var catalog = _content.Opening(outing.SelectedPointId, layoutId);
                var layout = _content.Layout(outing.SelectedPointId, layoutId);
                // The full cleaning room introduces the stairs. A new request can unlock the crate version.
                bool crates = layout?.IntroduceWithCleanup == true && location != null &&
                    location.Mode == (int)ApproachMode.Cleanup &&
                    session.Requests.TryGet(Guid.ParseExact(location.RequestId, "N"), out var previous) &&
                    previous.State == RequestState.Resolved;
                bool fresh = location == null || crates;
                using var initial = layout?.IntroduceWithCleanup == true ? new ApproachSession(outing.SessionId,
                    outing.Seed, catalog.Get(crates ? ApproachMode.Puzzle : ApproachMode.Cleanup), catalog) :
                    catalog.Create(outing.SessionId, outing.Seed);
                session.Requests.TryGet(outing.AcceptedRequestId, out var request);
                _encounter = new EncounterData
                {
                    RequestId = requestId, PointId = outing.SelectedPointId, Seed = outing.Seed,
                    LayoutId = layoutId, ContentVersion = _content.Version(outing.SelectedPointId, layoutId),
                    RuleVersion = FieldProgressContent.EncounterVersion(_config.GetRescue(request.TemplateId),
                        _config.TimePolicy),
                    Mode = fresh ? (int)initial.Template.Mode : location.Mode,
                    TemplateId = fresh ? initial.Template.Id : location.TemplateId,
                    Opening = fresh ? FieldCheckpointCodec.Capture(initial) :
                        CopyForNewEncounter(location.Opening),
                    Animal = MainResidentCodec.CaptureAnimal(
                        _config.GetRescue(request.TemplateId).CreateIndividual().Current)
                };
                _data.Encounters = _data.Encounters.Append(_encounter).ToArray();
            }
            else if (location != null && location.RequestId != requestId)
            {
                // The location is authoritative when another event has worked here since this animal's visit.
                _encounter.LayoutId = location.LayoutId;
                _encounter.ContentVersion = location.ContentVersion;
                _encounter.Mode = location.Mode;
                _encounter.TemplateId = location.TemplateId;
                _encounter.Opening = CopyForNewEncounter(location.Opening);
            }
            if (!restoring && location != null)
                RestoreClutter(location, session.Clock.Current.ElapsedMinutes);
            Animal = MainResidentCodec.RestoreAnimal(_encounter.Animal);
            _opening = CreateOpening(_encounter, outing.SessionId);
            _activity = null;
            _data.Active = new ActiveOutingData
            {
                OutingId = outing.SessionId.ToString("N"), RequestId = requestId, OutingSeed = outing.Seed,
                Stage = 0
            };
            return _opening;
        }

        internal void RestoreActive(GameLoopSession session)
        {
            if (_data.Active == null) return;
            var saved = _data.Active;
            var encounter = _data.Encounters.Single(item => item.RequestId == saved.RequestId);
            if (!session.TryOpenMap(Guid.ParseExact(saved.OutingId, "N"), saved.OutingSeed, out _) ||
                !session.Outing.TrySelectAndDepart(session.Outing.Session.SessionId, encounter.PointId, out _) ||
                session.Outing.Session.AcceptedRequestId.ToString("N") != saved.RequestId)
                throw new ArgumentException("Saved outing no longer has a valid departure.");
            var opening = Begin(session, null, restoring: true);
            if (saved.OpenedRoute ? !session.TryStartOpeningRoute(opening, out _) : !opening.IsComplete)
                throw new ArgumentException("Cannot restore opening ownership.");
            _pendingRestore = saved;
            _data.Active = saved;
            if (saved.Battle != null)
            {
                session.Requests.TryGet(Guid.ParseExact(saved.RequestId, "N"), out var request);
                var activity = new SiegeRescueActivity(session.Outing.Session.SessionId, Seed,
                    _content.Battle(encounter.PointId, encounter.LayoutId, opening), _config.GetRescue(request.TemplateId),
                    Animal);
                bool entered = saved.OpenedRoute
                    ? session.TryEnterRescueAfterOpening(activity, out _)
                    : session.TryEnterRouteFreeRescue(activity, out _);
                if (!entered)
                { activity.Dispose(); throw new ArgumentException("Cannot restore saved battle ownership."); }
                AttachBattle(activity, session);
            }
        }

        internal ApproachSession CreatePresentationOpening(Guid outingId) => CreateOpening(_encounter, outingId);

        internal void AttachBattle(SiegeRescueActivity activity, GameLoopSession session)
        {
            _activity = activity;
            _opening?.Dispose();
            _opening = null;
            if (_pendingRestore?.Battle != null)
            {
                activity.Battle.RestoreReplay(FieldCheckpointCodec.Restore(_pendingRestore.Battle));
                if (_pendingRestore.Stage == 2)
                {
                    activity.SynchronizeOutcome();
                    if (!session.TryConfirmRescue(out _))
                        throw new ArgumentException("Cannot restore confirmed rescue.");
                }
            }
            _pendingRestore = null;
            _data.Active.Stage = session.Flow.Stage == RescueStage.Rescued ? 2 : 1;
        }

        internal WorldFieldData Capture(GameLoopSession session)
        {
            if (_data.Active == null) return _data;
            _data.Active.OpenedRoute = session.Outing.Session.HasOpenedRoute;
            if (_opening != null && !_opening.IsClosed)
                _encounter.Opening = FieldCheckpointCodec.Capture(_opening);
            _encounter.Animal = MainResidentCodec.CaptureAnimal(Animal.Current);
            if (_activity != null)
            {
                _data.Active.Battle = FieldCheckpointCodec.Capture(_activity.Battle);
                _data.Active.Stage = session.Flow.Stage == RescueStage.Rescued ? 2 : 1;
            }
            var location = new LocationData
            {
                RequestId = _encounter.RequestId,
                PointId = _encounter.PointId, LayoutId = _encounter.LayoutId,
                ContentVersion = _encounter.ContentVersion, Mode = _encounter.Mode,
                TemplateId = _encounter.TemplateId, Opening = _encounter.Opening,
                UpdatedMinutes = session.Clock.Current.ElapsedMinutes
            };
            var previous = _data.Locations.SingleOrDefault(item => item.PointId == location.PointId);
            if (previous != null && SameOpening(previous.Opening, location.Opening))
            {
                location.UpdatedMinutes = previous.UpdatedMinutes;
                if (previous.RequestId == location.RequestId) return _data;
            }
            _data.Locations = _data.Locations.Where(item => item.PointId != location.PointId)
                .Append(location).ToArray();
            return _data;
        }

        internal void EndOuting()
        {
            _data.Active = null;
            _opening?.Dispose();
            _opening = null;
            _activity = null;
            _pendingRestore = null;
            _encounter = null;
            Animal = null;
        }

        internal void ApplyArchivedEncounters(EncounterData[] encounters) => _data.Encounters = encounters;

        internal void Dispose() => _opening?.Dispose();

        private ApproachSession CreateOpening(EncounterData encounter, Guid outingId)
        {
            var catalog = _content.Opening(encounter.PointId, encounter.LayoutId);
            var opening = new ApproachSession(outingId, encounter.Seed,
                catalog.Get((ApproachMode)encounter.Mode), catalog);
            opening.RestoreCheckpoint(FieldCheckpointCodec.Restore(encounter.Opening));
            return opening;
        }

        private void RestoreClutter(LocationData location, long minutes)
        {
            var layout = _content.Layout(_encounter.PointId, _encounter.LayoutId);
            if (layout == null || layout.RevisitMinutes <= 0 ||
                minutes - location.UpdatedMinutes < layout.RevisitMinutes ||
                _encounter.Mode != (int)ApproachMode.Cleanup) return;
            var pieces = layout.Opening.Get(ApproachMode.Cleanup).Debris;
            for (int index = 0; index < pieces.Count; index++)
            {
                if (!layout.CanRefill(pieces[index].Cell) || _encounter.Opening.Remaining[index] > 0) continue;
                _encounter.Opening = CopyForNewEncounter(_encounter.Opening);
                _encounter.Opening.Remaining[index] = pieces[index].WorkSeconds;
                return;
            }
        }

        private static OpeningData CopyForNewEncounter(OpeningData opening) => new OpeningData
        {
            Boxes = (int[])opening.Boxes.Clone(), Bridge = opening.Bridge,
            Remaining = (double[])opening.Remaining.Clone(), Undo = Array.Empty<PuzzleUndoData>()
        };

        private static bool SameOpening(OpeningData left, OpeningData right) =>
            left.Bridge == right.Bridge && left.Boxes.SequenceEqual(right.Boxes) &&
                left.Remaining.SequenceEqual(right.Remaining);
    }
}
