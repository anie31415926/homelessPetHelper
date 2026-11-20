using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.WorldTime;

namespace HomelessPetHelper.Modules.Station.Cleaning
{
    /// <summary>Per-animal partial care and time settlement, independent of any panel or rescue activity.</summary>
    public sealed class StationCoatCare
    {
        private readonly RescueStation _station;
        private readonly Guid _stationSessionId;
        private readonly Guid _animalId;
        private readonly ActionWorldClock _clock;
        private readonly CareWorkMeter _wash;
        private readonly CareWorkMeter _groom;
        private readonly AnimalCoatCondition _initial;
        private readonly DirtField _dirt;
        private readonly DirtField _fur;
        private AnimalCoatCondition _expectedCoat;
        private WorldTimeVersion _startedAt;
        private bool _grooming;

        public IReadOnlyDirtField Dirt { get; }
        public IReadOnlyDirtField Fur { get; }
        public bool IsActive { get; private set; }

        public StationCoatCare(RescueStation station, Guid stationSessionId, Guid animalId, ActionWorldClock clock,
            DirtField dirt, DirtField fur, int washMinutes, int groomMinutes)
            : this(station, stationSessionId, animalId, clock,
                new StationCoatCareState(dirt, fur, CurrentCoat(station, animalId), washMinutes,
                    groomMinutes, 0, 0), false) { }

        public StationCoatCare(RescueStation station, Guid stationSessionId, Guid animalId, ActionWorldClock clock,
            StationCoatCareState state) : this(station, stationSessionId, animalId, clock, state, true) { }

        private StationCoatCare(RescueStation station, Guid stationSessionId, Guid animalId, ActionWorldClock clock,
            StationCoatCareState state, bool restoring)
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
            _expectedCoat = animal.Coat;
            if (dirt == null) throw new ArgumentNullException(nameof(dirt));
            if (fur == null) throw new ArgumentNullException(nameof(fur));
            if (dirt.Width != fur.Width || dirt.Height != fur.Height)
                throw new ArgumentException("Coat fields must share dimensions.");
            if ((_initial.Dirtiness == 0) != ((restoring ? dirt.InitialAmount : dirt.RemainingAmount) == 0) ||
                (_initial.LooseFur == 0) != ((restoring ? fur.InitialAmount : fur.RemainingAmount) == 0) ||
                restoring && (animal.Coat.Dirtiness != RemainingCondition(_initial.Dirtiness, dirt) ||
                animal.Coat.LooseFur != RemainingCondition(_initial.LooseFur, fur)))
                throw new ArgumentException("Coat fields must represent the actual individual.");
            // Neither retained constructor inputs nor downcasts may bypass the care session gates.
            _dirt = dirt.Copy();
            _fur = fur.Copy();
            Dirt = _dirt.AsReadOnly();
            Fur = _fur.AsReadOnly();
            _wash = new CareWorkMeter(_dirt, state.WashMinutes, 97, state.WashSettledMinutes);
            _groom = new CareWorkMeter(_fur, state.GroomMinutes, 100, state.GroomSettledMinutes);
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
            return new StationCoatCareState(_dirt, _fur, _initial, _wash.FullDurationMinutes,
                _groom.FullDurationMinutes, _wash.SettledMinutes, _groom.SettledMinutes);
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
            return StationCoatCareFailure.None;
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
            if (!field.ContainsCoat(x, y) || !field.TryErase(x, y, 0.12f, strength, _grooming ? _dirt : null))
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
                RemainingCondition(_initial.Dirtiness, Dirt),
                RemainingCondition(_initial.LooseFur, Fur));
            if (coat.Dirtiness == current.Coat.Dirtiness && coat.LooseFur == current.Coat.LooseFur) return;
            if (!_station.TryApplyCoatCondition(_stationSessionId, current, coat))
                throw new InvalidOperationException("Validated coat effect could not be committed.");
            _expectedCoat = coat;
        }
    }
}
