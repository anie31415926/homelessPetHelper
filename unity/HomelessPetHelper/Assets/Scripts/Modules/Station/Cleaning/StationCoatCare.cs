using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.WorldTime;

namespace HomelessPetHelper.Modules.Station.Cleaning
{
    /// <summary>Per-animal partial care and time settlement, independent of any panel or rescue activity.</summary>
    public sealed class StationCoatCare
    {
        private const int StepsPerDay = 4;
        private const int ConditionPerStep = 5;
        private const int MinimumConditionCap = ConditionPerStep * StepsPerDay;

        private readonly RescueStation _station;
        private readonly Guid _stationSessionId;
        private readonly Guid _animalId;
        private readonly ActionWorldClock _clock;
        private CareWorkMeter _wash;
        private CareWorkMeter _groom;
        private readonly AnimalCoatCondition _initial;
        private readonly int _washMinutes, _groomMinutes;
        private int? _dirtCycleBaseline, _furCycleBaseline;
        private int DirtBaseline => _dirtCycleBaseline ?? _initial.Dirtiness;
        private int FurBaseline => _furCycleBaseline ?? _initial.LooseFur;
        private long _dirtGrowthMinute, _furGrowthMinute;
        private DirtField _dirt, _fur;
        private readonly Func<bool, int, DirtField> _createField;
        private long? _lastWashCompletedMinute, _lastGroomCompletedMinute;
        private long _earlierCyclesMinutes;
        private AnimalCoatCondition _expectedCoat;
        private WorldTimeVersion _startedAt;
        private bool _grooming;

        public IReadOnlyDirtField Dirt { get; private set; }
        public IReadOnlyDirtField Fur { get; private set; }
        // One day of ordinary buildup must still allow grooming as soon as its cooldown ends.
        public IReadOnlyDirtField GroomingBlocker => _createField != null &&
            _expectedCoat.Dirtiness <= MinimumConditionCap ? null : Dirt;
        public bool IsActive { get; private set; }

        public StationCoatCare(RescueStation station, Guid stationSessionId, Guid animalId, ActionWorldClock clock,
            DirtField dirt, DirtField fur, int washMinutes, int groomMinutes,
            Func<bool, int, DirtField> createField = null)
            : this(station, stationSessionId, animalId, clock,
                new StationCoatCareState(dirt, fur, CurrentCoat(station, animalId), washMinutes,
                    groomMinutes, 0, 0), false, createField) { }

        public StationCoatCare(RescueStation station, Guid stationSessionId, Guid animalId, ActionWorldClock clock,
            StationCoatCareState state, Func<bool, int, DirtField> createField = null)
            : this(station, stationSessionId, animalId, clock, state, true, createField) { }

