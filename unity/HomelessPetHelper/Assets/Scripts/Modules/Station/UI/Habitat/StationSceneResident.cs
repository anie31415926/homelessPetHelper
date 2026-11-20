using System;
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
        public bool Observing { get; }

        public StationSceneResident(Guid animalId, int index, string name, Sprite art,
            bool animatedCat, bool observing)
        {
            if (animalId == Guid.Empty || index < 0 || string.IsNullOrWhiteSpace(name) || art == null)
                throw new ArgumentException("Incomplete station scene resident.");
            AnimalId = animalId;
            Index = index;
            Name = name;
            Art = art;
            AnimatedCat = animatedCat;
            Observing = observing;
        }
    }
}
