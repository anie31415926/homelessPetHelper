using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Approach;
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
        [SerializeField] private OutingArtwork[] _outingArtwork = Array.Empty<OutingArtwork>();
        [SerializeField] private AudioCatalog _audioCatalog;
        [SerializeField] private AudioCatalog _musicCatalog;
        [SerializeField] private TextAsset _approachConfig, _approachText;
        [SerializeField] private TextAsset[] _fieldConfigs = Array.Empty<TextAsset>();
        [SerializeField] private TextAsset _stationEconomy, _economyLoadout;
        internal Progress.StationEconomyRules CreateEconomy() => _stationEconomy == null ? null :
            Progress.StationEconomyRules.Load(_stationEconomy, _economyLoadout);
        [SerializeField] private TextAsset _levelUiText, _levelGuidance;
        public Text.OptionPreviewText CreateLevelUiText() => new Text.OptionPreviewText(_levelUiText);
        public Guidance.LevelGuideCatalog CreateGuides() =>
            new Guidance.LevelGuideCatalog(_levelGuidance, CreateLevelUiText());
        public Field.OutingFieldCatalog CreateFields() => new Field.OutingFieldCatalog(_fieldConfigs,
            CreateMapConfig().Definition.Points.Select(point => point.PointId));
        public SiegeCatalog CreateFieldSiegeCatalog(Field.OutingFieldLayout field) =>
            field.CreateBattleCatalog(_siegeData);
        public SiegeCatalog CreateFieldSiegeCatalog(Field.OutingFieldLayout field, ApproachSession opening) =>
            field.CreateBattleCatalog(_siegeData, opening);
        internal SiegeCatalog CreateFieldSiegeCatalog(Field.OutingFieldLayout field, ApproachSession opening,
            Progress.StationEconomyRules clip) => field.CreateBattleCatalog(_siegeData, opening,
                clip == null ? null : new Action<SiegeCatalogData>(clip.ApplyClip));
        public Progress.FieldProgressContent CreateFieldProgressContent(Field.OutingFieldCatalog fields) =>
            new Progress.FieldProgressContent(this, fields, _siegeData.text, _approachConfig.text);
        public TextAsset ApproachText => _approachText;
        public ApproachCatalog CreateApproachCatalog() => _approachConfig != null ?
            new ApproachCatalog(JsonUtility.FromJson<ApproachCatalogData>(_approachConfig.text)) :
            throw new InvalidOperationException("Missing approach content.");
        public AudioCatalog AudioCatalog => _audioCatalog;
        public AudioCatalog MusicCatalog => _musicCatalog;
        public TextAsset MapText => _mapText;
        public TextAsset StationText => _stationText;
        public TextAsset SiegeText => _siegeText;
        public Font Font => _font;
        public StationArtwork Artwork => new StationArtwork(_cat, _catMask, _dog, _dogMask);
        public CityMapPreviewConfig CreateMapConfig() => new CityMapPreviewConfig(_mapConfig);
        public SiegeCatalog CreateSiegeCatalog() => CreateSiegeCatalog(null);
        internal SiegeCatalog CreateSiegeCatalog(Progress.StationEconomyRules clip) =>
            new SiegeCatalog(CreateSiegeData(clip));

        private SiegeCatalogData CreateSiegeData(Progress.StationEconomyRules clip)
        {
            if (_siegeData == null) throw new InvalidOperationException("Missing siege content.");
            var data = JsonUtility.FromJson<SiegeCatalogData>(_siegeData.text);
            clip?.ApplyClip(data);
            return data;
        }

        public OutingArtwork GetOutingArtwork(string pointId)
        {
            // Only the two authored scenes are kept; every other point falls back to the first
            // (parking) so entering a location always shows a real field instead of an empty map.
            OutingArtwork fallback = null;
            foreach (OutingArtwork item in _outingArtwork)
            {
                if (item == null || item.Background == null) continue;
                if (fallback == null) fallback = item;
                if (item.PointId == pointId) return item;
            }
            if (fallback == null) throw new ArgumentException("Missing outing artwork: " + pointId, nameof(pointId));
            return fallback;
        }

        public Sprite GetOutingBackground(string pointId) => GetOutingArtwork(pointId).Background;

        public Framework.UI.SceneSpritePalette GetOutingProps(string pointId) =>
            _outingArtwork.FirstOrDefault(item => item.PointId == pointId)?.Props;

        private void ValidateArtwork(CityMapPreviewConfig map)
        {
            if (_outingArtwork == null) throw new InvalidOperationException("Missing outing artwork bindings.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (OutingArtwork item in _outingArtwork)
                if (item == null || string.IsNullOrWhiteSpace(item.PointId) || item.PointId != item.PointId.Trim() ||
                    !ids.Add(item.PointId) || item.Background == null ||
                    !map.Definition.TryGetPoint(item.PointId, out _))
                    throw new InvalidOperationException("Invalid or duplicate outing artwork binding.");
            if (ids.Count == 0) throw new InvalidOperationException("Missing outing artwork fallback.");
        }

        public void Validate()
        {
            if (_font == null || _mapText == null || _stationText == null || _siegeText == null)
                throw new InvalidOperationException("Missing game content bindings.");
            Artwork.Get(false, out _);
            Artwork.Get(true, out _);
            var map = CreateMapConfig();
            ValidateArtwork(map);
            map.ValidateText(new Text.OptionPreviewText(_mapText));
            map.ValidateRescues(CreateSiegeCatalog());
            new Modules.Siege.UI.SiegeText(_siegeText).Validate(CreateSiegeCatalog());
            var economy = CreateEconomy();
            if (economy != null) new Modules.Siege.UI.SiegeText(_siegeText).Validate(CreateSiegeCatalog(economy));
            var guides = CreateGuides();
            var opening = CreateApproachCatalog();
            foreach (Modules.Approach.ApproachMode mode in Enum.GetValues(typeof(Modules.Approach.ApproachMode)))
                guides.ValidateOpening(opening.Get(mode));
            foreach (var field in CreateFields().Layouts)
            {
                new Modules.Siege.UI.SiegeText(_siegeText).Validate(CreateFieldSiegeCatalog(field));
                foreach (Modules.Approach.ApproachMode mode in Enum.GetValues(typeof(Modules.Approach.ApproachMode)))
                    guides.ValidateOpening(field.Opening.Get(mode));
            }
            Approach.ApproachPanelView.ValidateText(new Text.OptionPreviewText(_approachText));
        }
    }
}
