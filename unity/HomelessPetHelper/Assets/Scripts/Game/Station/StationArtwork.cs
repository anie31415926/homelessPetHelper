using System;
using UnityEngine;

namespace HomelessPetHelper.Game.Station
{
    public sealed class StationArtwork
    {
        private readonly Sprite _cat, _dog;
        private readonly TextAsset _catMask, _dogMask;

        public StationArtwork(Sprite cat, TextAsset catMask, Sprite dog, TextAsset dogMask)
        {
            _cat = cat;
            _catMask = catMask;
            _dog = dog;
            _dogMask = dogMask;
        }

        public Sprite Get(bool dog, out byte[] mask)
        {
            Sprite sprite = dog ? _dog : _cat;
            TextAsset asset = dog ? _dogMask : _catMask;
            if (sprite == null || asset == null || asset.bytes.Length != 128 * 128)
                throw new InvalidOperationException("Missing or incompatible station artwork/mask.");
            mask = asset.bytes;
            return sprite;
        }
    }
}
