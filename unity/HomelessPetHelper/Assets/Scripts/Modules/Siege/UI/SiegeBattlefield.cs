using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Animals.UI;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SiegeBattlefield : MaskableGraphic, IPointerClickHandler
    {
        [SerializeField] private SceneSpritePalette _spritePalette;
        public override Texture mainTexture => _spritePalette == null ? base.mainTexture : _spritePalette.Texture;
        [SerializeField]
        private Image _cat;
        private RectTransform _towerGuideTarget;
        private SiegePoint _towerGuidePoint;
        public RectTransform DefenderGuideTarget => _cat != null ? _cat.rectTransform : null;
        [SerializeField] private AnimalCoatView _coat;
        public void ShowAnimal(AnimalSnapshot snapshot) => _coat.Show(snapshot);
        [SerializeField] private SiegeCarryOverlay _carryOverlay;
        [SerializeField] private Slider _rescueBar;
        private float _rescueFill;
        private bool _rescueVisible;
        [Serializable]
        private sealed class AnimalVisual
        {
            public string Id;
            public Sprite Sprite;
        }
        [SerializeField] private AnimalVisual[] _animalVisuals = Array.Empty<AnimalVisual>();
        private Sprite _legacySprite;
        private SiegeCatalog _catalog;
        private SiegeState _state;
        private HashSet<int> _selected = new HashSet<int>();
        private int _lastUnit;
        private string _tower;
        private bool _interactive;
        private bool _sceneMode;
        private readonly SiegePresentation _presentationMotion = new SiegePresentation();
        private bool StyledCombat => _sceneMode && _state.Realtime;
        private Action<string> _playPresentationCue;
        private float _terminalTailMs;
        private int PresentationTime => _state.ElapsedMs + (int)_terminalTailMs;
        private Sprite _sceneAnimal;
        private float _animalWidth;
        private string _ghostDefinition, _ghostLane, _previewLane;
        private bool _ghostSelected, _showRoutes, _freeDeployment;
        [SerializeField] private RectTransform _routeViewport;
        [SerializeField, Min(1)] private float _routeDropRadius = 72;
        [SerializeField, Min(0)] private float _routeAmbiguity = 6;
        public event Action<string> LaneClicked;
        public event Action<SiegePoint> DeploymentClicked;
        public event Action EmptyClicked;
        private SiegeCatalog _validatedCatalog;
        public event Action<int> UnitClicked;
        public event Action<string> TowerClicked;

        internal void SetPresentationAudio(Action<string> play) => _playPresentationCue = play;
        internal void ResetPresentation()
        {
            _terminalTailMs = 0;
            _presentationMotion.Reset();
            _rescueVisible = false;
            if (_rescueBar != null) _rescueBar.gameObject.SetActive(false);
        }

        internal void AdvanceTerminalEffects(float milliseconds)
        {
            if (_state == null || !StyledCombat || _state.Paused || _terminalTailMs >= SiegePresentation.MaxTailMs ||
                _state.Phase != BattlePhase.Won && _state.Phase != BattlePhase.Timeout) return;
            _terminalTailMs = Mathf.Min(SiegePresentation.MaxTailMs, _terminalTailMs + milliseconds);
            SetVerticesDirty();
        }

        private SceneSpritePalette _defaultPalette;
        private bool _paletteCaptured, _animalOnSeparateFloor;
        public void ConfigureScene(Sprite animal, float width) => ConfigureScene(animal, width, false, null);

        public void ConfigureScene(Sprite animal, float width, bool animalOnSeparateFloor, SceneSpritePalette props)
        {
            if (animal == null || width <= 0 || _routeViewport == null)
                throw new ArgumentException("Invalid field animal or route viewport.");
            if (!_paletteCaptured) { _defaultPalette = _spritePalette; _paletteCaptured = true; }
            _spritePalette = props != null ? props : _defaultPalette;
            _validatedCatalog = null;
            _carryOverlay?.SetPalette(props);
            _animalOnSeparateFloor = animalOnSeparateFloor;
            SetMaterialDirty();
            _sceneMode = true; _sceneAnimal = animal; _animalWidth = width;
            if (animalOnSeparateFloor)
            {
                _carryOverlay?.Show(null, null, 1);
                return;
            }
            _cat.preserveAspect = true;
            _cat.rectTransform.anchorMin = _cat.rectTransform.anchorMax = new Vector2(.5f, .5f);
            _cat.rectTransform.pivot = new Vector2(.5f, .15f);
        }

        public void ShowPlan(string definition, string lane, bool selected, string previewLane,
            bool showRoutes = false, bool freeDeployment = false)
        {
            _ghostDefinition = definition; _ghostLane = lane; _ghostSelected = selected; _previewLane = previewLane;
            _showRoutes = showRoutes; _freeDeployment = freeDeployment;
            SetVerticesDirty();
        }

        public Vector2 UnitNormalized(int id)
        {
            SiegePoint point;
            var unit = _state.Units.FirstOrDefault(item => item.Id == id);
            if (id == SiegeOrder.NewUnit && _ghostLane != null) point = _catalog.Lane(_ghostLane).PointAt(0);
            else if (unit != null) point = SiegeEntitiesPosition(unit);
            else return new Vector2(.5f, .5f);
            return new Vector2((float)(point.X / _catalog.Width), 1 - (float)(point.Y / _catalog.Height));
        }
        private static readonly Color Ink = new Color32(58, 79, 73, 255);
        private static readonly Color Mint = new Color32(76, 165, 139, 255);
        private static readonly Color Coral = new Color32(224, 112, 104, 255);
        private float Scale =>
            _catalog == null
                ? 1
                : Mathf.Min(
                    rectTransform.rect.width / (float)_catalog.Width,
                    rectTransform.rect.height / (float)_catalog.Height
                );

        private Vector2 ProjectionScale => _sceneMode
            ? new Vector2(rectTransform.rect.width / (float)_catalog.Width,
                rectTransform.rect.height / (float)_catalog.Height) : Vector2.one * Scale;

        private Vector2 Local(SiegePoint p) => rectTransform.rect.center + Vector2.Scale(
            new Vector2((float)(p.X - _catalog.Width / 2), (float)(_catalog.Height / 2 - p.Y)), ProjectionScale);

        private float PropScale => _sceneMode
            ? Mathf.Max(Scale, 1.35f / (canvas == null ? 1 : canvas.scaleFactor))
            : _state?.EncounterId == null ? Scale
            : Mathf.Max(Scale, .6f / (canvas == null ? 1 : canvas.scaleFactor));

        public void Show(
            SiegeCatalog catalog,
            SiegeState state,
            IEnumerable<int> selected,
            string tower,
            bool interactive
        )
        {
            if (_validatedCatalog != catalog)
            {
                if (_carryOverlay == null)
                    throw new InvalidOperationException("Missing siege carry overlay binding.");
                foreach (var encounter in catalog.Content.Encounters)
                    foreach (string id in encounter.UnitIds)
                    {
                        var unit = catalog.Unit(id);
                        var upgrade = catalog.Unit(unit.UpgradeId);
                        if (!HasVisual(unit.Icon, false) || upgrade != null && !HasVisual(upgrade.Icon, false))
                            throw new InvalidOperationException("Missing siege unit visual: " + id);
                    }
                foreach (var defense in catalog.Content.Towers)
                    if (!HasVisual(defense.Visual, true))
                        throw new InvalidOperationException("Missing siege tower visual: " + defense.Id);
                foreach (var encounter in catalog.Content.Encounters)
                    if (_animalVisuals.Count(x => x.Id == encounter.AnimalVisual && x.Sprite != null) != 1)
                        throw new InvalidOperationException("Missing/duplicate siege animal visual: "
                            + encounter.AnimalVisual);
                if (_legacySprite == null) _legacySprite = _cat.sprite;
                _validatedCatalog = catalog;
            }
            _catalog = catalog;
            _state = state;
            if (StyledCombat) _presentationMotion.Observe(catalog, state, PresentationTime, _playPresentationCue);
            _selected.Clear();
            foreach (int id in selected) _selected.Add(id);
            _tower = tower;
            _interactive = interactive;
            SetVerticesDirty();
        }

        private bool HasVisual(string id, bool tower)
        {
            if (_spritePalette != null) return _spritePalette.TryGet(id, out _);
            return tower ? SiegeTokenArt.SupportsTower(id) : SiegeTokenArt.Supports(id);
        }

        public RectTransform AimTowerGuide(SiegeState.Tower tower)
        {
            if (_towerGuideTarget == null)
            {
                _towerGuideTarget = new GameObject("EnemyGuideTarget", typeof(RectTransform))
                    .GetComponent<RectTransform>();
                _towerGuideTarget.SetParent(rectTransform, false);
            }
            _towerGuidePoint = SiegeGeometry.Position(_catalog, tower);
            PlaceTowerGuide();
            return _towerGuideTarget;
        }

        private void PlaceTowerGuide()
        {
            _towerGuideTarget.localPosition = Local(_towerGuidePoint);
            _towerGuideTarget.sizeDelta = Vector2.one * (32 * PropScale);
        }

        private void LateUpdate()
        {
            if (_catalog == null || _cat == null) return;
            if (_towerGuideTarget != null && _state?.Paused == true) PlaceTowerGuide();
            if (_animalOnSeparateFloor)
            {
                RefreshRescueBar();
                return;
            }
            var encounter = _state?.EncounterId == null ? null : _catalog.Content.Encounter(_state.EncounterId);
            bool jumping = encounter != null && encounter.SweepDelayMs > 0;
            var sprite = _sceneMode ? _sceneAnimal : encounter == null ? _legacySprite
                : _animalVisuals.First(x => x.Id == encounter.AnimalVisual).Sprite;
            if (sprite != null) _cat.sprite = sprite;
            Vector2 home = Local(_sceneMode ? _catalog.Lanes[0].PointAt(_catalog.Lanes[0].Length) : new
                SiegePoint(301, 38));
            Vector2 position = home;
            float jump = 0;
            if (jumping && _state != null && _state.AnimalState.Cast?.Kind == IntentKind.Sweep)
            {
                var a = _state.AnimalState;
                if (a.ImpactAtMs > _state.ElapsedMs)
                {
                    float t = (float)(_state.ElapsedMs - a.LeapStartMs) / (a.ImpactAtMs - a.LeapStartMs);
                    position = Vector2.Lerp(home, Local(a.SweepCenter), t);
                    jump = Mathf.Sin(t * Mathf.PI) * 18 * Scale;
                }
                else if (a.LeapStartMs > 0 && a.RecoveryUntilMs > _state.ElapsedMs)
                {
                    float back = Mathf.Clamp01((a.RecoveryUntilMs - _state.ElapsedMs) / 450f);
                    position = Vector2.Lerp(home, Local(a.SweepCenter), back);
                }
            }
            if (_state != null && _state.AnimalState.Cast?.Kind == IntentKind.Carry)
                position = CarryAnimalPosition(home);
            _cat.rectTransform.anchoredPosition = position + Vector2.up * jump;
            _cat.rectTransform.sizeDelta = _sceneMode
                ? new Vector2(_animalWidth, _animalWidth * 1.15f) * (Scale * 1.6f) :
                Vector2.one * (78 * Scale);
            bool acting = _state != null && _state.AnimalState.Cast?.Kind != IntentKind.Carry
                && _state.Effects.Any(x => x.Kind == "slap" || x.Kind == "sweep");
            _cat.rectTransform.localRotation = Quaternion.Euler(0, 0, acting ? -9 : 0);
            bool winding = jumping && _state != null && _state.AnimalState.Intent == IntentKind.Sweep
                && _state.AnimalState.Cast == null && _state.AnimalState.Charge > .5;
            _cat.rectTransform.localScale = winding ? new Vector3(1.05f, .9f, 1) : Vector3.one;
            _carryOverlay.Show(_catalog, _state?.Units.FirstOrDefault(x => x.IsCarried), PropScale);
            RefreshRescueBar();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_catalog == null || _state == null)
                return;
            if (_freeDeployment) DrawDeployment(vh);
            if (_catalog.HasDeploymentZone && !UsesHeadProgress) DrawRescueProgress(vh);
            foreach (var lane in _catalog.Lanes)
            {
                if (_freeDeployment || _sceneMode && !_showRoutes && lane.Id != _previewLane) continue;
                for (int i = 1; i < lane.Points.Count; i++)
                    Line(
                        vh,
                        Local(lane.Points[i - 1]),
                        Local(lane.Points[i]),
                        (_sceneMode ? lane.Id == _previewLane ? 4 : 2 : 13) * Scale,
                        _sceneMode ? new Color32(116, 153, 128, 70) : new Color32(222, 233, 226, 255)
                    );
            }
            foreach (var pad in _catalog.Pads)
            {
                if (_sceneMode) continue;
                Ring(
                    vh,
                    Local(_catalog.Lane(pad.LaneId).PointAt(_catalog.Lane(pad.LaneId).Length * pad.At)),
                    8 * Scale,
                    1 * Scale,
                    new Color32(169, 189, 178, 255)
                );
            }
            DrawIntent(vh);
            DrawZones(vh);
            DrawTowers(vh);
            foreach (var unit in _state.Units)
                if (!unit.IsCarried) DrawUnit(vh, unit);
            if (_sceneMode && _ghostDefinition != null && _ghostLane != null)
            {
                Vector2 point = Local(_catalog.Lane(_ghostLane).PointAt(0));
                SiegeTokenArt.Draw(vh, _catalog.Unit(_ghostDefinition).Icon, point,
                    9 * PropScale, 0, palette: _spritePalette);
                Ring(vh, point, 15 * PropScale, Scale, _ghostSelected ? Ink : Mint);
            }
            DrawHold(vh);
            DrawCarry(vh);
            DrawEffects(vh);
        }

        private bool UsesHeadProgress => _rescueBar != null && _sceneMode && _state?.Realtime == true;

        private void RefreshRescueBar()
        {
            if (_rescueBar == null) return;
            if (!UsesHeadProgress)
            {
                if (_rescueVisible) _rescueBar.gameObject.SetActive(false);
                _rescueVisible = false;
                return;
            }
            float target = Mathf.Clamp01((float)(_state.Progress / _catalog.Rules.Target));
            if (!_rescueVisible)
            {
                _rescueFill = target;
                _rescueBar.gameObject.SetActive(true);
                _rescueVisible = true;
            }
            _rescueFill = Mathf.MoveTowards(_rescueFill, target, Time.unscaledDeltaTime * 2f);
            _rescueBar.SetValueWithoutNotify(_rescueFill);
            var animal = _cat.rectTransform;
            var bar = (RectTransform)_rescueBar.transform;
            // The upstairs animal lives in a full-frame stretched battlefield. Share its
            // head anchor so a late room resize moves both elements together.
            if (_animalOnSeparateFloor && animal.parent == rectTransform && rectTransform.parent == bar.parent)
            {
                var anchor = new Vector2((animal.anchorMin.x + animal.anchorMax.x) * .5f, animal.anchorMax.y);
                bar.anchorMin = bar.anchorMax = anchor;
                bar.anchoredPosition = new Vector2((animal.offsetMin.x + animal.offsetMax.x) * .5f,
                    animal.offsetMax.y + 10);
                return;
            }
            bar.anchorMin = bar.anchorMax = new Vector2(.5f, .5f);
            var head = animal.TransformPoint(new Vector3(animal.rect.center.x, animal.rect.yMax, 0));
            bar.position = head + bar.parent.TransformVector(new Vector3(0, 10, 0));
        }

        private void DrawRescueProgress(VertexHelper mesh)
        {
            var top = rectTransform.rect;
            float scale = Scale;
            var origin = new Vector2(top.xMin + 40 * scale, top.yMax - 108 * scale);
            float width = 116 * scale;
            float filled = width * Mathf.Clamp01((float)(_state.Progress / _catalog.Rules.Target));
            SiegeTokenArt.Paw(mesh, origin - Vector2.right * 15 * scale, 7 * scale, _spritePalette);
            Line(mesh, origin, origin + Vector2.right * width, 7 * scale, new Color32(243, 231, 203, 255));
            if (filled > 0) Line(mesh, origin, origin + Vector2.right * filled, 7 * scale, Mint);
            var heart = origin + Vector2.right * (width + 10 * scale);
            Circle(mesh, heart + new Vector2(-2.8f, 2) * scale, 3.4f * scale, Coral);
            Circle(mesh, heart + new Vector2(2.8f, 2) * scale, 3.4f * scale, Coral);
            Triangle(mesh, heart + new Vector2(-6, 2) * scale,
                heart + new Vector2(6, 2) * scale, heart + Vector2.down * 6 * scale, Coral);
        }

        private readonly List<SiegePoint> _deploymentPolygon = new List<SiegePoint>(8);
        private readonly List<SiegePoint> _clipPolygon = new List<SiegePoint>(8);
        private void DrawDeployment(VertexHelper vh)
        {
            _deploymentPolygon.Clear();
            var min = _catalog.HasDeploymentZone ? _catalog.DeploymentMin : new SiegePoint(0, 0);
            var max = _catalog.HasDeploymentZone ? _catalog.DeploymentMax :
                new SiegePoint(_catalog.Width, _catalog.Height);
            _deploymentPolygon.Add(min);
            _deploymentPolygon.Add(new SiegePoint(max.X, min.Y));
            _deploymentPolygon.Add(max);
            _deploymentPolygon.Add(new SiegePoint(min.X, max.Y));
            if (!_catalog.HasDeploymentZone)
            {
                ClipDeployment(-.1, false);
                ClipDeployment(_catalog.Rules.DeploymentFraction, true);
            }
            for (int index = 1; index + 1 < _deploymentPolygon.Count; index++)
                Triangle(vh, Local(_deploymentPolygon[0]), Local(_deploymentPolygon[index]),
                    Local(_deploymentPolygon[index + 1]), new Color32(76, 165, 139, 38));
            for (int index = 0; index < _deploymentPolygon.Count; index++)
                Line(vh, Local(_deploymentPolygon[index]),
                    Local(_deploymentPolygon[(index + 1) % _deploymentPolygon.Count]), 2 * Scale, Mint);
        }

        private void ClipDeployment(double bound, bool upper)
        {
            _clipPolygon.Clear();
            for (int index = 0; index < _deploymentPolygon.Count; index++)
            {
                var a = _deploymentPolygon[index];
                var b = _deploymentPolygon[(index + 1) % _deploymentPolygon.Count];
                double fa = SiegeDeployment.Fraction(_catalog, a), fb = SiegeDeployment.Fraction(_catalog, b);
                bool insideA = upper ? fa <= bound : fa >= bound;
                bool insideB = upper ? fb <= bound : fb >= bound;
                if (insideA) _clipPolygon.Add(a);
                if (insideA != insideB) _clipPolygon.Add(a.Lerp(b, (bound - fa) / (fb - fa)));
            }
            _deploymentPolygon.Clear(); _deploymentPolygon.AddRange(_clipPolygon);
        }

        private void DrawIntent(VertexHelper vh)
        {
            var a = _state.AnimalState;
            if ((a.Intent == IntentKind.Sweep && a.HeldUnitId == 0 && a.Cast?.Kind != IntentKind.Carry)
                || a.ImpactAtMs > _state.ElapsedMs)
            {
                Area(vh, a.SweepCenter, a.SweepRadius, new Color32(233, 122, 108, 35));
                AreaOutline(vh, a.SweepCenter, a.SweepRadius, 1.5f * Scale, Coral);
            }
            if (a.GuardLane != null && a.GuardUntilMs > _state.ElapsedMs)
            {
                var center = Local(
                    _catalog.Lane(a.GuardLane).PointAt(_catalog.Lane(a.GuardLane).Length * _catalog.Rules.GuardStart)
                );
                Ring(vh, center, 18 * Scale, 3 * Scale, Coral);
            }
            if (!_state.Realtime && a.IntentPad != null)
            {
                var pad = _catalog.Pads.First(x => x.Id == a.IntentPad);
                Ring(
                    vh,
                    Local(_catalog.Lane(pad.LaneId).PointAt(_catalog.Lane(pad.LaneId).Length * pad.At)),
                    17 * Scale,
                    2 * Scale,
                    new Color32(224, 176, 64, 255)
                );
            }
        }

        private void DrawHold(VertexHelper vh)
        {
            var a = _state.AnimalState;
            bool active = a.HeldUnitId != 0;
            if (!active && a.Intent != IntentKind.Hold)
                return;
            var d = _catalog.Content.Encounter(_state.EncounterId);
            Vector2 origin = Local(new SiegePoint(d.HoldOriginX, d.HoldOriginY));
            var target = _state.Units.FirstOrDefault(x => x.Id == (active ? a.HeldUnitId : a.IntentUnitId));
            if (!active)
            {
                DrawGrabRange(vh, new SiegePoint(d.HoldOriginX, d.HoldOriginY), d.HoldRange);
                if (target == null) return;
                Vector2 point = Local(SiegeEntitiesPosition(target));
                Line(vh, origin, point, Scale, new Color32(224, 176, 64, 150));
                Ring(vh, point, 15 * PropScale, 2 * PropScale, new Color32(224, 176, 64, 255));
                return;
            }
            if (target == null) return;
            float t = Mathf.Clamp01((_state.ElapsedMs - a.HoldStartedMs) / 200f);
            Vector2 held = Local(SiegeEntitiesPosition(target));
            Vector2 paw = Vector2.Lerp(held + Vector2.up * 20 * PropScale, held, t);
            Vector2 shoulder = origin + new Vector2(-9, -22) * Scale;
            Line(vh, shoulder, paw, 7 * PropScale, new Color32(108, 83, 66, 255));
            Line(vh, shoulder, paw, 5 * PropScale, new Color32(245, 190, 126, 255));
            SiegeTokenArt.Paw(vh, paw + Vector2.up * 6 * PropScale, 12 * PropScale, _spritePalette);
            Ring(vh, held, 17 * PropScale, 2 * PropScale, Coral);
        }

        private void DrawGrabRange(VertexHelper vh, SiegePoint point, double range)
        {
            // Only the reachable arc belongs on the map, not over the surrounding HUD.
            var bounds = new Rect(0, 0, (float)_catalog.Width, (float)_catalog.Height);
            var origin = new Vector2((float)point.X, (float)point.Y);
            for (int i = 0; i < 120; i++)
            {
                float angle = i * Mathf.PI / 60;
                float next = (i + 1) * Mathf.PI / 60;
                var from = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (float)range;
                var to = origin + new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * (float)range;
                if (bounds.Contains(from) && bounds.Contains(to))
                    Line(vh, Local(new SiegePoint(from.x, from.y)), Local(new SiegePoint(to.x, to.y)),
                        Scale, new Color32(224, 176, 64, 180));
            }
        }

        private Vector2 CarryAnimalPosition(Vector2 home)
        {
            var cast = _state.AnimalState.Cast;
            var d = _catalog.Content.Encounter(_state.EncounterId);
            Vector2 offset = Vector2.down * 6 * Scale;
            var unit = _state.Units.FirstOrDefault(x => x.Id == cast.Target.UnitId);
            if (cast.Phase == CastPhase.Active && unit != null)
                return Local(SiegeEntitiesPosition(unit)) + offset;
            if (cast.Phase == CastPhase.Windup)
            {
                float t = Mathf.Clamp01((_state.ElapsedMs - cast.StartedMs)
                    / (float)Math.Max(1, cast.ResolveAtMs - cast.StartedMs));
                return Vector2.Lerp(home, Local(cast.Point) + offset, t);
            }
            float back = Mathf.Clamp01((_state.AnimalState.RecoveryUntilMs - _state.ElapsedMs)
                / (float)Math.Max(1, d.CarryRecoveryMs));
            return Vector2.Lerp(home, Local(cast.Point) + offset, back);
        }

        private void DrawCarry(VertexHelper vh)
        {
            var a = _state.AnimalState;
            bool casting = a.Cast != null && a.Cast.Kind == IntentKind.Carry;
            if (casting && a.Cast.Phase == CastPhase.Recovery) return;
            if (!casting && a.Intent != IntentKind.Carry) return;
            var d = _catalog.Content.Encounter(_state.EncounterId);
            var unit = _state.Units.FirstOrDefault(x => x.Id == (casting ? a.Cast.Target.UnitId : a.IntentUnitId));
            if (!casting)
                DrawGrabRange(vh, new SiegePoint(d.CarryOriginX, d.CarryOriginY), d.CarryRange);
            if (unit == null) return;
            var lane = _catalog.Lane(unit.LaneId);
            double from = unit.IsCarried ? unit.CarryFromDistance : unit.Distance;
            double to = unit.IsCarried ? unit.CarryToDistance : Math.Max(d.CarryMinDistance, from - d.CarryDistance);
            for (int i = 0; i < 16; i++)
                Line(vh, Local(lane.PointAt(from + (to - from) * i / 16)),
                    Local(lane.PointAt(from + (to - from) * (i + 1) / 16)),
                    Scale, new Color32(224, 176, 64, 190));
            Ring(vh, Local(lane.PointAt(to)), 13 * PropScale, 2 * PropScale, Mint);
            Ring(vh, Local(SiegeEntitiesPosition(unit)), 15 * PropScale, 2 * PropScale,
                unit.IsCarried ? Coral : new Color32(224, 176, 64, 255));
        }

        private void DrawZones(VertexHelper vh)
        {
            foreach (var zone in _state.SlowZones)
            {
                var center = Local(zone.Center);
                float radius = (float)zone.Radius * Scale;
                Area(vh, zone.Center, zone.Radius, new Color32(100, 176, 211, 38));
                AreaOutline(vh, zone.Center, zone.Radius, Scale, new Color32(100, 176, 211, 130));
                for (int i = -1; i <= 1; i++)
                    if (zone.Visual == "thread")
                        Line(vh, center + new Vector2(-radius * .6f, i * radius * .3f),
                            center + new Vector2(radius * .6f, i * radius * .3f + radius * .2f),
                            Scale, new Color32(229, 119, 139, 190));
                    else
                        Ring(vh, center + new Vector2(i * radius * .45f, i % 2 * radius * .25f),
                            radius * .22f, Scale, new Color32(100, 176, 211, 180));
            }
        }

        private void DrawTowers(VertexHelper vh)
        {
            foreach (var tower in _state.Towers)
            {
                var d = _catalog.Defense(tower);
                Vector2 point = Local(SiegeGeometry.Position(_catalog, tower));
                if (StyledCombat && tower.Status == TowerStatus.Destroyed) continue;
                if (tower.Id == _tower && tower.Status != TowerStatus.Destroyed)
                {
                    Area(vh, SiegeGeometry.Position(_catalog, tower), d.Range,
                        new Color32(224, 112, 104, 20));
                    AreaOutline(vh, SiegeGeometry.Position(_catalog, tower), d.Range, Scale, Coral);
                }
                Color tint = tower.Status == TowerStatus.Destroyed ? new Color32(172, 181, 174, 255) : Coral;
                if (tower.DefinitionId != null && tower.Status != TowerStatus.Destroyed)
                {
                    float unfold = tower.Status == TowerStatus.Building
                        ? Mathf.Lerp(.3f, 1, 1 - (tower.ReadyAtMs - _state.ElapsedMs)
                            / (float)(_state.Realtime
                                ? _catalog.Rules.FastBuildDurationMs : _catalog.Rules.BuildDurationMs)) : 1;
                    if (StyledCombat)
                        _presentationMotion.DrawTower(vh, _spritePalette, tower, d.Visual, point,
                            11 * PropScale * unfold, PresentationTime, d.PeriodMs);
                    else SiegeTokenArt.Tower(vh, d.Visual, point, 11 * PropScale * unfold,
                        tower.WindingUp, _spritePalette);
                }
                else Quad(vh, point, new Vector2(16, 18) * Scale, tint);
                if (tower.WindingUp && tower.Status == TowerStatus.Active)
                {
                    Area(vh, tower.LockedPoint, d.Radius, new Color32(233, 122, 108, 35));
                    AreaOutline(vh, tower.LockedPoint, d.Radius, 1.5f * Scale, Coral);
                }
                if (tower.Status == TowerStatus.Building)
                    Ring(vh, point, 13 * Scale, Scale, Ink);
                if (tower.Status == TowerStatus.Destroyed)
                    continue;
                Bar(vh, point + Vector2.up * 18 * PropScale, (float)(tower.Hp / d.Hp), Coral);
                if (_state.AnimalState.RepairTowerId == tower.Id)
                    Ring(vh, point, 15 * Scale, 3 * Scale, Mint);
            }
        }

        private void DrawUnit(VertexHelper vh, Unit unit)
        {
            var d = _catalog.Unit(unit.DefinitionId);
            Vector2 point = Local(SiegeEntitiesPosition(unit));
            bool basic = !string.IsNullOrEmpty(d.UpgradeId);
            Color tint = basic ? Mint : new Color32(71, 112, 183, 255);
            float radius = (basic ? 8 : 11) * PropScale * (_catalog.HasDeploymentZone ? 1.4f : 1f);
            if (_selected.Contains(unit.Id))
                Ring(vh, point, radius + 4 * Scale, 2 * Scale, Ink);
            if (unit.Shield > 0)
                Ring(vh, point, radius + 2 * Scale, Scale, new Color32(130, 182, 224, 255));
            if (_state.EncounterId != null)
            {
                if (StyledCombat)
                    _presentationMotion.DrawUnit(vh, _spritePalette, unit, d.Icon, point, radius, PresentationTime);
                else
                {
                    float phase = (float)(unit.Distance / 8 + unit.Id * .7);
                    var bob = Vector2.up * Mathf.Abs(Mathf.Sin(phase)) * 1.5f * Scale;
                    SiegeTokenArt.Draw(vh, d.Icon, point + bob, radius, phase, unit.Brace > 0, _spritePalette);
                }
                if (unit.Brace > 0)
                    Line(vh, point + new Vector2(-radius, -radius), point + new Vector2(radius, -radius),
                        2 * Scale, new Color32(224, 173, 80, 255));
            }
            else if (d.Icon == "truck" || d.Icon == "shield")
            {
                Quad(vh, point, new Vector2(radius * 2, radius * 1.6f), tint);
                Circle(vh, point + new Vector2(-radius * .6f, -radius), radius * .3f, Ink);
                Circle(vh, point + new Vector2(radius * .6f, -radius), radius * .3f, Ink);
            }
            else if (d.Icon == "umbrella")
            {
                Triangle(
                    vh,
                    point + Vector2.up * radius,
                    point + new Vector2(-radius, -radius * .4f),
                    point + new Vector2(radius, -radius * .4f),
                    tint
                );
                Line(vh, point, point - Vector2.up * radius, Scale, Ink);
            }
            else
            {
                Circle(vh, point, radius, tint);
                Circle(vh, point, radius * .35f, Color.white);
            }
            Bar(vh, point + Vector2.up * (radius + 5 * PropScale), (float)(unit.Hp / d.Hp), tint);
        }

        private SiegePoint SiegeEntitiesPosition(Unit unit) => SiegeGeometry.Position(_catalog, unit);

        private void DrawEffects(VertexHelper vh)
        {
            foreach (var effect in _state.Effects)
            {
                if (StyledCombat && _presentationMotion.DrawEffect(vh, _spritePalette, effect,
                    Local(effect.From), Local(effect.To), ProjectionScale * (float)effect.Radius,
                    11 * PropScale, PresentationTime)) continue;
                float duration = Mathf.Max(1, effect.UntilMs - effect.StartMs);
                float t = Mathf.Clamp01((_state.ElapsedMs - effect.StartMs) / duration);
                if (effect.Kind == "mergePart")
                {
                    SiegeTokenArt.Draw(vh, effect.Visual, Vector2.Lerp(Local(effect.From), Local(effect.To), t),
                        (1 - t * .7f) * 8 * Scale, t * 4, palette: _spritePalette);
                    continue;
                }
                if (effect.Kind == "mend" || effect.Kind == "interfere" || effect.Kind == "impact")
                {
                    Color color = effect.Kind == "interfere" ? (Color)new Color32(224, 173, 80, 230) : Mint;
                    Vector2 destination = Local(effect.To);
                    Line(vh, Local(effect.From), destination, 2 * Scale, color);
                    Ring(vh, destination, (5 + t * 8) * Scale, 2 * Scale, color);
                    continue;
                }
                if (effect.Kind == "brace")
                {
                    Line(vh, Local(effect.From), Local(effect.To), 2 * Scale, new Color32(224, 173, 80, 220));
                    continue;
                }
                if (effect.Kind == "towerShot" && effect.Visual != null && effect.Visual != "legacy")
                {
                    Circle(vh, Vector2.Lerp(Local(effect.From), Local(effect.To), t),
                        3.5f * Scale, effect.Visual == "kennel" ? new Color32(255, 241, 202, 255) : Coral);
                    continue;
                }
                if (effect.Kind == "towerShot" || effect.Kind == "unitShot")
                    Line(
                        vh,
                        Local(effect.From),
                        Local(effect.To),
                        1.5f * Scale,
                        effect.Kind == "towerShot" ? Coral : Mint
                    );
                else if (effect.Kind == "sweep" || effect.Kind == "push")
                {
                    Circle(vh, Local(effect.From), (float)effect.Radius * Scale, new Color32(233, 122, 108, 72));
                    Ring(vh, Local(effect.From), (float)effect.Radius * Scale * Mathf.Max(.1f, t), 4 * Scale, Coral);
                }
                else
                    Ring(vh, Local(effect.From), 16 * Scale, 3 * Scale, effect.Kind == "slap" ? Coral : Mint);
            }
        }

        /// <summary>Screen-space path distance; shared segments and equally near lanes are ambiguous.</summary>
        public string PickLane(Vector2 screen, Camera camera)
        {
            if (_catalog == null || _routeViewport == null || rectTransform.rect.width <= 0 ||
                rectTransform.rect.height <= 0 ||
                !RectTransformUtility.RectangleContainsScreenPoint(_routeViewport, screen, camera) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera,
                    out var local)) return null;
            string best = null;
            float first = float.PositiveInfinity, second = float.PositiveInfinity;
            foreach (var lane in _catalog.Lanes)
            {
                if (!lane.Open) continue;
                float distance = float.PositiveInfinity;
                for (int i = 1; i < lane.Points.Count; i++)
                {
                    Vector2 a = Local(lane.Points[i - 1]), b = Local(lane.Points[i]);
                    Vector2 delta = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(local - a, delta) / delta.sqrMagnitude);
                    distance = Mathf.Min(distance, Vector2.Distance(local, a + delta * t));
                }
                if (distance < first) { second = first; first = distance; best = lane.Id; }
                else second = Mathf.Min(second, distance);
            }
            return first <= _routeDropRadius && second - first > _routeAmbiguity ? best : null;
        }

        public bool PickDeployment(Vector2 screen, Camera camera, out SiegePoint point)
        {
            point = default;
            if (_catalog == null || _routeViewport == null || rectTransform.rect.width <= 0 ||
                rectTransform.rect.height <= 0 ||
                (!_catalog.HasDeploymentZone &&
                    !RectTransformUtility.RectangleContainsScreenPoint(_routeViewport, screen, camera)) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera,
                    out var local)) return false;
            point = new SiegePoint(_catalog.Width / 2 + (local.x - rectTransform.rect.center.x) / ProjectionScale.x,
                _catalog.Height / 2 - (local.y - rectTransform.rect.center.y) / ProjectionScale.y);
            return SiegeDeployment.CanDeploy(_catalog, point);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (
                _state == null
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 local
                )
            )
                return;
            if (_freeDeployment && _interactive)
            {
                if (PickDeployment(eventData.position, eventData.pressEventCamera, out var deploy))
                    DeploymentClicked?.Invoke(deploy);
                else EmptyClicked?.Invoke();
                return;
            }
            if (_sceneMode && _interactive && _ghostDefinition != null && _ghostLane != null &&
                Vector2.Distance(Local(_catalog.Lane(_ghostLane).PointAt(0)), local) <= 17 * PropScale)
            { UnitClicked?.Invoke(SiegeOrder.NewUnit); return; }
            if (_showRoutes && _interactive)
            {
                string lane = PickLane(eventData.position, eventData.pressEventCamera);
                if (lane != null) { LaneClicked?.Invoke(lane); return; }
            }
            var units = _state
                .Units.Where(x => !x.IsCarried
                    && Vector2.Distance(Local(SiegeEntitiesPosition(x)), local) <= 15 * PropScale)
                .OrderBy(x => x.Id)
                .ToArray();
            if (units.Length > 0 && _interactive)
            {
                int index = Array.FindIndex(units, x => x.Id == _lastUnit);
                _lastUnit = units[(index + 1) % units.Length].Id;
                UnitClicked?.Invoke(_lastUnit);
                return;
            }
            var tower = _state.Towers.FirstOrDefault(x =>
                x.Status != TowerStatus.Destroyed &&
                Vector2.Distance(Local(SiegeGeometry.Position(_catalog, x)), local) <= 14 * PropScale
            );
            if (tower != null)
                TowerClicked?.Invoke(tower.Id);
            else if (_sceneMode) EmptyClicked?.Invoke();
        }

        private void Bar(VertexHelper vh, Vector2 p, float fill, Color tint)
        {
            Quad(vh, p, new Vector2(20, 2.5f) * PropScale, new Color32(206, 214, 208, 255));
            float width = 20 * Mathf.Clamp01(fill) * PropScale;
            Quad(vh, p + Vector2.left * (20 * PropScale - width) / 2,
                new Vector2(width, 2.5f * PropScale), tint);
        }

        private static void Quad(VertexHelper vh, Vector2 center, Vector2 size, Color tint)
        {
            Vector2 half = size / 2;
            Triangle(
                vh,
                center + new Vector2(-half.x, -half.y),
                center + new Vector2(-half.x, half.y),
                center + new Vector2(half.x, half.y),
                tint
            );
            Triangle(
                vh,
                center + new Vector2(-half.x, -half.y),
                center + new Vector2(half.x, half.y),
                center + new Vector2(half.x, -half.y),
                tint
            );
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width / 2;
            Triangle(vh, a - n, a + n, b + n, tint);
            Triangle(vh, a - n, b + n, b - n, tint);
        }

        private void Area(VertexHelper vh, SiegePoint point, double radius, Color tint)
        {
            Vector2 center = Local(point), axes = ProjectionScale * (float)radius;
            const int segments = 36;
            for (int index = 0; index < segments; index++)
                Triangle(vh, center, center + Vector2.Scale(Direction(index, segments), axes),
                    center + Vector2.Scale(Direction(index + 1, segments), axes), tint);
        }

        private void AreaOutline(VertexHelper vh, SiegePoint point, double radius, float width, Color tint)
        {
            Vector2 center = Local(point), axes = ProjectionScale * (float)radius;
            const int segments = 36;
            for (int index = 0; index < segments; index++)
                Line(vh, center + Vector2.Scale(Direction(index, segments), axes),
                    center + Vector2.Scale(Direction(index + 1, segments), axes), width, tint);
        }

        private static void Circle(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            const int segments = 16;
            for (int i = 0; i < segments; i++)
                Triangle(
                    vh,
                    center,
                    center + Direction(i, segments) * radius,
                    center + Direction(i + 1, segments) * radius,
                    tint
                );
        }

        private static void Ring(VertexHelper vh, Vector2 center, float radius, float width, Color tint)
        {
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = Direction(i, segments),
                    b = Direction(i + 1, segments);
                Triangle(vh, center + a * radius, center + a * (radius - width), center + b * radius, tint);
                Triangle(vh, center + b * radius, center + a * (radius - width), center + b * (radius - width), tint);
            }
        }

        private static Vector2 Direction(int i, int count) =>
            new Vector2(Mathf.Cos(i * Mathf.PI * 2 / count), Mathf.Sin(i * Mathf.PI * 2 / count));

        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero);
            vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
