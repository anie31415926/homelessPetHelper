using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SiegeBattlefield : MaskableGraphic, IPointerClickHandler
    {
        [SerializeField]
        private Image _cat;
        private SiegeCatalog _catalog;
        private SiegeState _state;
        private HashSet<int> _selected = new HashSet<int>();
        private int _lastUnit;
        private string _tower;
        private bool _interactive;
        public event Action<int> UnitClicked;
        public event Action<string> TowerClicked;
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

        private Vector2 Local(SiegePoint p) =>
            rectTransform.rect.center
            + new Vector2((float)(p.X - _catalog.Width / 2), (float)(_catalog.Height / 2 - p.Y)) * Scale;

        public void Show(
            SiegeCatalog catalog,
            SiegeState state,
            IEnumerable<int> selected,
            string tower,
            bool interactive
        )
        {
            _catalog = catalog;
            _state = state;
            _selected = new HashSet<int>(selected);
            _tower = tower;
            _interactive = interactive;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (_catalog == null || _cat == null)
                return;
            _cat.rectTransform.anchoredPosition = Local(new SiegePoint(301, 38));
            _cat.rectTransform.sizeDelta = Vector2.one * (78 * Scale);
            bool acting = _state != null && _state.Effects.Any(x => x.Kind == "slap" || x.Kind == "sweep");
            _cat.rectTransform.localRotation = Quaternion.Euler(0, 0, acting ? -9 : 0);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_catalog == null || _state == null)
                return;
            foreach (var lane in _catalog.Lanes)
                for (int i = 1; i < lane.Points.Count; i++)
                    Line(
                        vh,
                        Local(lane.Points[i - 1]),
                        Local(lane.Points[i]),
                        13 * Scale,
                        new Color32(222, 233, 226, 255)
                    );
            foreach (var pad in _catalog.Pads)
                Ring(
                    vh,
                    Local(_catalog.Lane(pad.LaneId).PointAt(_catalog.Lane(pad.LaneId).Length * pad.At)),
                    8 * Scale,
                    1 * Scale,
                    new Color32(169, 189, 178, 255)
                );
            DrawIntent(vh);
            DrawTowers(vh);
            foreach (var unit in _state.Units)
                DrawUnit(vh, unit);
            DrawEffects(vh);
        }

        private void DrawIntent(VertexHelper vh)
        {
            var a = _state.AnimalState;
            if (a.Intent == IntentKind.Sweep)
            {
                Circle(vh, Local(a.SweepCenter), (float)a.SweepRadius * Scale, new Color32(233, 122, 108, 35));
                Ring(vh, Local(a.SweepCenter), (float)a.SweepRadius * Scale, 1.5f * Scale, Coral);
            }
            if (a.GuardLane != null && a.GuardUntilMs > _state.ElapsedMs)
            {
                var center = Local(
                    _catalog.Lane(a.GuardLane).PointAt(_catalog.Lane(a.GuardLane).Length * _catalog.Rules.GuardStart)
                );
                Ring(vh, center, 18 * Scale, 3 * Scale, Coral);
            }
            if (a.IntentPad != null)
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

        private void DrawTowers(VertexHelper vh)
        {
            foreach (var tower in _state.Towers)
            {
                Vector2 point = Local(_catalog.Lane(tower.LaneId).PointAt(tower.Distance));
                if (tower.Id == _tower && tower.Status != TowerStatus.Destroyed)
                {
                    Circle(vh, point, (float)_catalog.Tower.Range * Scale, new Color32(224, 112, 104, 20));
                    Ring(vh, point, (float)_catalog.Tower.Range * Scale, Scale, Coral);
                }
                Color tint = tower.Status == TowerStatus.Destroyed ? new Color32(172, 181, 174, 255) : Coral;
                Quad(vh, point, new Vector2(16, 18) * Scale, tint);
                if (tower.Status == TowerStatus.Building)
                    Ring(vh, point, 13 * Scale, Scale, Ink);
                if (tower.Status == TowerStatus.Destroyed)
                    continue;
                Bar(vh, point + Vector2.up * 15 * Scale, (float)(tower.Hp / _catalog.Tower.Hp), Coral);
                if (_state.AnimalState.RepairTowerId == tower.Id)
                    Ring(vh, point, 15 * Scale, 3 * Scale, Mint);
            }
        }

        private void DrawUnit(VertexHelper vh, Unit unit)
        {
            var d = _catalog.Unit(unit.DefinitionId);
            Vector2 point = Local(SiegeEntitiesPosition(unit));
            Color tint = d.Bag ? Mint : new Color32(71, 112, 183, 255);
            float radius = (d.Bag ? 7 : 10) * Scale;
            if (_selected.Contains(unit.Id))
                Ring(vh, point, radius + 4 * Scale, 2 * Scale, Ink);
            if (unit.Shield > 0)
                Ring(vh, point, radius + 2 * Scale, Scale, new Color32(130, 182, 224, 255));
            if (d.Icon == "truck" || d.Icon == "shield")
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
            Bar(vh, point + Vector2.up * (radius + 5 * Scale), (float)(unit.Hp / d.Hp), tint);
        }

        private SiegePoint SiegeEntitiesPosition(Unit unit) => SiegeGeometry.Position(_catalog, unit);

        private void DrawEffects(VertexHelper vh)
        {
            foreach (var effect in _state.Effects)
            {
                if (effect.Kind == "towerShot" || effect.Kind == "unitShot")
                    Line(
                        vh,
                        Local(effect.From),
                        Local(effect.To),
                        1.5f * Scale,
                        effect.Kind == "towerShot" ? Coral : Mint
                    );
                else if (effect.Kind == "sweep")
                {
                    Circle(vh, Local(effect.From), (float)effect.Radius * Scale, new Color32(233, 122, 108, 72));
                    Ring(vh, Local(effect.From), (float)effect.Radius * Scale, 4 * Scale, Coral);
                }
                else
                    Ring(vh, Local(effect.From), 16 * Scale, 3 * Scale, effect.Kind == "slap" ? Coral : Mint);
            }
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
            var units = _state
                .Units.Where(x => Vector2.Distance(Local(SiegeEntitiesPosition(x)), local) <= 15 * Scale)
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
                Vector2.Distance(Local(_catalog.Lane(x.LaneId).PointAt(x.Distance)), local) <= 14 * Scale
            );
            if (tower != null)
                TowerClicked?.Invoke(tower.Id);
        }

        private void Bar(VertexHelper vh, Vector2 p, float fill, Color tint)
        {
            Quad(vh, p, new Vector2(20, 2.5f) * Scale, new Color32(206, 214, 208, 255));
            float width = 20 * Mathf.Clamp01(fill) * Scale;
            Quad(vh, p + Vector2.left * (20 * Scale - width) / 2, new Vector2(width, 2.5f * Scale), tint);
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

        private static void Circle(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            const int segments = 48;
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
            const int segments = 64;
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
