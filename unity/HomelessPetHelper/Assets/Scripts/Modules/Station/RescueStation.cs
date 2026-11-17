using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HomelessPetHelper.Modules.Station
{
    /// <summary>
    /// Owns temporary admissions on the calling gameplay thread, not UI, treatment or rescue certification.
    /// The session owner must validate the active outing and actual rescue success before admission.
    /// Companion places, adoption, costs and persistence are not implemented here.
    /// </summary>
    public sealed class RescueStation
    {
        private readonly Guid _sessionId;
        private readonly Dictionary<Guid, StationAnimalIntake> _animalsById =
            new Dictionary<Guid, StationAnimalIntake>();
        private readonly HashSet<Guid> _acceptedOutingIds = new HashSet<Guid>();
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

        public bool TryGetAnimal(Guid animalId, out StationAnimalIntake animal)
        {
            return _animalsById.TryGetValue(animalId, out animal);
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
            _animalsById.Add(arrival.AnimalId, arrival);
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
            else if (IsFull)
            {
                failure = StationFailure.TemporaryCapacityFull;
            }

            return failure == StationFailure.None;
        }
    }
}
