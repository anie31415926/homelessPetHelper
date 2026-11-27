using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Affection
{
    internal sealed class DoudouAffectionMesh
    {
        [Serializable]
        private sealed class Layout
        {
            public int size;
            public int[] tail, tailCell, rootCell, face, smileCell, laughCell, hingeFrom, hingeTo;
            public float restTilt, swingLimit, liftLimit;
        }

        internal const float FlowerDuration = 1.2f;
        private const int Divisions = 16;
        private readonly Layout _art;
        private readonly int _width, _height;
        private readonly Vector3 _axis;
        private readonly Vector2 _hinge;
        private readonly Vector2 _white;
        private readonly Rect _body, _tail, _tailUV, _rootUV, _face, _smileUV, _laughUV;
        private readonly float _depthSlope;
        private static readonly Color32[] FlowerColors =
        {
            new Color32(224, 126, 150, 255), new Color32(240, 172, 111, 255),
            new Color32(239, 208, 115, 255), new Color32(144, 183, 146, 255),
            new Color32(131, 184, 202, 255), new Color32(184, 155, 205, 255)
        };

        internal DoudouAffectionMesh(TextAsset source, int width, int height)
        {
            _art = JsonUtility.FromJson<Layout>(source.text);
            _width = width;
            _height = height;
            if (_art == null || _art.size != 512 || width != 1024 || height != 512 ||
                _art.hingeFrom?.Length != 2 || _art.hingeTo?.Length != 2)
                throw new InvalidOperationException("Invalid Doudou layer layout.");
            _axis = new Vector3(_art.hingeTo[0] - _art.hingeFrom[0], _art.hingeFrom[1] - _art.hingeTo[1], 0).normalized;
            _hinge = new Vector2(_art.hingeTo[0], _art.hingeTo[1]);
            _depthSlope = Mathf.Tan(_art.restTilt);
            _white = Uv(new Vector2(width - 2, height - 2));
            _body = new Rect(0, 0, _art.size, _art.size);
            _tail = Read(_art.tail);
            _tailUV = Read(_art.tailCell);
            _rootUV = Read(_art.rootCell);
            _face = Read(_art.face);
            _smileUV = Read(_art.smileCell);
            _laughUV = Read(_art.laughCell);
        }

        private static Rect Read(int[] values) => new Rect(values[0], values[1], values[2], values[3]);
        private Vector2 Uv(Vector2 point) => new Vector2(point.x / _width, 1 - point.y / _height);
        private Vector2 Point(Rect view, Vector2 pixel) => view.center + new Vector2(
            pixel.x / _art.size - .5f, .5f - pixel.y / _art.size) * Mathf.Min(view.width, view.height);

        internal void Draw(VertexHelper mesh, Rect view, float smile, float laugh, float lean, float head,
            float wag, float flower, Color tint, DoudouTouchFur fur)
        {
            Layer(mesh, view, _body, _body, Divisions, lean, head, tint);
            fur?.Draw(this, mesh, view, lean, head, tint);
            if (smile > 0)
                Layer(mesh, view, _face, _smileUV, 6, lean, head, new Color(tint.r, tint.g, tint.b, tint.a * smile));
            if (laugh > 0)
                Layer(mesh, view, _face, _laughUV, 6, lean, head, new Color(tint.r, tint.g, tint.b, tint.a * laugh));
            Quaternion lift = Quaternion.AngleAxis(-_art.liftLimit * wag * wag * Mathf.Rad2Deg, Vector3.forward);
            Quaternion tailRotation = lift * Quaternion.AngleAxis(_art.swingLimit * wag * Mathf.Rad2Deg, _axis);
            Layer(mesh, view, _tail, _tailUV, 1, 0, 0, tint, tailRotation);
            Layer(mesh, view, _tail, _rootUV, 1, 0, 0, tint);
            if (flower >= 0 && flower < FlowerDuration) DrawFlowers(mesh, view, flower, tint.a);
        }

        internal void AddFurVertex(VertexHelper mesh, Rect view, Vector2 pixel, float lean, float head, Color tint) =>
            mesh.AddVert(Point(view, MoveHead(pixel, lean, head)), tint, Uv(pixel));

        private void Layer(VertexHelper mesh, Rect view, Rect draw, Rect sample, int divisions, float lean,
            float head, Color32 tint, Quaternion? tail = null)
        {
            int first = mesh.currentVertCount;
            for (int y = 0; y <= divisions; y++)
            {
                for (int x = 0; x <= divisions; x++)
                {
                    var uv = new Vector2((float)x / divisions, (float)y / divisions);
                    Vector2 pixel = draw.min + Vector2.Scale(draw.size, uv);
                    pixel = tail.HasValue ? MoveTail(pixel, tail.Value) : MoveHead(pixel, lean, head);
                    mesh.AddVert(Point(view, pixel), tint, Uv(sample.min + Vector2.Scale(sample.size, uv)));
                    if (x == 0 || y == 0) continue;
                    int i = mesh.currentVertCount - 1;
                    mesh.AddTriangle(i - divisions - 2, i - divisions - 1, i);
                    mesh.AddTriangle(i - divisions - 2, i, i - 1);
                }
            }
        }

        private Vector2 MoveTail(Vector2 point, Quaternion rotation)
        {
            var relative = new Vector3(point.x - _hinge.x, _hinge.y - point.y);
            relative.z = (_axis.x * relative.y - _axis.y * relative.x) * _depthSlope;
            relative = rotation * relative;
            return _hinge + new Vector2(relative.x, -relative.y);
        }

        private Vector2 MoveHead(Vector2 point, float lean, float intensity)
        {
            if (intensity == 0) return point;
            float x = point.x / _art.size, y = point.y / _art.size;
            float head = (1 - Smooth((y - .37f) / .20f)) * (1 - Smooth((x - .56f) / .09f));
            float ears = 1 - Smooth((y - .19f) / .14f);
            float tilt = intensity * lean * head;
            return point + _art.size * new Vector2(
                (.016f + .012f * ears) * tilt + (x - .385f) * .025f * intensity * head,
                -.027f * intensity * head + (x - .385f) * .082f * tilt + .019f * ears * intensity * head);
        }

        private static float Smooth(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3 - 2 * t);
        }

        private void DrawFlowers(VertexHelper mesh, Rect view, float age, float opacity)
        {
            float progress = age / FlowerDuration;
            float envelope = Smooth(progress * 7) * (1 - Smooth((progress - .58f) / .42f)) * opacity;
            for (int i = 0; i < FlowerColors.Length; i++)
            {
                var tint = FlowerColors[i];
                tint.a = (byte)(255 * envelope);
                var center = new Vector2(225 + i * 19 + (i - 2.5f) * 10 * progress,
                    116 - 90 * progress - Mathf.Sin(i * 1.75f) * 19);
                for (int petal = 0; petal < 5; petal++)
                {
                    float angle = petal * (2 * Mathf.PI / 5) + progress * 2;
                    Disc(mesh, view, center + 5 * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 4, tint);
                }
                Disc(mesh, view, center, 3, new Color32(255, 240, 172, tint.a));
            }
            for (int band = 0; band < 5; band++)
            {
                Color32 tint = FlowerColors[band];
                tint.a = (byte)(190 * envelope);
                Vector2 center = new Vector2(308, 124 - 32 * progress);
                float radius = 30 - band * 3;
                for (int j = 0; j < 14; j++)
                {
                    float angle = Mathf.Lerp(3.5f, 5.8f, j / 13f);
                    Disc(mesh, view, center + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 1.85f, tint);
                }
            }
        }

        private void Disc(VertexHelper mesh, Rect view, Vector2 center, float radius, Color32 tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(Point(view, center), tint, _white);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                var point = center + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(Point(view, point), tint, _white);
                mesh.AddTriangle(first, first + 1 + i, first + 1 + ((i + 1) % 8));
            }
        }
    }
}
