using System;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.Cleaning;
using HomelessPetHelper.Modules.WorldTime;
using UnityEngine;

namespace HomelessPetHelper.Game.Station
{
    /// <summary>Per-resident view data. Textures belong to views; business effects belong to care.</summary>
    internal sealed class StationResidentPresentation
    {
        private DirtField _sampleDirt;
        private DirtField _sampleFur;

        internal string Name { get; private set; }
        internal Sprite Art { get; private set; }
        internal IReadOnlyDirtField Dirt => SharedCare != null ? SharedCare.Dirt : _sampleDirt;
        internal IReadOnlyDirtField Fur => SharedCare != null ? SharedCare.Fur : _sampleFur;
        internal CareWorkMeter WashWork { get; private set; }
        internal CareWorkMeter GroomWork { get; private set; }
        internal StationContactSample Contact { get; private set; }
        internal StationCoatCare SharedCare { get; private set; }

        internal static StationResidentPresentation CreateSample(int index, OptionPreviewText text,
            StationArtwork artwork)
        {
            Sprite art = artwork.Get(index == 0, out var mask);
            var resident = new StationResidentPresentation
            {
                Art = art,
                _sampleDirt = StationCoatFields.Create(mask, index, false, index == 1),
                _sampleFur = StationCoatFields.Create(mask, index, true, false)
            };
            resident.WashWork = new CareWorkMeter(resident.Dirt, 20);
            resident.GroomWork = new CareWorkMeter(resident.Fur, 12, 100);
            resident.Name = text.Get("name" + index);
            resident.Contact = new StationContactSample(index == 1, index != 1, index == 2, index == 0);
            return resident;
        }

        internal static StationResidentPresentation CreateShared(RescueStation station, Guid stationId,
            StationAnimalIntake arrival, ActionWorldClock clock, int index, OptionPreviewText text,
            StationArtwork artwork, StationCoatCare sharedCare = null, bool dogArtwork = false)
        {
            if (!station.TryGetCurrentAnimal(arrival.AnimalId, out var animal) || animal.Coat == null)
                throw new InvalidOperationException("A shared resident needs explicit animal and coat data.");
            Sprite art = artwork.Get(dogArtwork, out var mask);
            return new StationResidentPresentation
            {
                Art = art,
                Name = text.Get(dogArtwork ? "loop.dog" : "loop.animal", index + 1),
                SharedCare = sharedCare ?? new StationCoatCare(station, stationId, arrival.AnimalId, clock,
                    StationCoatFields.Create(mask, Modules.Animals.UI.AnimalCoatPattern.Seed(animal.AnimalId),
                        false, animal.Coat.Dirtiness),
                    StationCoatFields.Create(mask, Modules.Animals.UI.AnimalCoatPattern.Seed(animal.AnimalId),
                        true, animal.Coat.LooseFur), 20, 12)
            };
        }

        internal bool TrySampleStroke(bool grooming, float x, float y, int strength)
        {
            if (SharedCare != null) throw new InvalidOperationException("Shared care owns its own field mutations.");
            return (grooming ? _sampleFur : _sampleDirt).TryErase(x, y, 0.12f, strength,
                grooming ? _sampleDirt : null);
        }

        internal void ClearSampleDirtResidue()
        {
            if (SharedCare != null) throw new InvalidOperationException("Shared care owns its own field mutations.");
            _sampleDirt.ClearResidue();
        }

    }
}
