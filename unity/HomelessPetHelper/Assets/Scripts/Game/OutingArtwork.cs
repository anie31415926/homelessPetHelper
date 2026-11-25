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
        [SerializeField] private Framework.UI.SceneSpritePalette _props;
        [SerializeField] private Sprite _foreground;
        [SerializeField, Tooltip("Normalized background rectangle, with its origin at the lower left.")]
        private Rect _foregroundBounds;
        public Framework.UI.SceneSpritePalette Props => _props;
        public Sprite Foreground => _foreground;
        public Rect ForegroundBounds => _foregroundBounds;
        public string PointId => _pointId;
        public Sprite Background => _background;
    }
}
