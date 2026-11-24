using System;
using System.Collections.Generic;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Modules.Station.Cleaning;

namespace HomelessPetHelper.Game.Station
{
    /// <summary>Owns care across views and outings; the panel only reads and forwards commands.</summary>
    public sealed class StationCareRoster
    {
        private readonly GameLoopSession _session;
        private readonly StationArtwork _artwork;
        private readonly CityMapPreviewConfig _config;
        private readonly Dictionary<Guid, StationCoatCare> _care = new Dictionary<Guid, StationCoatCare>();

        public StationCareRoster(GameLoopSession session, StationArtwork artwork, CityMapPreviewConfig config)
        {
            _session = session;
            _artwork = artwork;
            _config = config;
        }

        public bool UsesDogArtwork(string definitionId) =>
            _config.GetAnimalPresentation(definitionId)?.DogArtwork ??
            throw new ArgumentException("No authored animal presentation.");

        public StationCoatCare Get(Guid animalId)
        {
            if (_care.TryGetValue(animalId, out var care)) return care;
            if (!_session.Station.TryGetCurrentAnimal(animalId, out var animal))
                throw new ArgumentException("Animal is not admitted.");
            var definition = _config.GetAnimalPresentation(animal.DefinitionId) ??
                throw new ArgumentException("Missing care configuration.");
            _artwork.Get(definition.DogArtwork, out var mask);
            int seed = Modules.Animals.UI.AnimalCoatPattern.Seed(animal.AnimalId);
            care = new StationCoatCare(_session.Station, _session.StationSessionId, animalId, _session.Clock,
                StationCoatFields.Create(mask, seed, false, animal.Coat.Dirtiness),
                StationCoatFields.Create(mask, seed, true, animal.Coat.LooseFur),
                    definition.WashMinutes, definition.GroomMinutes);
            _care.Add(animalId, care);
            return care;
        }

        internal void Forget(Guid animalId) => _care.Remove(animalId);

        public void Restore(Guid animalId, StationCoatCareState state)
        {
            if (_care.ContainsKey(animalId)) throw new ArgumentException("Duplicate care state.");
            _care.Add(animalId, new StationCoatCare(_session.Station, _session.StationSessionId, animalId,
                _session.Clock, state));
        }
    }
}
