using System;
using UnityEngine;

namespace HomelessPetHelper.Game
{
    /// <summary>Authored location artwork, independent of animal and opening-route rules.</summary>
    [Serializable]
    public sealed class OutingArtwork
    {
        [SerializeField] private string _pointId;
        [SerializeField] private Sprite _background;
        public string PointId => _pointId;
        public Sprite Background => _background;
    }
}
