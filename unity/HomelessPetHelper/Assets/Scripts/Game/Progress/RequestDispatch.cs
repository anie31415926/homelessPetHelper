using System;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Modules.Outing;

namespace HomelessPetHelper.Game.Progress
{
    internal sealed class RequestDispatch
    {
        private readonly CityMapPreviewConfigData.RequestDispatch _policy;
        private readonly CityMapPreviewConfigData.Request[] _candidates;
        private readonly GameLoopSession _session;
        private RequestDispatchData _data;

        internal RequestDispatch(CityMapPreviewConfig config, FieldProgressContent fields, GameLoopSession session,
            RequestDispatchData data)
        {
            _policy = config.Dispatch;
            _candidates = _policy?.candidates
                .Where(entry => fields?.HasPlayableLocation(entry.pointId) == true).ToArray();
            _session = session;
            _data = data;
        }

        internal RequestDispatchData Capture() => _data;

        internal HelpRequest UnshownNotice
        {
            get
            {
                if (_data?.Shown != false) return null;
                var request = Current;
                return request?.State == RequestState.Available || request?.State == RequestState.HelpedElsewhere
                    ? request : null;
            }
        }

        private HelpRequest Current => _data != null && Guid.TryParseExact(_data.NoticeId, "N", out var id) &&
            _session.Requests.TryGet(id, out var request) ? request : null;

        internal void MarkShown() => _data.Shown = true;

        internal void Refresh(bool outingActive)
        {
            if (_candidates == null || _candidates.Length == 0) return;
            long now = _session.Clock.Current.ElapsedMinutes;
            int spaces = _session.Station.AvailableTemporaryPlaces;
            _data ??= new RequestDispatchData { ObservedMinute = now, NextMinute = After(now, Interval(spaces)) };
            var notice = Current;
            if (outingActive)
            {
                if (notice?.State == RequestState.Accepted) _data.Protected = true;
                return;
            }

            CheckExpiry(notice, now);
            if (now > _data.ObservedMinute && now >= _data.NextMinute && notice?.State != RequestState.Available &&
                UnshownNotice == null && TryCreate(out var created))
            {
                _data.NoticeId = created.RequestId.ToString("N");
                _data.Shown = false;
                _data.NextMinute = After(now, Interval(spaces));
                _data.ExpiresMinute = After(now, _policy.lifetimeMinutes);
            }
            _data.ObservedMinute = now;
            if (spaces > 0 && !_candidates.Any(candidate =>
                _session.Map.TrySelectPoint(candidate.pointId, out var request, out _) && request.ReunionPlan == null))
                TryCreate(out _);
        }

        private void CheckExpiry(HelpRequest notice, long now)
        {
            if (_data.Protected && notice?.State == RequestState.Available)
                _data.ExpiresMinute = After(now, _policy.lifetimeMinutes);
            _data.Protected = false;
            if (notice?.State == RequestState.Available && now >= _data.ExpiresMinute)
                _session.Requests.TryTransition(notice.RequestId, RequestSignal.HelpedElsewhere, out _);
        }

        private bool TryCreate(out HelpRequest request)
        {
            request = null;
            var requests = _session.Requests.Requests;
            var points = _session.Map.BuildSnapshot().Points;
            var eligible = _candidates.Where(candidate =>
                points.Any(point => point.PointId == candidate.pointId &&
                    point.DisplayState == PointDisplayState.NoRequest) &&
                !requests.Any(item => item.PointId == candidate.pointId && item.State == RequestState.Accepted));
            var selected = eligible.OrderBy(candidate => LastUse(candidate.pointId)).FirstOrDefault();
            if (selected == null) return false;
            request = new HelpRequest(Guid.NewGuid(), selected.templateId, selected.pointId, selected.templateVersion);
            if (!_session.Requests.TryAdd(request, out _))
                throw new InvalidOperationException("Cannot add scheduled request.");
            return true;
        }

        private int LastUse(string pointId)
        {
            var requests = _session.Requests.Requests;
            for (int index = requests.Count - 1; index >= 0; index--)
                if (requests[index].PointId == pointId) return index;
            return -1;
        }

        private int Interval(int spaces) => spaces == 0 ? _policy.fullIntervalMinutes :
            spaces < _policy.highCapacityThreshold ? _policy.lowIntervalMinutes : _policy.highIntervalMinutes;

        private static long After(long minute, int delta) => Math.Min(minute, long.MaxValue - delta) + delta;

        internal static void Validate(RequestDispatchData data, GameLoopSession session, WorldFieldData fields)
        {
            if (data == null) return;
            if (data.ObservedMinute < 0 || data.ObservedMinute > session.Clock.Current.ElapsedMinutes ||
                data.NextMinute <= 0 || data.ExpiresMinute < 0 ||
                (data.NoticeId == null ? data.Shown || data.Protected || data.ExpiresMinute != 0 :
                    !Guid.TryParseExact(data.NoticeId, "N", out var id) ||
                    !session.Requests.TryGet(id, out var request) || request.ReunionPlan != null ||
                    data.ExpiresMinute == 0 || data.Protected != (fields?.Active?.RequestId == data.NoticeId)))
                throw new ArgumentException("Invalid saved request dispatch state.");
        }
    }
}
