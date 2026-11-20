using System;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Modules.Siege;
using UnityEngine;

namespace HomelessPetHelper.Game
{
    [CreateAssetMenu(menuName = "Homeless Pet Helper/Game Content")]
    public sealed class GameContent : ScriptableObject
    {
        [SerializeField] private TextAsset _mapConfig, _mapText, _stationText, _siegeData, _siegeText;
        [SerializeField] private Sprite _cat, _dog;
        [SerializeField] private TextAsset _catMask, _dogMask;
        [SerializeField] private Font _font;
        [SerializeField] private AudioCatalog _audioCatalog;
        public AudioCatalog AudioCatalog => _audioCatalog;
        public TextAsset MapText => _mapText;
        public TextAsset StationText => _stationText;
        public TextAsset SiegeText => _siegeText;
        public Font Font => _font;
        public StationArtwork Artwork => new StationArtwork(_cat, _catMask, _dog, _dogMask);
        public CityMapPreviewConfig CreateMapConfig() => new CityMapPreviewConfig(_mapConfig);
        public SiegeCatalog CreateSiegeCatalog() => _siegeData != null ?
            new SiegeCatalog(JsonUtility.FromJson<SiegeCatalogData>(_siegeData.text)) :
            throw new InvalidOperationException("Missing siege content.");

        public void Validate()
        {
            if (_font == null || _mapText == null || _stationText == null || _siegeText == null)
                throw new InvalidOperationException("Missing game content bindings.");
            Artwork.Get(false, out _);
            Artwork.Get(true, out _);
            CreateMapConfig().ValidateRescues(CreateSiegeCatalog());
            new Modules.Siege.UI.SiegeText(_siegeText).Validate(CreateSiegeCatalog());
        }
    }
}