        private StationCoatCare(RescueStation station, Guid stationSessionId, Guid animalId, ActionWorldClock clock,
            StationCoatCareState state, bool restoring, Func<bool, int, DirtField> createField)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var dirt = state.CopyDirt();
            var fur = state.CopyFur();
            _station = station ?? throw new ArgumentNullException(nameof(station));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _stationSessionId = stationSessionId;
            _animalId = animalId;
            if (!station.TryGetCurrentAnimal(animalId, out var animal) || animal.Coat == null)
                throw new ArgumentException("Coat care needs an admitted individual with explicit coat data.");
            _initial = state.InitialCoat;
            _createField = createField;
            if (state.WashMinutes is < 1 or > 1440 || state.GroomMinutes is < 1 or > 1440)
                throw new ArgumentException("Invalid care duration.");
            _washMinutes = state.WashMinutes;
            _groomMinutes = state.GroomMinutes;
            _dirtCycleBaseline = state.DirtBaseline;
            _furCycleBaseline = state.FurBaseline;
            _lastWashCompletedMinute = state.LastWashCompletedMinute;
            _lastGroomCompletedMinute = state.LastGroomCompletedMinute;
            _earlierCyclesMinutes = state.EarlierCyclesMinutes;
            long now = _clock.Current.ElapsedMinutes;
            if (_earlierCyclesMinutes < 0 || _lastWashCompletedMinute is < 0 || _lastWashCompletedMinute > now ||
                _lastGroomCompletedMinute is < 0 || _lastGroomCompletedMinute > now ||
                state.DirtGrowthMinute is < 0 || state.DirtGrowthMinute > now ||
                state.FurGrowthMinute is < 0 || state.FurGrowthMinute > now)
                throw new ArgumentException("Invalid care time.");
            // Older saves know the last completion, but have no growth anchor for partly worked coats.
            _dirtGrowthMinute = state.DirtGrowthMinute ??
                (dirt.RemainingAmount == 0 ? _lastWashCompletedMinute : null) ?? now;
            _furGrowthMinute = state.FurGrowthMinute ??
                (fur.RemainingAmount == 0 ? _lastGroomCompletedMinute : null) ?? now;
            _expectedCoat = animal.Coat;
            if (dirt.Width != fur.Width || dirt.Height != fur.Height)
                throw new ArgumentException("Coat fields must share dimensions.");
            if (DirtBaseline < 0 || DirtBaseline > ConditionCap(false) ||
                FurBaseline < 0 || FurBaseline > ConditionCap(true) ||
                (DirtBaseline == 0) != ((restoring ? dirt.InitialAmount : dirt.RemainingAmount) == 0) ||
                (FurBaseline == 0) != ((restoring ? fur.InitialAmount : fur.RemainingAmount) == 0) ||
                restoring && (animal.Coat.Dirtiness != RemainingCondition(DirtBaseline, dirt) ||
                animal.Coat.LooseFur != RemainingCondition(FurBaseline, fur)))
                throw new ArgumentException("Coat fields must represent the actual individual.");
            // Neither retained constructor inputs nor downcasts may bypass the care session gates.
            _dirt = dirt.Copy();
            _fur = fur.Copy();
            Dirt = _dirt.AsReadOnly();
            Fur = _fur.AsReadOnly();
            _wash = new CareWorkMeter(_dirt, CycleDuration(false, state.DirtBaseline), 97, state.WashSettledMinutes);
            _groom = new CareWorkMeter(_fur, CycleDuration(true, state.FurBaseline), 100, state.GroomSettledMinutes);
            if (restoring && (_wash.PendingMinutes != 0 || _groom.PendingMinutes != 0))
                throw new ArgumentException("Stable care must have settled all whole minutes.");
        }

        private static AnimalCoatCondition CurrentCoat(RescueStation station, Guid animalId)
        {
            if (station == null || !station.TryGetCurrentAnimal(animalId, out var animal) || animal.Coat == null)
                throw new ArgumentException("Missing admitted coat.");
            return animal.Coat;
        }

        public StationCoatCareState Capture()
        {
            if (IsActive) throw new InvalidOperationException("End care deliberately before capturing a checkpoint.");
            return new StationCoatCareState(_dirt, _fur, _initial, _washMinutes,
                _groomMinutes, _wash.SettledMinutes, _groom.SettledMinutes,
                _lastWashCompletedMinute, _lastGroomCompletedMinute, _earlierCyclesMinutes,
                _dirtGrowthMinute, _furGrowthMinute, _dirtCycleBaseline, _furCycleBaseline);
        }

        /// <summary>Action eligibility also applies to switching tools after explicitly ending active care.</summary>
        public bool CanBegin(bool grooming) => GetBeginFailure(grooming) == StationCoatCareFailure.None;

