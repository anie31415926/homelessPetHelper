using System;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.Cleaning;
using HomelessPetHelper.Modules.WorldTime;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor.StationPreview
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

        internal static StationResidentPresentation CreateSample(int index, OptionPreviewText text)
        {
            Sprite art = LoadArtwork(index == 0 ? "Dog" : "Cat", out var mask);
            var resident = new StationResidentPresentation
            {
                Art = art,
                _sampleDirt = CreateField(mask, index, false, index == 1),
                _sampleFur = CreateField(mask, index, true, false)
            };
            resident.WashWork = new CareWorkMeter(resident.Dirt, 20);
            resident.GroomWork = new CareWorkMeter(resident.Fur, 12, 100);
            resident.Name = text.Get("name" + index);
            resident.Contact = new StationContactSample(index == 1, index != 1, index == 2, index == 0);
            return resident;
        }

        internal static StationResidentPresentation CreateShared(RescueStation station, Guid stationId,
            StationAnimalIntake arrival, ActionWorldClock clock, int index, OptionPreviewText text)
        {
            if (!station.TryGetCurrentAnimal(arrival.AnimalId, out var animal) || animal.Coat == null)
                throw new InvalidOperationException("A shared resident needs explicit animal and coat data.");
            if (animal.DefinitionId != "cat.shy")
                throw new InvalidOperationException("No station presentation for: " + animal.DefinitionId);
            Sprite art = LoadArtwork("Cat", out var mask);
            return new StationResidentPresentation
            {
                Art = art,
                Name = text.Get("loop.animal", index + 1),
                SharedCare = new StationCoatCare(station, stationId, arrival.AnimalId, clock,
                    CreateField(mask, index * 2, false, animal.Coat.Dirtiness == 0),
                    CreateField(mask, index * 2, true, animal.Coat.LooseFur == 0), 20, 12)
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

        private static Sprite LoadArtwork(string species, out byte[] mask)
        {
            string path = StationPreviewAssets.CareArtFolder + "/T_Station_" + species + "Temporary";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path + ".png");
            TextAsset maskAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path + "Mask.bytes");
            if (sprite == null || maskAsset == null)
                throw new InvalidOperationException("Missing station artwork/mask: " + path);
            mask = maskAsset.bytes;
            return sprite;
        }

        private static DirtField CreateField(byte[] mask, int seed, bool fur, bool cleared)
        {
            var samples = new byte[mask.Length];
            if (!cleared)
            {
                for (int y = 0; y < 128; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        if (fur)
                        {
                            float noise = Mathf.PerlinNoise(x * 0.07f + seed * 13, y * 0.07f + 43);
                            samples[y * 128 + x] = (x + y * 2) % 11 < 2 && y % 9 < 6 ?
                                (byte)(Mathf.Clamp01((noise - 0.38f) * 4) * 200) : (byte)0;
                        }
                        else
                        {
                            float noise = Mathf.PerlinNoise(x * 0.10f + seed * 19, y * 0.10f + 7);
                            samples[y * 128 + x] = (byte)(Mathf.Clamp01((noise - 0.43f) * 5) * 220);
                        }
                    }
                }
            }
            return new DirtField(128, 128, mask, samples);
        }
    }
}
