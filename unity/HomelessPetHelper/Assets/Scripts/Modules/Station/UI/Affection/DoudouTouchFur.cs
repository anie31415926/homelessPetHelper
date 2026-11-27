using System;
using HomelessPetHelper.Modules.Animals.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Affection
{
    internal sealed class DoudouTouchFur
    {
        private const int Columns = 12;
        private const int Rows = 7;
        private const float MinLift = .008f;
        private readonly FurProfile _profile;
        private readonly FurPose _pose;
        private readonly byte[] _edge;
        private readonly Vector2 _light;
        private bool _moving;

        public bool Visible { get; private set; }

        public DoudouTouchFur(TextAsset source)
        {
            _profile = FurProfile.Read(source);
            _pose = new FurPose(_profile.patches);
            _edge = Convert.FromBase64String(_profile.edge.data);
            float angle = _profile.light.angle * Mathf.Deg2Rad;
            _light = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Reset();
        }

        public void Stroke(Vector2 from, Vector2 to) => _moving |= _pose.Stroke(from, to);

        public void Reset()
        {
            _pose.Settle(FurMode.Pet);
            _moving = Visible = false;
        }

        public bool Advance(float seconds)
        {
            if (!_moving) return false;
            _moving = _pose.Advance(seconds * 1000);
            bool previous = Visible;
            Visible = false;
            for (int index = 0; index < _pose.Count && !Visible; index++)
                Visible = _pose[index].Lift > MinLift;
            return previous || Visible;
        }

        public void Draw(DoudouAffectionMesh body, VertexHelper mesh, Rect view, float lean, float head, Color tint)
        {
            if (!Visible) return;
            for (int index = 0; index < _pose.Count; index++)
            {
                var tuft = _pose[index];
                if (tuft.Lift <= MinLift) continue;
                AddPatch(body, mesh, view, lean, head, tint, _profile.patches[index], tuft);
            }
        }

        private void AddPatch(DoudouAffectionMesh body, VertexHelper mesh, Rect view, float lean, float head,
            Color tint, FurProfile.Patch patch, FurPose.Tuft tuft)
        {
            int first = mesh.currentVertCount;
            float length = patch.length;
            float bend = tuft.Lift * (.38f + .23f * tuft.Curl);
            float facing = .5f - .5f * Vector2.Dot(_light, patch.Grain);
            float cover = Smooth(0, .21f, tuft.Lift);
            float shadow = Smooth(0, .58f, tuft.Lift) * _profile.light.shadow;
            float spread = 2 + tuft.Lift * (.20f + .08f * tuft.Curl) * length;
            for (int column = 0; column <= Columns; column++)
            {
                float across = (float)column / Columns;
                float side = Smooth(0, .19f, across) * (1 - Smooth(.81f, 1, across));
                var ink = EdgeInk(patch, across);
                float point = Mathf.Lerp(.18f, .69f + (ink.x - .44f) * patch.toothDepth * 1.6f,
                    Smooth(0, .24f, across) * (1 - Smooth(.77f, 1, across)));
                float lip = point * length * (1 - bend * (.61f + .51f * Mathf.Sin(across * Mathf.PI)));
                Vector2 root = patch.Root + patch.Cross * ((across - .5f) * patch.width);
                for (int row = 0; row < Rows; row++)
                {
                    float reach = Mathf.Min(row, 4) * .25f;
                    float along = row <= 4 ? lip * reach : lip + (row == 5 ? Mathf.Min(1, spread * .25f) : spread);
                    float fold = (.02f + .05f * tuft.Curl) * Mathf.Sin(reach * Mathf.PI) * facing
                        - (.08f * tuft.Curl * (1 - facing) + .13f * ink.y) * Smooth(.62f, 1, reach);
                    float tone = row <= 4 ? 1 + tuft.Lift * fold : .09f;
                    float alpha = row <= 4 ? cover * Smooth(0, .24f, along / length) : row == 5 ? shadow * .3f : 0;
                    var vertexTint = new Color(tint.r * tone, tint.g * tone, tint.b * tone, tint.a * side * alpha);
                    body.AddFurVertex(mesh, view, root + patch.Grain * along, lean, head, vertexTint);
                    if (column == 0 || row == 0) continue;
                    int vertex = first + column * Rows + row;
                    mesh.AddTriangle(vertex - Rows - 1, vertex - Rows, vertex);
                    mesh.AddTriangle(vertex - Rows - 1, vertex, vertex - 1);
                }
            }
        }

        private Vector2 EdgeInk(FurProfile.Patch patch, float across)
        {
            var edge = _profile.edge;
            float span = Mathf.Lerp(.46f, 1, Mathf.Clamp01((patch.teeth - 2) / 14));
            float phase = Mathf.Repeat(patch.seed * edge.rows, 1);
            float sample = Mathf.Clamp01(across * span + (1 - span) * phase) * (edge.width - 1);
            int row = Mathf.Min(edge.rows - 1, (int)(patch.seed * edge.rows));
            int left = (row * edge.width + (int)sample) * 2;
            int right = Mathf.Min((row + 1) * edge.width * 2 - 2, left + 2);
            float fraction = sample - (int)sample;
            return new Vector2(Mathf.Lerp(_edge[left], _edge[right], fraction),
                Mathf.Lerp(_edge[left + 1], _edge[right + 1], fraction)) / 255;
        }

        private static float Smooth(float min, float max, float value)
        {
            float amount = Mathf.InverseLerp(min, max, value);
            return amount * amount * (3 - 2 * amount);
        }
    }
}
