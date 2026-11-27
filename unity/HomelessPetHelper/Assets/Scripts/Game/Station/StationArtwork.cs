using System;
using HomelessPetHelper.Modules.Station.UI.Affection;
using UnityEngine;

namespace HomelessPetHelper.Game.Station
{
    public sealed class StationArtwork
    {
        public const string AffectionDefinitionId = "dog.doudou";
        private readonly Sprite _cat, _dog;
        private readonly TextAsset _catMask, _dogMask;
        private readonly Sprite _affection;
        private readonly TextAsset _affectionMask;
        public DoudouAffectionView AffectionView { get; }

        public StationArtwork(Sprite cat, TextAsset catMask, Sprite dog, TextAsset dogMask,
            Sprite affection = null, TextAsset affectionMask = null, DoudouAffectionView affectionView = null)
        {
            _cat = cat;
            _catMask = catMask;
            _dog = dog;
            _dogMask = dogMask;
            _affection = affection;
            _affectionMask = affectionMask;
            AffectionView = affectionView;
        }

        public Sprite Get(bool dog, out byte[] mask) => Get(null, dog, out mask);

        public Sprite GetAffectionOverride(string definitionId) =>
            definitionId == AffectionDefinitionId ? _affection : null;

        public Sprite Get(string definitionId, bool dog, out byte[] mask)
        {
            bool affection = definitionId == AffectionDefinitionId;
            Sprite sprite = affection ? _affection : dog ? _dog : _cat;
            TextAsset asset = affection ? _affectionMask : dog ? _dogMask : _catMask;
            if (sprite == null || asset == null || asset.bytes.Length != 128 * 128)
                throw new InvalidOperationException("Missing or incompatible station artwork/mask.");
            mask = asset.bytes;
            return sprite;
        }
    }
}
