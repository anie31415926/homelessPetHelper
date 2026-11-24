using System;
using HomelessPetHelper.Modules.Animals;
using UnityEngine;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    public sealed class StationSceneResident
    {
        public Guid AnimalId { get; }
        public int Index { get; }
        public string Name { get; }
        public Sprite Art { get; }
        public bool AnimatedCat { get; }
        public bool HasAnimation => AnimatedCat || Appearance != null;
        public bool Observing { get; }
        public AnimalSnapshot Snapshot { get; }
        public StationHabitatRenderer Appearance { get; }

        public StationSceneResident(Guid animalId, int index, string name, Sprite art,
            bool animatedCat, bool observing, AnimalSnapshot snapshot = null, StationHabitatRenderer appearance = null)
        {
            if (animalId == Guid.Empty || index < 0 || string.IsNullOrWhiteSpace(name) || art == null)
                throw new ArgumentException("Incomplete station scene resident.");
            if (snapshot != null && snapshot.AnimalId != animalId)
                throw new ArgumentException("Resident snapshot identity mismatch.");
            Appearance = appearance;
            Snapshot = snapshot;
            AnimalId = animalId;
            Index = index;
            Name = name;
            Art = art;
            AnimatedCat = animatedCat;
            Observing = observing;
        }
    }
}
