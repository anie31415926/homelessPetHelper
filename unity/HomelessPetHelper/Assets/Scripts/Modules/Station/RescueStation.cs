using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Modules.Station
{
    /// <summary>
    /// Owns temporary admissions and initial-care arrangements on the calling gameplay thread.
    /// Does not diagnose, simulate treatment, control UI or certify rescue success.
    /// The session owner must validate the active outing and actual rescue success before admission.
    /// Companion places, adoption, costs and persistence are not implemented here.
    /// </summary>
    public sealed class RescueStation
    {
        private readonly Guid _sessionId;
        private readonly Dictionary<Guid, StationAnimalIntake> _animalsById =
            new Dictionary<Guid, StationAnimalIntake>();
        private readonly HashSet<Guid> _acceptedOutingIds = new HashSet<Guid>();
        private readonly Dictionary<Guid, StationCareRecord> _careByAnimalId =
            new Dictionary<Guid, StationCareRecord>();
        private ReadOnlyCollection<StationAnimalIntake> _animals =
            Array.AsReadOnly(Array.Empty<StationAnimalIntake>());

        public IReadOnlyList<StationAnimalIntake> Animals => _animals;

        public int TemporaryCapacity { get; private set; }

        public int AvailableTemporaryPlaces => TemporaryCapacity - _animals.Count;

        public bool IsFull => AvailableTemporaryPlaces == 0;

        public bool IsSuspended { get; private set; }

        public bool IsClosed { get; private set; }

        public RescueStation(Guid sessionId, int temporaryCapacity)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("A nonempty station session ID is required.", nameof(sessionId));
            }

            if (temporaryCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(temporaryCapacity));
            }

            _sessionId = sessionId;
            TemporaryCapacity = temporaryCapacity;
        }

        public void SetSuspended(bool isSuspended)
        {
            IsSuspended = isSuspended;
        }

        public void Close()
        {
            IsClosed = true;
        }

        internal bool CanModify(Guid sessionId) => ValidateSession(sessionId, out _);

        public bool TryGetAnimal(Guid animalId, out StationAnimalIntake animal)
        {
            return _animalsById.TryGetValue(animalId, out animal);
        }

        public bool TryGetCurrentAnimal(Guid animalId, out AnimalSnapshot animal)
        {
            animal = null;
            if (!_animalsById.TryGetValue(animalId, out var intake) || intake.Individual == null)
            {
                return false;
            }

            animal = intake.Individual.Current;
            return true;
        }

        /// <summary>Applies validated care effects to the admitted individual, never to arrival history.</summary>
        public bool TryApplyCoatCondition(Guid stationSessionId, AnimalSnapshot expected, AnimalCoatCondition coat)
        {
            return ValidateSession(stationSessionId, out _) && expected != null &&
                _animalsById.TryGetValue(expected.AnimalId, out var intake) && intake.Individual != null &&
                intake.Individual.TryApplyCoatCondition(expected, coat);
        }

        /// <summary>
        /// Limited assessment for explicitly known healthy data, not a diagnostic classifier.
        /// Missing individual data or any health problem requires a future assessment provider.
        /// Does not infer health from disposition, admission observations or player-visible hotspots.
        /// </summary>
        public bool TryAssessKnownHealthyAnimal(Guid stationSessionId, Guid animalId, out StationFailure failure)
        {
            if (!TryGetCareRecord(stationSessionId, animalId, out _, out failure))
            {
                return false;
            }

            if (!TryGetCurrentAnimal(animalId, out var animal) || animal.Health.Problems.Count != 0)
            {
                failure = StationFailure.AssessmentSourceUnavailable;
                return false;
            }

            return TryCompleteBasicAssessment(stationSessionId, animalId, Array.Empty<string>(), out failure);
        }

        public bool TryGetCareSnapshot(Guid animalId, out StationCareSnapshot snapshot)
        {
            snapshot = null;
            if (!_careByAnimalId.TryGetValue(animalId, out var record))
            {
                return false;
            }

            snapshot = record.Current;
            return true;
        }

        /// <summary>
        /// Records the assistant's complete initial safety assessment, not a selected observation hotspot.
        /// The assessment caller must supply every necessary problem; an empty list means none were found.
        /// Does not infer diagnoses from history, advance time, clean, or simulate recovery. Refusal changes nothing.
        /// Later reassessments require a separate health/version contract and are not implemented here.
        /// </summary>
        public bool TryCompleteBasicAssessment(Guid stationSessionId, Guid animalId,
            IReadOnlyList<string> necessaryProblemIds, out StationFailure failure)
        {
            return TryGetCareRecord(stationSessionId, animalId, out var record, out failure) &&
                record.TryCompleteBasicAssessment(necessaryProblemIds,
                    TryGetCurrentAnimal(animalId, out var animal) ? animal.Health : null, out failure);
        }

        /// <summary>Records player confirmation only; a request alone does not permit another outing.</summary>
        public bool TryRequestProfessionalCare(Guid stationSessionId, Guid animalId,
            string problemId, out StationFailure failure)
        {
            return TryGetCareRecord(stationSessionId, animalId, out var record, out failure) &&
                record.TryAdvanceProblem(problemId, ProfessionalCareStatus.AwaitingConfirmation,
                    ProfessionalCareStatus.AwaitingSafeHandoff, out failure);
        }

        /// <summary>
        /// Called by the professional-handoff owner after actual safe acceptance, not by closing a panel.
        /// Records arrangement progress only: does not cure the animal, erase problems, or free its place.
        /// </summary>
        public bool TryRecordSafeHandoff(Guid stationSessionId, Guid animalId,
            string problemId, out StationFailure failure)
        {
            return TryGetCareRecord(stationSessionId, animalId, out var record, out failure) &&
                record.TryAdvanceProblem(problemId, ProfessionalCareStatus.AwaitingSafeHandoff,
                    ProfessionalCareStatus.SafelyHandedOver, out failure);
        }

        /// <summary>Read-only initial-care gate; optional grooming and full recovery are not prerequisites.</summary>
        public bool CanCompleteNecessaryCare(Guid stationSessionId, Guid animalId, out StationFailure failure)
        {
            if (!TryGetCareRecord(stationSessionId, animalId, out var record, out failure))
            {
                return false;
            }

            if (!record.Current.NecessaryCareIsArranged)
            {
                failure = record.Current.BasicAssessment == StationHealthAssessment.Unchecked ?
                    StationFailure.BasicAssessmentRequired : StationFailure.NecessaryCarePending;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Records one already-rescued animal per outing and returns a frozen summary for presentation.
        /// Rejection returns null and changes no roster, capacity or accepted IDs. Deduplication is in-memory only.
        /// </summary>
        public bool TryReceive(Guid stationSessionId, StationAnimalIntake arrival,
            out StationReturnSummary summary, out StationFailure failure)
        {
            summary = null;
            if (!CanReceive(stationSessionId, arrival, out failure))
            {
                return false;
            }

            var nextAnimals = new List<StationAnimalIntake>(_animals) { arrival };
            var nextSummary = new StationReturnSummary(arrival, nextAnimals.Count, TemporaryCapacity);
            var care = new StationCareRecord(arrival);
            _animalsById.Add(arrival.AnimalId, arrival);
            _careByAnimalId.Add(arrival.AnimalId, care);
            _acceptedOutingIds.Add(arrival.OutingId);
            _animals = nextAnimals.AsReadOnly();
            summary = nextSummary;
            return true;
        }

        /// <summary>Read-only admission gate. Valid until another operation mutates this station.</summary>
        public bool CanReceive(Guid stationSessionId, StationAnimalIntake arrival, out StationFailure failure)
        {
            return ValidateSession(stationSessionId, out failure) && ValidateArrival(arrival, out failure);
        }

        /// <summary>
        /// Applies an already-approved expansion, using an absolute capacity to avoid duplicate increments.
        /// The owning construction rule must validate unlocks and costs before calling. Rejection changes nothing.
        /// </summary>
        public bool TryExpandTemporaryCapacity(Guid stationSessionId, int newCapacity, out StationFailure failure)
        {
            if (!ValidateSession(stationSessionId, out failure))
            {
                return false;
            }

            if (newCapacity < 0)
            {
                failure = StationFailure.InvalidCapacity;
                return false;
            }

            if (newCapacity <= TemporaryCapacity)
            {
                failure = StationFailure.CapacityNotIncreased;
                return false;
            }

            TemporaryCapacity = newCapacity;
            return true;
        }

        private bool TryGetCareRecord(Guid stationSessionId, Guid animalId,
            out StationCareRecord record, out StationFailure failure)
        {
            record = null;
            if (!ValidateSession(stationSessionId, out failure))
            {
                return false;
            }

            if (!_careByAnimalId.TryGetValue(animalId, out record))
            {
                failure = StationFailure.AnimalNotFound;
                return false;
            }

            return true;
        }

        private bool ValidateSession(Guid stationSessionId, out StationFailure failure)
        {
            failure = StationFailure.None;
            if (IsClosed)
            {
                failure = StationFailure.Closed;
            }
            else if (IsSuspended)
            {
                failure = StationFailure.Suspended;
            }
            else if (stationSessionId != _sessionId)
            {
                failure = StationFailure.WrongSession;
            }

            return failure == StationFailure.None;
        }

        private bool ValidateArrival(StationAnimalIntake arrival, out StationFailure failure)
        {
            failure = StationFailure.None;
            if (arrival == null)
            {
                failure = StationFailure.InvalidArrival;
            }
            else if (_acceptedOutingIds.Contains(arrival.OutingId))
            {
                failure = StationFailure.DuplicateOuting;
            }
            else if (_animalsById.ContainsKey(arrival.AnimalId))
            {
                failure = StationFailure.DuplicateAnimal;
            }
            else if (arrival.AnimalAtArrival != null && !arrival.MatchesCurrentIndividual(arrival.Individual))
            {
                failure = StationFailure.InvalidArrival;
            }
            else if (IsFull)
            {
                failure = StationFailure.TemporaryCapacityFull;
            }

            return failure == StationFailure.None;
        }
    }
}
