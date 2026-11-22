using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>One dim layer with explicit input holes, including drag paths between permitted targets.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GuideMaskGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        [SerializeField] private Color _highlight = new Color32(247, 214, 116, 255);
        [SerializeField] private float _outlineWidth = 4;
        private readonly List<RectTransform> _targets = new List<RectTransform>(16);
        private readonly List<Rect> _holes = new List<Rect>(16);
        private readonly List<float> _xs = new List<float>(34), _ys = new List<float>(34);
        private readonly Vector3[] _corners = new Vector3[4];

        public void SetTargets(IReadOnlyList<RectTransform> targets)
        {
            if (targets == null || targets.Count == 0 || targets.Count > 16)
                throw new ArgumentException("A guide mask supports 1 to 16 explicit targets.");
            _targets.Clear();
            foreach (var target in targets)
            {
                if (target == null) throw new ArgumentException("Missing guide target.");
                _targets.Add(target);
            }
            RefreshHoles(true);
        }

        private void LateUpdate() => RefreshHoles(true);

        private void RefreshHoles(bool markDirty)
        {
            bool changed = _holes.Count != _targets.Count;
            while (_holes.Count < _targets.Count) _holes.Add(default);
            if (_holes.Count > _targets.Count) _holes.RemoveRange(_targets.Count, _holes.Count - _targets.Count);
            for (int index = 0; index < _targets.Count; index++)
            {
                var target = _targets[index];
                Rect hole = default;
                if (target != null && target.gameObject.activeInHierarchy)
                {
                    target.GetWorldCorners(_corners);
                    Vector3 min = rectTransform.InverseTransformPoint(_corners[0]);
                    Vector3 max = rectTransform.InverseTransformPoint(_corners[2]);
                    Rect bounds = rectTransform.rect;
                    hole = Rect.MinMaxRect(Mathf.Max(min.x, bounds.xMin), Mathf.Max(min.y, bounds.yMin),
                        Mathf.Min(max.x, bounds.xMax), Mathf.Min(max.y, bounds.yMax));
                }
                if (_holes[index] != hole) { changed = true; _holes[index] = hole; }
            }
            if (changed && markDirty) SetVerticesDirty();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            RefreshHoles(true);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint,
                eventCamera, out var point)) return true;
            return !InsideHole(point);
        }

        private bool InsideHole(Vector2 point)
        {
            foreach (Rect hole in _holes)
                if (hole.width > 0 && hole.height > 0 && hole.Contains(point)) return true;
            return false;
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            RefreshHoles(false);
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            _xs.Clear(); _ys.Clear();
            _xs.Add(bounds.xMin); _xs.Add(bounds.xMax); _ys.Add(bounds.yMin); _ys.Add(bounds.yMax);
            foreach (Rect hole in _holes)
            {
                if (hole.width <= 0 || hole.height <= 0) continue;
                _xs.Add(hole.xMin); _xs.Add(hole.xMax); _ys.Add(hole.yMin); _ys.Add(hole.yMax);
            }
            _xs.Sort(); _ys.Sort();
            for (int x = 1; x < _xs.Count; x++)
                for (int y = 1; y < _ys.Count; y++)
                {
                    Rect tile = Rect.MinMaxRect(_xs[x - 1], _ys[y - 1], _xs[x], _ys[y]);
                    if (tile.width > 0 && tile.height > 0 && !InsideHole(tile.center)) Quad(vertices, tile, color);
                }
            foreach (Rect hole in _holes)
                if (hole.width > 0 && hole.height > 0) Outline(vertices, hole);
        }

        private void Outline(VertexHelper vertices, Rect hole)
        {
            float width = Mathf.Min(_outlineWidth, hole.width / 2, hole.height / 2);
            Quad(vertices, new Rect(hole.xMin, hole.yMin, hole.width, width), _highlight);
            Quad(vertices, new Rect(hole.xMin, hole.yMax - width, hole.width, width), _highlight);
            Quad(vertices, new Rect(hole.xMin, hole.yMin, width, hole.height), _highlight);
            Quad(vertices, new Rect(hole.xMax - width, hole.yMin, width, hole.height), _highlight);
        }

        private static void Quad(VertexHelper vertices, Rect rect, Color tint)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(new Vector3(rect.xMin, rect.yMin), tint, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMin, rect.yMax), tint, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMax), tint, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMin), tint, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }
    }
}
