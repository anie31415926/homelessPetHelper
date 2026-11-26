using HomelessPetHelper.Framework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Siege.UI
{
    // Small procedural props share a paint vocabulary, not a combat implementation.
    internal static class SiegeTokenArt
    {
        private static readonly Color Ink = new Color32(57, 74, 73, 255);
        private static readonly Color FishColor = new Color32(245, 168, 105, 255);
        private static readonly Color YarnColor = new Color32(229, 119, 139, 255);
        private static readonly Color FeatherColor = new Color32(104, 188, 177, 255);
        private static readonly Color BoneColor = new Color32(255, 241, 202, 255);
        private static readonly Color DiscColor = new Color32(94, 165, 212, 255);
        private static readonly Color RopeColor = new Color32(224, 173, 80, 255);

        internal static bool Supports(string visual) => visual == "fish" || visual == "bouquet"
            || visual == "yarn" || visual == "yarnLion" || visual == "feather" || visual == "pinwheel"
            || visual == "bone" || visual == "boneWheel" || visual == "disc" || visual == "triDisc"
            || visual == "rope" || visual == "ropeOctopus";

        internal static bool SupportsTower(string visual) => visual == "paper" || visual == "thread"
            || visual == "scratch" || visual == "kennel" || visual == "bubbles" || visual == "slippers";

        internal static void DrawSymbol(VertexHelper mesh, SiegeResourceGraphic.Symbol symbol,
            Vector2 center, float size, Color ink)
        {
            var paint = new Paint(mesh, center, size);
            switch (symbol)
            {
                case SiegeResourceGraphic.Symbol.Supply:
                    paint.Ellipse(0, .30f, .84f, .51f, ink);
                    paint.Ellipse(0, .30f, .72f, .40f, BoneColor);
                    paint.Ellipse(0, -.17f, .88f, .58f, ink);
                    paint.Ellipse(0, -.17f, .76f, .45f, FishColor);
                    paint.Ellipse(0, .17f, .85f, .20f, ink);
                    paint.Ellipse(0, .21f, .72f, .12f, BoneColor);
                    paint.Box(0, -.75f, .65f, .14f, ink);
                    break;
                case SiegeResourceGraphic.Symbol.Damage:
                    paint.Line(-.55f, -.62f, .43f, .36f, .24f, ink);
                    var hammer = paint.Child(.35f, .42f, 1, -.78f);
                    hammer.Box(0, 0, 1.16f, .64f, ink);
                    hammer.Box(0, 0, .95f, .42f, FishColor);
                    break;
                case SiegeResourceGraphic.Symbol.Cadence:
                    paint.Disc(0, 0, .78f, ink);
                    paint.Disc(0, 0, .62f, BoneColor);
                    paint.Box(0, .84f, .48f, .20f, ink);
                    paint.Line(0, 0, 0, .40f, .13f, ink);
                    paint.Line(0, 0, .29f, -.10f, .13f, ink);
                    break;
                case SiegeResourceGraphic.Symbol.Range:
                    paint.Line(-.83f, 0, .83f, 0, .17f, ink);
                    paint.Line(-.83f, 0, -.44f, .38f, .17f, ink);
                    paint.Line(-.83f, 0, -.44f, -.38f, .17f, ink);
                    paint.Line(.83f, 0, .44f, .38f, .17f, ink);
                    paint.Line(.83f, 0, .44f, -.38f, .17f, ink);
                    break;
                case SiegeResourceGraphic.Symbol.Durability:
                    paint.Box(0, .26f, 1.40f, .95f, ink);
                    paint.Triangle(-.70f, -.20f, .70f, -.20f, 0, -.86f, ink);
                    paint.Box(0, .24f, 1.10f, .67f, FeatherColor);
                    paint.Triangle(-.55f, -.09f, .55f, -.09f, 0, -.65f, FeatherColor);
                    break;
            }
        }

        internal static void Paw(VertexHelper mesh, Vector2 center, float size, SceneSpritePalette palette = null)
        {
            if (palette != null && palette.TryDraw(mesh, "paw", center, Vector2.one * size * 2)) return;
            var p = new Paint(mesh, center, size);
            var fur = new Color32(250, 204, 148, 235);
            p.Ellipse(0, 0, .85f, .7f, Ink);
            p.Ellipse(0, 0, .75f, .6f, fur);
            for (int i = -1; i <= 1; i++)
            {
                p.Disc(i * .55f, .55f, .36f, Ink);
                p.Disc(i * .55f, .55f, .28f, fur);
                p.Disc(i * .55f, .6f, .1f, YarnColor);
            }
            p.Ellipse(0, -.05f, .35f, .25f, YarnColor);
        }

        internal static void Draw(VertexHelper mesh, string visual, Vector2 center, float size,
            float phase, bool braced = true, SceneSpritePalette palette = null)
        {
            if (palette != null && palette.TryDraw(mesh, visual, center, Vector2.one * size * 2,
                Mathf.Sin(phase) * .12f)) return;
            var p = new Paint(mesh, center, size);
            switch (visual)
            {
                case "fish": Fish(ref p, 0, 0, .9f, Mathf.Sin(phase) * .18f); break;
                case "bouquet":
                    for (int i = -1; i <= 1; i++) Fish(ref p, i * .55f, .1f, .7f, i * (braced ? -.55f : -.2f));
                    p.Line(-.25f, -.7f, .25f, -.7f, .17f, YarnColor); break;
                case "yarn": Yarn(ref p, 0, 0, .85f, phase); break;
                case "yarnLion":
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        p.Disc(Mathf.Cos(a) * .58f, Mathf.Sin(a) * .58f, .35f, YarnColor);
                    }
                    p.Disc(0, 0, .6f, BoneColor);
                    p.Disc(-.2f, .14f, .07f, Ink); p.Disc(.2f, .14f, .07f, Ink);
                    p.Triangle(-.1f, -.08f, .1f, -.08f, 0, -.2f, Ink);
                    p.Line(-.9f, -.5f, -.5f, -.45f, .08f, YarnColor); break;
                case "feather": Feather(ref p, 0, 0, 1, Mathf.Sin(phase) * .22f); break;
                case "pinwheel":
                    for (int i = 0; i < 3; i++) Feather(ref p, 0, 0, .85f, phase + i * Mathf.PI * 2 / 3);
                    p.Disc(0, 0, .17f, RopeColor); break;
                case "bone": Bone(ref p, 0, 0, .9f, Mathf.Sin(phase) * .45f); break;
                case "boneWheel":
                    for (int i = 0; i < 3; i++) Bone(ref p, 0, 0, .82f, phase + i * Mathf.PI / 3);
                    p.Disc(0, 0, .16f, YarnColor); break;
                case "disc": Disc(ref p, 0, 0, 1, phase); break;
                case "triDisc":
                    for (int i = 0; i < 3; i++)
                    {
                        float a = phase + i * Mathf.PI * 2 / 3;
                        Disc(ref p, Mathf.Cos(a) * .48f, Mathf.Sin(a) * .48f, .62f, a);
                    }
                    p.Disc(0, 0, .16f, BoneColor); break;
                case "rope": Rope(ref p, 0, 0, 1, phase); break;
                case "ropeOctopus":
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3;
                        p.Line(Mathf.Cos(a) * .35f, Mathf.Sin(a) * .35f,
                            Mathf.Cos(a + .15f * Mathf.Sin(phase)) * .95f,
                            Mathf.Sin(a + .15f * Mathf.Sin(phase)) * .95f, .16f, RopeColor);
                    }
                    Rope(ref p, 0, 0, .65f, phase); break;
            }
        }

        internal static void Tower(VertexHelper mesh, string visual, Vector2 center, float size,
            bool winding, SceneSpritePalette palette = null)
        {
            if (palette != null && palette.TryDraw(mesh, visual, center,
                new Vector2(2.2f, winding ? 1.8f : 2.2f) * size)) return;
            var p = new Paint(mesh, center, size);
            float squeeze = winding ? .25f : 0;
            p.Ellipse(0, -.75f, 1.1f, .22f, new Color32(57, 74, 73, 40));
            switch (visual)
            {
                case "paper":
                    p.Box(0, -.1f, 1.5f, 1.1f, FishColor);
                    p.Line(-.75f, .45f, -.95f, .7f, .12f, Ink);
                    p.Line(.75f, .45f, .95f, .7f, .12f, Ink);
                    p.Box(0, .55f, .4f, .9f, BoneColor); p.Disc(0, 1, .22f, Ink); break;
                case "thread":
                    p.Box(0, -.55f, 1.8f, .3f, FishColor);
                    p.Line(-.6f, -.5f, -.6f, .6f, .18f, Ink);
                    p.Line(.6f, -.5f, .6f, .6f, .18f, Ink);
                    Yarn(ref p, 0, .4f, .55f, 0); break;
                case "scratch":
                    p.Box(0, -.4f, 1.7f, .45f, FishColor);
                    p.Line(-.7f, -.4f, .6f, .65f - squeeze, .35f, RopeColor);
                    p.Ellipse(.55f, .65f - squeeze, .5f, .27f, YarnColor);
                    for (int i = -1; i <= 1; i++) p.Disc(.55f + i * .25f, .95f - squeeze, .17f, YarnColor);
                    break;
                case "kennel":
                    p.Box(0, -.15f, 1.5f, 1.2f, FeatherColor);
                    p.Triangle(-1, .4f, 0, 1.15f, 1, .4f, YarnColor);
                    p.Disc(0, -.05f, .43f, Ink); p.Disc(0, -.05f, .23f, BoneColor); break;
                case "bubbles":
                    p.Ellipse(0, -.3f, .95f, .4f, DiscColor);
                    p.Ellipse(0, -.1f, .8f, .25f, new Color32(187, 229, 237, 255));
                    p.Disc(-.25f, .4f, .28f, FeatherColor); p.Disc(.3f, .75f, .22f, DiscColor);
                    p.Disc(-.3f, .47f, .07f, Color.white); break;
                case "slippers":
                    p.Triangle(-.35f, -.7f, 0, -.1f, .35f, -.7f, RopeColor);
                    p.Line(-.9f, .3f - squeeze, .9f, -.1f + squeeze, .22f, Ink);
                    p.Ellipse(-.8f, .4f - squeeze, .38f, .23f, YarnColor);
                    p.Ellipse(.8f, .02f + squeeze, .38f, .23f, DiscColor); break;
            }
        }

        private static void Fish(ref Paint p, float x, float y, float size, float angle)
        {
            var q = p.Child(x, y, size, angle);
            q.Ellipse(.1f, .05f, .85f, .43f, Ink);
            q.Ellipse(.12f, .05f, .75f, .34f, FishColor);
            q.Triangle(-.55f, 0, -1.05f, .4f, -1.05f, -.4f, FishColor);
            q.Disc(.62f, .13f, .08f, Ink);
            q.Line(-.15f, -.22f, .1f, .27f, .07f, BoneColor);
        }

        private static void Yarn(ref Paint p, float x, float y, float size, float phase)
        {
            var q = p.Child(x, y, size, phase * .2f);
            q.Disc(0, 0, 1, Ink); q.Disc(0, 0, .89f, YarnColor);
            for (int i = -1; i <= 1; i++) q.Line(-.65f, i * .22f, .65f, i * .22f + .35f, .07f, BoneColor);
            q.Line(.5f, -.6f, 1.2f, -.8f, .12f, YarnColor);
        }

        private static void Feather(ref Paint p, float x, float y, float size, float angle)
        {
            var q = p.Child(x, y, size, angle);
            q.Triangle(-.45f, -.2f, 0, 1.05f, .5f, .15f, FeatherColor);
            q.Triangle(-.45f, -.2f, .5f, .15f, 0, -.75f, DiscColor);
            q.Line(0, -.95f, 0, .8f, .09f, Ink);
            q.Line(0, -.25f, .3f, .1f, .06f, BoneColor);
        }

        private static void Bone(ref Paint p, float x, float y, float size, float angle)
        {
            var q = p.Child(x, y, size, angle);
            q.Box(0, 0, 1.35f, .43f, Ink); q.Box(0, 0, 1.35f, .3f, BoneColor);
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                {
                    q.Disc(i * .7f, j * .18f, .31f, Ink);
                    q.Disc(i * .7f, j * .18f, .23f, BoneColor);
                }
        }

        private static void Disc(ref Paint p, float x, float y, float size, float angle)
        {
            var q = p.Child(x, y, size, angle * .25f);
            q.Ellipse(0, 0, 1, .55f, Ink); q.Ellipse(0, .05f, .9f, .43f, DiscColor);
            q.Ellipse(0, .1f, .6f, .25f, BoneColor); q.Ellipse(0, .1f, .44f, .17f, DiscColor);
        }

        private static void Rope(ref Paint p, float x, float y, float size, float phase)
        {
            var q = p.Child(x, y, size, Mathf.Sin(phase) * .15f);
            q.Line(-1, -.5f, 1, .5f, .22f, RopeColor);
            q.Line(-1, .5f, 1, -.5f, .22f, RopeColor);
            q.Disc(0, 0, .48f, Ink); q.Disc(0, 0, .37f, RopeColor);
            q.Line(-.18f, -.25f, .18f, .25f, .12f, BoneColor);
        }

        private struct Paint
        {
            private readonly VertexHelper _mesh;
            private readonly Vector2 _center;
            private readonly float _size, _angle;

            internal Paint(VertexHelper mesh, Vector2 center, float size, float angle = 0)
            { _mesh = mesh; _center = center; _size = size; _angle = angle; }

            internal Paint Child(float x, float y, float size, float angle) =>
                new Paint(_mesh, Point(x, y), _size * size, _angle + angle);

            private Vector2 Point(float x, float y) => _center + new Vector2(
                x * Mathf.Cos(_angle) - y * Mathf.Sin(_angle),
                x * Mathf.Sin(_angle) + y * Mathf.Cos(_angle)) * _size;

            internal void Triangle(float ax, float ay, float bx, float by, float cx, float cy, Color color)
            {
                int start = _mesh.currentVertCount;
                _mesh.AddVert(Point(ax, ay), color, Vector2.zero);
                _mesh.AddVert(Point(bx, by), color, Vector2.zero);
                _mesh.AddVert(Point(cx, cy), color, Vector2.zero);
                _mesh.AddTriangle(start, start + 1, start + 2);
            }

            internal void Box(float x, float y, float width, float height, Color color)
            {
                Triangle(x - width / 2, y - height / 2, x - width / 2, y + height / 2,
                    x + width / 2, y + height / 2, color);
                Triangle(x - width / 2, y - height / 2, x + width / 2, y + height / 2,
                    x + width / 2, y - height / 2, color);
            }

            internal void Disc(float x, float y, float radius, Color color) =>
                Ellipse(x, y, radius, radius, color);

            internal void Ellipse(float x, float y, float rx, float ry, Color color)
            {
                for (int i = 0; i < 18; i++)
                {
                    float a = i * Mathf.PI * 2 / 18, b = (i + 1) * Mathf.PI * 2 / 18;
                    Triangle(x, y, x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry,
                        x + Mathf.Cos(b) * rx, y + Mathf.Sin(b) * ry, color);
                }
            }

            internal void Line(float ax, float ay, float bx, float by, float width, Color color)
            {
                var n = new Vector2(-(by - ay), bx - ax).normalized * width / 2;
                Triangle(ax - n.x, ay - n.y, ax + n.x, ay + n.y, bx + n.x, by + n.y, color);
                Triangle(ax - n.x, ay - n.y, bx + n.x, by + n.y, bx - n.x, by - n.y, color);
            }
        }
    }
}
