using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;
using Unity.Profiling;
using HomelessPetHelper.Modules.Approach;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Game.Field
{
    /// <summary>Shared scene props rendered in the same frame as paths and opening cells.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OutingFieldGraphic : MaskableGraphic
    {
        [SerializeField] private Color _stone, _soil, _leaves, _leafLight, _box, _water, _wood, _mud;
        [SerializeField] private SceneSpritePalette _spritePalette;
        [SerializeField] private MotionScope _motion;
        public override Texture mainTexture => _spritePalette == null ? base.mainTexture : _spritePalette.Texture;
        private OutingFieldLayout _layout;
        private FieldSceneSnapshot _snapshot;
        private struct Effect
        {
            public bool Active;
            public int Cell;
            public string Visual;
            public MotionCue Cue;
            public float StartedAt;
        }
        private readonly Effect[] _effects = new Effect[16];
        private float _nextFrame, _lastTime = -1;
        private static readonly ProfilerMarker MotionMarker = new ProfilerMarker("Presentation.FieldMotion");

        protected override void OnEnable()
        {
            base.OnEnable();
            System.Array.Clear(_effects, 0, _effects.Length);
            _nextFrame = 0;
            _lastTime = -1;
        }

        public void Configure(OutingFieldLayout layout)
        {
            _layout = layout;
            _snapshot = null;
            System.Array.Clear(_effects, 0, _effects.Length);
            _nextFrame = 0;
            _lastTime = -1;
            SetVerticesDirty();
        }
        public void Show(FieldSceneSnapshot snapshot)
        {
            if (_motion != null && _snapshot != null && _snapshot.Template == snapshot.Template)
                ObserveChanges(snapshot);
            _snapshot = snapshot;
            SetVerticesDirty();
        }
        private void LateUpdate()
        {
            if (_motion == null || _layout == null || _snapshot == null || _spritePalette == null) return;
            float now = _motion.SceneSeconds;
            if (now <= _lastTime || now < _nextFrame) return;
            using (MotionMarker.Auto())
            {
                _lastTime = now;
                _nextFrame = now + _motion.Library.SceneFrameSeconds;
                SetVerticesDirty();
            }
        }

        private void ObserveChanges(FieldSceneSnapshot next)
        {
            // Undo/restoration cancels old cosmetic remnants rather than replaying completion.
            bool restored = next.Boxes.Count < _snapshot.Boxes.Count ||
                (_snapshot.Bridge >= 0 && next.Bridge != _snapshot.Bridge);
            for (int index = 0; index < next.Remaining.Count; index++)
                restored |= next.Remaining[index] > _snapshot.Remaining[index];
            if (restored)
            {
                System.Array.Clear(_effects, 0, _effects.Length);
                return;
            }
            for (int index = 0; index < next.Remaining.Count; index++)
            {
                if (next.Remaining[index] >= _snapshot.Remaining[index] || next.Remaining[index] > 0) continue;
                var debris = next.Template.Debris[index];
                AddEffect(debris.Cell, DebrisVisual(debris.Tool), MotionCue.Clear);
            }
            foreach (int cell in next.Boxes)
                if (!_snapshot.Boxes.Contains(cell)) AddEffect(cell, "box", MotionCue.Place);
            if (next.Bridge >= 0 && next.Bridge != _snapshot.Bridge)
                AddEffect(next.Bridge, "bridge", MotionCue.Place);
        }

        private void AddEffect(int cell, string visual, MotionCue cue)
        {
            int slot = 0;
            for (int index = 0; index < _effects.Length; index++)
            {
                if (!_effects[index].Active) { slot = index; break; }
                if (_effects[index].StartedAt < _effects[slot].StartedAt) slot = index;
            }
            _effects[slot] = new Effect { Active = true, Cell = cell, Visual = visual,
                Cue = cue, StartedAt = _motion.SceneSeconds };
        }

        private MotionPose Ambient(MotionCue cue, int cell) => _motion == null ? MotionPose.Identity :
            _motion.Library.Sample(cue, _motion.SceneSeconds + cell * .173f);

        private MotionPose Placement(int cell)
        {
            if (_motion == null) return MotionPose.Identity;
            for (int index = 0; index < _effects.Length; index++)
            {
                var effect = _effects[index];
                if (effect.Active && effect.Cue == MotionCue.Place && effect.Cell == cell)
                    return _motion.Library.Sample(effect.Cue, _motion.SceneSeconds - effect.StartedAt);
            }
            return MotionPose.Identity;
        }

        private void DrawEffects(VertexHelper mesh, Vector2 size)
        {
            if (_motion == null) return;
            for (int index = 0; index < _effects.Length; index++)
            {
                var effect = _effects[index];
                if (!effect.Active) continue;
                float age = _motion.SceneSeconds - effect.StartedAt;
                if (age >= _motion.Library.Duration(effect.Cue)) { _effects[index].Active = false; continue; }
                if (effect.Cue != MotionCue.Clear) continue;
                var pose = _motion.Library.Sample(effect.Cue, age);
                // Vertex alpha belongs to this isolated prop mesh; no Image/Canvas alpha is animated.
                DrawSprite(mesh, effect.Visual, Local(_layout.CellPoint(effect.Cell)), size * .9f, pose);
            }
        }
        private static string DebrisVisual(ApproachTool tool) => tool == ApproachTool.Weeds ? "weeds"
            : tool == ApproachTool.Mud ? "mud" : "debris";
        private Vector2 Local(Vector2 point) => new Vector2(
            rectTransform.rect.xMin + point.x / _layout.Width * rectTransform.rect.width,
            rectTransform.rect.yMax - point.y / _layout.Height * rectTransform.rect.height);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            using (MotionMarker.Auto()) PopulateMesh(vh);
        }

        private void PopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_layout == null || _snapshot == null) return;
            if (_spritePalette != null)
            {
                DrawSprites(vh);
                return;
            }
            float scale = rectTransform.rect.width / _layout.Width;
            var template = _snapshot.Template;
            var size = new Vector2(_layout.Grid.width / template.Columns, _layout.Grid.height / template.Rows) * scale;
            for (int cell = 0; cell < template.CellCount; cell++)
            {
                var point = Local(_layout.CellPoint(cell));
                char terrain = template.Terrain(cell);
                if (terrain == '#') Hedge(vh, point, size, scale, cell);
                else if (terrain == '~') Quad(vh, point, size, _water);
                else if (cell % 2 == 0) Quad(vh, point, size * new Vector2(.24f, .18f), _stone);
                if (_snapshot.Bridge == cell)
                    for (int plank = -2; plank <= 2; plank++)
                        Quad(vh, point + Vector2.up * plank * size.y / 5, new Vector2(size.x * .94f, size.y /
                            6), _wood);
                if (_snapshot.Boxes.Contains(cell))
                {
                    Quad(vh, point + Vector2.down * scale * 2, size * .67f, _soil);
                    Quad(vh, point, size * .65f, _box);
                    Line(vh, point - size * .28f, point + size * .28f, scale * 2, _wood);
                    Line(vh, point + new Vector2(-size.x, size.y) * .28f,
                        point + new Vector2(size.x, -size.y) * .28f, scale * 2, _wood);
                }
            }
            for (int i = 0; i < template.Debris.Count; i++)
            {
                if (_snapshot.Remaining[i] <= 0) continue;
                var debris = template.Debris[i];
                var point = Local(_layout.CellPoint(debris.Cell));
                if (debris.Tool == ApproachTool.Weeds)
                    for (int leaf = -2; leaf <= 2; leaf++)
                        Triangle(vh, point + new Vector2(-6, -10) * scale, point +
                            new Vector2(leaf * 6, 13 - Mathf.Abs(leaf) * 2) * scale,
                            point + new Vector2(7, -10) * scale, leaf % 2 == 0 ? _leaves : _leafLight);
                else if (debris.Tool == ApproachTool.Mud)
                    for (int patch = 0; patch < 4; patch++)
                        Disc(vh, point + new Vector2(patch % 2 * 9 - 5, patch / 2 * 7 - 3) * scale, 9 * scale, _mud);
                else
                {
                    Quad(vh, point + new Vector2(-4, -2) * scale, new Vector2(18, 15) * scale, _box);
                    Quad(vh, point + new Vector2(7, 5) * scale, new Vector2(12, 20) * scale, _wood);
                    Line(vh, point + new Vector2(-12, 4) * scale, point + new Vector2(5, -6) * scale, scale, _soil);
                }
            }
        }

        private void DrawSprites(VertexHelper mesh)
        {
            var template = _snapshot.Template;
            float scale = rectTransform.rect.width / _layout.Width;
            var size = new Vector2(_layout.Grid.width / template.Columns,
                _layout.Grid.height / template.Rows) * scale;
            for (int cell = 0; cell < template.CellCount; cell++)
            {
                var point = Local(_layout.CellPoint(cell));
                char terrain = template.Terrain(cell);
                if (terrain == '#') DrawSprite(mesh, "hedge", point, size * 1.15f, Ambient(MotionCue.Foliage, cell));
                else if (terrain == '~') DrawSprite(mesh, "water", point, size, Ambient(MotionCue.Water, cell));
                else if (cell % 2 == 0) DrawSprite(mesh, "stones", point, size * .35f);
                if (_snapshot.Bridge == cell) DrawSprite(mesh, "bridge", point, size * .96f, Placement(cell));
                if (_snapshot.Boxes.Contains(cell)) DrawSprite(mesh, "box", point, size * .85f, Placement(cell));
            }
            for (int index = 0; index < template.Debris.Count; index++)
            {
                if (_snapshot.Remaining[index] <= 0) continue;
                var debris = template.Debris[index];
                var pose = debris.Tool == ApproachTool.Weeds ? Ambient(MotionCue.Foliage, debris.Cell) :
                    MotionPose.Identity;
                DrawSprite(mesh, DebrisVisual(debris.Tool), Local(_layout.CellPoint(debris.Cell)), size * .9f, pose);
            }
            DrawEffects(mesh, size);
        }

        private void DrawSprite(VertexHelper mesh, string id, Vector2 center, Vector2 size) =>
            DrawSprite(mesh, id, center, size, MotionPose.Identity);

        private void DrawSprite(VertexHelper mesh, string id, Vector2 center, Vector2 size, MotionPose pose)
        {
            float scale = rectTransform.rect.width / _layout.Width;
            var tint = new Color(1, 1, 1, pose.Opacity);
            if (!_spritePalette.TryDraw(mesh, id, center + pose.Offset * scale, Vector2.Scale(size, pose.Scale),
                pose.AngleDegrees * Mathf.Deg2Rad, tint))
                throw new System.InvalidOperationException("Missing field sprite: " + id);
        }

        private void Hedge(VertexHelper vh, Vector2 point, Vector2 size, float scale, int cell)
        {
            // Continuous rounded foliage; adjoining cells form garden strips without square grid seams.
            for (int leaf = 0; leaf < 6; leaf++)
            {
                var center = point + new Vector2((leaf % 3 - 1) * size.x * .29f,
                    (leaf / 3 - .5f) * size.y * .42f);
                float radius = size.y * (.32f + ((cell + leaf) % 3) * .025f);
                Disc(vh, center + Vector2.down * scale * 3, radius * 1.07f, _soil);
                Disc(vh, center, radius, (cell + leaf) % 2 == 0 ? _leafLight : _leaves);
            }

        }

        private static void Disc(VertexHelper vh, Vector2 point, float radius, Color color)
        {
            for (int i = 0; i < 16; i++)
                Triangle(vh, point, point + Direction(i) * radius, point + Direction(i + 1) * radius, color);
        }
        private static Vector2 Direction(int index) => new Vector2(Mathf.Cos(index * Mathf.PI / 8),
            Mathf.Sin(index * Mathf.PI / 8));
        private static void Quad(VertexHelper vh, Vector2 point, Vector2 size, Color color)
        {
            Vector2 half = size / 2;
            Triangle(vh, point - half, point + new Vector2(-half.x, half.y), point + half, color);
            Triangle(vh, point - half, point + half, point + new Vector2(half.x, -half.y), color);
        }
        private static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 side = new Vector2(from.y - to.y, to.x - from.x).normalized * width / 2;
            Triangle(vh, from - side, from + side, to + side, color);
            Triangle(vh, from - side, to + side, to - side, color);
        }
        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int index = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
        }
    }
}