        public StationCoatCareFailure GetBeginFailure(bool grooming)
        {
            StationCoatCareFailure failure = GetSessionFailure(out var animal);
            if (failure != StationCoatCareFailure.None) return failure;
            if (!_station.TryGetCareSnapshot(_animalId, out var care))
                return StationCoatCareFailure.AnimalUnavailable;
            if (care.BasicAssessment == StationHealthAssessment.Unchecked)
                return StationCoatCareFailure.AssessmentRequired;
            if (care.BasicAssessment != StationHealthAssessment.NoUrgentCareRequired ||
                animal.Health.Problems.Count > 0)
                return StationCoatCareFailure.HealthCareRequired;
            if (animal.Disposition.Stress != AnimalStress.None ||
                animal.Disposition.Emotion != AnimalEmotion.Relaxed)
                return StationCoatCareFailure.AnimalNeedsSpace;
            if (grooming && !animal.CarePermissions.AcceptsGroom) return StationCoatCareFailure.GroomNotAccepted;
            if (!grooming && !animal.CarePermissions.AcceptsWash) return StationCoatCareFailure.WashNotAccepted;
            long? completedMinute = grooming ? _lastGroomCompletedMinute : _lastWashCompletedMinute;
            if (IsCoolingDown(_lastWashCompletedMinute) || (grooming && IsCoolingDown(completedMinute)) ||
                ((_createField != null || completedMinute.HasValue) &&
                    (grooming ? Fur : Dirt).RemainingAmount == 0))
                return grooming ? StationCoatCareFailure.AlreadyTidy : StationCoatCareFailure.AlreadyClean;
            return StationCoatCareFailure.None;
        }

        private bool IsCoolingDown(long? completedMinute)
        {
            WorldTimeSnapshot now = _clock.Current;
            return completedMinute.HasValue && now.ElapsedMinutes - completedMinute.Value < now.MinutesPerDay;
        }

        private int ConditionCap(bool grooming) =>
            Math.Max(MinimumConditionCap, grooming ? _initial.LooseFur : _initial.Dirtiness);

        private int CycleDuration(bool grooming, int? baseline)
        {
            int authoredMinutes = grooming ? _groomMinutes : _washMinutes;
            int cap = ConditionCap(grooming);
            return baseline.HasValue ?
                Math.Max(1, (authoredMinutes * baseline.Value + cap - 1) / cap) : authoredMinutes;
        }

        /// <summary>Applies elapsed game time at stable boundaries, leaving open care sessions untouched.</summary>
        public void RefreshAccumulation()
        {
            if (_createField == null || IsActive || GetSessionFailure(out _) != StationCoatCareFailure.None) return;
            long now = _clock.Current.ElapsedMinutes;
            int interval = Math.Max(1, _clock.Current.MinutesPerDay / StepsPerDay);
            bool changed = Accumulate(false, now, interval, ref _dirtGrowthMinute);
            changed |= Accumulate(true, now, interval, ref _furGrowthMinute);
            if (changed) UpdateIndividual();
        }

        private bool Accumulate(bool grooming, long now, int interval, ref long growthMinute)
        {
            long steps = (now - growthMinute) / interval;
            if (steps == 0) return false;
            growthMinute += steps * interval;
            int cap = ConditionCap(grooming);
            var current = grooming ? _fur : _dirt;
            int condition = RemainingCondition(grooming ? FurBaseline : DirtBaseline, current);
            int target = Math.Min(cap, condition + (int)Math.Min(steps, 100 / ConditionPerStep) * ConditionPerStep);
            if (target <= condition) return false;

            var field = current.WithAccumulation(_createField(grooming, 100), condition, target, cap);
            if (field == null) return false;
            var previous = grooming ? _groom : _wash;
            _earlierCyclesMinutes = checked(_earlierCyclesMinutes + previous.SettledMinutes);
            var work = new CareWorkMeter(field, CycleDuration(grooming, target), grooming ? 100 : 97);
            if (grooming)
            {
                _fur = field;
                Fur = field.AsReadOnly();
                _groom = work;
                _furCycleBaseline = target;
            }
            else
            {
                _dirt = field;
                Dirt = field.AsReadOnly();
                _wash = work;
                _dirtCycleBaseline = target;
            }
            return true;
        }

        /// <summary>Captures time only on success; refusal leaves the existing care session untouched.</summary>
        public bool TryBegin(bool grooming, out StationCoatCareFailure failure)
        {
            failure = IsActive ? StationCoatCareFailure.AlreadyActive : GetBeginFailure(grooming);
            if (failure != StationCoatCareFailure.None) return false;
            _grooming = grooming;
            _startedAt = _clock.Current.Version;
            IsActive = true;
            return true;
        }

        public bool TryStroke(float x, float y, int strength)
        {
            if (!IsActive || !CanBegin(_grooming)) return false;
            DirtField field = _grooming ? _fur : _dirt;
            if (!field.ContainsCoat(x, y) || !field.TryErase(x, y, 0.12f, strength, _grooming ? GroomingBlocker : null))
                return false;
            UpdateIndividual();
            return true;
        }

        /// <summary>
        /// Only deliberate ending commits time. Refusal returns zero minutes and preserves active work;
        /// callers must not replace the captured time version just to make a retry succeed.
        /// </summary>
        public bool TryEnd(out long minutes, out StationCoatCareFailure failure)
        {
            minutes = 0;
            failure = !IsActive ? StationCoatCareFailure.NotActive : GetSessionFailure(out _);
            if (failure != StationCoatCareFailure.None) return false;
            CareWorkMeter work = _grooming ? _groom : _wash;
            long pending = work.PendingMinutes;
            if (pending > 0 && !_clock.TryAdvance(_startedAt, pending, out _, out var timeFailure))
            {
                failure = timeFailure == WorldTimeFailure.StaleVersion ?
                    StationCoatCareFailure.TimeVersionStale : StationCoatCareFailure.TimeRejected;
                return false;
            }
            work.ConfirmSettlement();
            if (!_grooming && Dirt.RemovedFraction >= 0.97)
            {
                _dirt.ClearResidue();
                work.ConfirmSettlement();
            }
            long completedMinute = _clock.Current.ElapsedMinutes;
            if (_grooming && Fur.InitialAmount > 0 && Fur.RemainingAmount == 0)
            {
                _lastGroomCompletedMinute = completedMinute;
                _furGrowthMinute = completedMinute;
            }
            if (!_grooming && Dirt.InitialAmount > 0 && Dirt.RemainingAmount == 0)
            {
                _lastWashCompletedMinute = completedMinute;
                _dirtGrowthMinute = completedMinute;
            }
            UpdateIndividual();
            IsActive = false;
            minutes = pending;
            return true;
        }

        private StationCoatCareFailure GetSessionFailure(out AnimalSnapshot animal)
        {
            animal = null;
            if (_clock.IsSuspended || _station.IsSuspended) return StationCoatCareFailure.Suspended;
            if (_clock.IsClosed || !_station.CanModify(_stationSessionId))
                return StationCoatCareFailure.SessionUnavailable;
            if (!_station.TryGetCurrentAnimal(_animalId, out animal)) return StationCoatCareFailure.AnimalUnavailable;
            return ReferenceEquals(animal.Coat, _expectedCoat) ?
                StationCoatCareFailure.None : StationCoatCareFailure.CoatChanged;
        }

        private static int RemainingCondition(int initial, IReadOnlyDirtField field) =>
            (int)Math.Ceiling(initial * (1 - field.RemovedFraction));

        private void UpdateIndividual()
        {
            _station.TryGetCurrentAnimal(_animalId, out var current);
            var coat = new AnimalCoatCondition(
                RemainingCondition(DirtBaseline, Dirt),
                RemainingCondition(FurBaseline, Fur));
            if (coat.Dirtiness == current.Coat.Dirtiness && coat.LooseFur == current.Coat.LooseFur) return;
            if (!_station.TryApplyCoatCondition(_stationSessionId, current, coat))
                throw new InvalidOperationException("Validated coat effect could not be committed.");
            _expectedCoat = coat;
        }
    }
}
