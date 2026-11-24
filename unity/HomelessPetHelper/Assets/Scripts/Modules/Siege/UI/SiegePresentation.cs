using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.UI;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege.UI
{
    // Presentation only. All positions, outcomes and time come from the battle snapshot.
    internal sealed class SiegePresentation
    {
        private const int TossTravelMs = 90;
        private const int MoveGraceMs = 80;
        internal const int MaxTailMs = 430;
        private static readonly Color Paper = new Color32(251, 231, 184, 255);
        private static readonly Color Cardboard = new Color32(155, 99, 66, 255);
        private static readonly Color Dust = new Color32(252, 222, 164, 255);
        private static readonly Color Brush = new Color32(183, 92, 70, 255);
        private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>(96);
        private readonly List<int> _stale = new List<int>(96);
        private string _session;
        private int _sample;
        private int _startAudioThrough;
        private int _impactAudioThrough;
        private int _retireAudioThrough;

        private struct Track
        {
            public SiegePoint Point;
            public int Seen, MovedAt;
            public float Phase, Facing;
            public Effect Deploy, Strike, Hit;
        }

        public void Reset()
        {
            _session = null;
            _sample = _startAudioThrough = _impactAudioThrough = _retireAudioThrough = 0;
            _tracks.Clear();
        }

        public void Observe(SiegeCatalog catalog, SiegeState state, int audioNow, Action<string> play)
        {
            bool initial = _session != state.SessionId || _sample == 0;
            if (initial)
            {
                Reset();
                _session = state.SessionId;
                _startAudioThrough = _impactAudioThrough = _retireAudioThrough = state.LatestEffectSequence;
            }
            _sample++;
            for (int i = 0; i < state.Units.Count; i++)
                TrackUnit(catalog, state.Units[i], state.ElapsedMs);
            for (int i = 0; i < state.Effects.Count; i++)
            {
                var effect = state.Effects[i];
                TrackEffect(effect);
                if (!initial && !state.Paused && play != null) Play(effect, audioNow, play);
            }
            if (_tracks.Count == state.Units.Count) return;
            _stale.Clear();
            foreach (var entry in _tracks)
                if (entry.Value.Seen != _sample) _stale.Add(entry.Key);
            for (int i = 0; i < _stale.Count; i++) _tracks.Remove(_stale[i]);
        }

        private void TrackUnit(SiegeCatalog catalog, Unit unit, int now)
        {
            var point = SiegeGeometry.Position(catalog, unit);
            if (!_tracks.TryGetValue(unit.Id, out var track))
                track.MovedAt = now - MoveGraceMs;
            else if (!unit.IsCarried)
            {
                float distance = (float)point.Distance(track.Point);
                if (distance > .01f)
                {
                    track.Phase = (track.Phase + distance / 3f) % (Mathf.PI * 2);
                    if (point.X != track.Point.X) track.Facing = point.X >= track.Point.X ? 1 : -1;
                    track.MovedAt = now;
                }
            }
            track.Point = point;
            track.Seen = _sample;
            // Effect instances belong to a reusable snapshot, so only use this sample's references.
            track.Deploy = track.Strike = track.Hit = null;
            _tracks[unit.Id] = track;
        }

        private void TrackEffect(Effect effect)
        {
            if (effect.UnitId == 0 || !_tracks.TryGetValue(effect.UnitId, out var track)) return;
            switch (effect.Kind)
            {
                case EffectKind.Deploy: track.Deploy = effect; break;
                case EffectKind.UnitShot: track.Strike = effect; break;
                case EffectKind.TowerShot: track.Hit = effect; break;
                default: return;
            }
            _tracks[effect.UnitId] = track;
        }

        private void Play(Effect effect, int now, Action<string> play)
        {
            bool paper = effect.Kind == EffectKind.TowerShot && effect.Visual == "paper";
            if (effect.Sequence > _startAudioThrough)
            {
                _startAudioThrough = effect.Sequence;
                switch (effect.Kind)
                {
                    case EffectKind.Deploy: play("siege.toy-drop"); break;
                    case EffectKind.TowerShot when paper: play("siege.paper-shot"); break;
                    case EffectKind.TowerShot when effect.Visual == "kennel": play("siege.kennel-ball"); break;
                    case EffectKind.TowerLost: play("siege.toy-retire"); break;
                    case "merge": play("game.merge"); break;
                }
            }
            if (now - effect.StartMs < TossTravelMs) return;
            if (paper && effect.Sequence > _impactAudioThrough)
            {
                _impactAudioThrough = effect.Sequence;
                play("siege.paper-impact");
            }
            if (effect.Kind == EffectKind.UnitLost && effect.Sequence > _retireAudioThrough)
            {
                _retireAudioThrough = effect.Sequence;
                play("siege.toy-retire");
            }
        }

        public void DrawUnit(VertexHelper mesh, SceneSpritePalette palette, Unit unit, string visual,
            Vector2 center, float size, int now)
        {
            if (!_tracks.TryGetValue(unit.Id, out var track)) return;
            size *= 1.12f;
            bool moving = now - track.MovedAt < MoveGraceMs && unit.HeldUntilMs <= now;
            float phase = track.Phase + unit.Id * .7f;
            float wave = moving ? Mathf.Sin(phase) : 0;
            float hop = 0, angle = 0, stretch = 1;
            switch (visual)
            {
                case "yarn": case "yarnLion": case "pinwheel": case "boneWheel": case "triDisc":
                    angle = phase * (track.Facing < 0 ? 1 : -1); hop = Mathf.Abs(wave) * .08f; break;
                case "feather":
                    angle = wave * .45f; hop = .18f + (wave + 1) * .13f; break;
                case "bone": case "fish": case "bouquet":
                    angle = wave * .33f; hop = Mathf.Abs(wave) * .28f; break;
                default:
                    angle = wave * .23f; hop = Mathf.Abs(wave) * .14f; break;
            }
            if (track.Deploy != null)
            {
                float p = Progress(track.Deploy, now);
                hop += (1 - p) * (1 - p) * .85f;
                stretch += Mathf.Sin(p * Mathf.PI * 2) * (1 - p) * .28f - (1 - p) * .3f;
            }
            DrawShadow(mesh, center + Vector2.down * size * .54f, size, hop);
            Vector2 shift = Vector2.zero;
            if (track.Strike != null)
            {
                var strike = track.Strike;
                var direction = new Vector2((float)(strike.To.X - strike.From.X),
                    (float)(strike.From.Y - strike.To.Y)).normalized;
                shift = direction * Mathf.Sin(Progress(strike, now) * Mathf.PI) * size * .22f;
            }
            Color tint = Color.white;
            if (track.Hit != null)
            {
                float hit = Mathf.Clamp01((now - track.Hit.StartMs - TossTravelMs) / 125f);
                float jolt = Mathf.Sin(hit * Mathf.PI);
                angle += jolt * .48f;
                stretch -= jolt * .2f;
                tint = Color.Lerp(Color.white, Paper, jolt * .6f);
            }
            DrawSprite(mesh, palette, visual, center + Vector2.up * (hop * size) + shift, size * stretch, angle,
                tint);
        }

        public void DrawTower(VertexHelper mesh, SceneSpritePalette palette, Tower tower, string visual,
            Vector2 point, float size, int now, int period)
        {
            float until = tower.NextAttackAtMs - now;
            float since = period - until;
            float recoil = tower.NextAttackAtMs == 0 || tower.Status != TowerStatus.Active
                ? 0 : 1 - Mathf.Clamp01(since / 145f);
            // A loaded pose is not a new warning or additional gameplay windup.
            float loaded = tower.Status != TowerStatus.Active ? 0 :
                tower.WindingUp ? 1 : until > 0 ? 1 - Mathf.Clamp01(until / 200f) : 0;
            float cock = loaded * (1 - recoil);
            Vector2 pose = point + Vector2.up * size * (recoil * .29f - cock * .10f);
            DrawShadow(mesh, point + Vector2.down * size * .55f, size, recoil);
            if (palette == null || !palette.TryDraw(mesh, visual, pose,
                Vector2.one * (2.2f * size * (1 - cock * .12f + recoil * .12f)), .22f * recoil - .12f * cock))
                SiegeTokenArt.Tower(mesh, visual, pose, size, tower.WindingUp);
            if (visual == "paper" && recoil > 0)
                DrawPaperFlap(mesh, pose, size, recoil);
        }

        private static void DrawPaperFlap(VertexHelper mesh, Vector2 pose, float size, float recoil)
        {
            Vector2 hinge = pose + new Vector2(-.24f, .53f) * size;
            Vector2 width = Vector2.right * size * .46f;
            Vector2 lifted = new Vector2(-.34f, .46f) * (size * recoil);
            Quad(mesh, hinge, hinge + lifted, hinge + width + lifted * .7f, hinge + width,
                Fade(Cardboard, recoil * .8f));
        }

        public bool DrawEffect(VertexHelper mesh, SceneSpritePalette palette, Effect effect,
            Vector2 from, Vector2 to, Vector2 axes, float size, int now)
        {
            float p = Progress(effect, now);
            switch (effect.Kind)
            {
                case EffectKind.Deploy:
                    Chips(mesh, from, Vector2.up * .35f, size * .7f, p, Dust, effect.Sequence, 4); return true;
                case EffectKind.UnitLost:
                    int vanish = effect.StartMs + TossTravelMs;
                    float fade = Mathf.Clamp01((now - vanish) / (float)(effect.UntilMs - vanish));
                    DrawRetirement(mesh, palette, effect, from, size * .83f, fade, now >= vanish); return true;
                case EffectKind.TowerLost:
                    DrawRetirement(mesh, palette, effect, from, size, p, true); return true;
                case EffectKind.UnitShot:
                    Chips(mesh, to, (to - from).normalized * .3f, size * .65f, p,
                        Cardboard, effect.Sequence, 3); return true;
                case EffectKind.TowerShot when effect.Visual != null && effect.Visual != "legacy":
                    DrawToss(mesh, effect, from, to, size, now - effect.StartMs); return true;
                case "sweep":
                case "push":
                    DrawBrush(mesh, from, axes, p, effect.Kind == "push"); return true;
                default: return false;
            }
        }

        private static void DrawRetirement(VertexHelper mesh, SceneSpritePalette palette, Effect effect, Vector2 from,
            float size, float p, bool dust)
        {
            if (p >= 1) return;
            DrawSprite(mesh, palette, effect.Visual, from + Vector2.up * size * .5f * p,
                size * (1 - .7f * p), p * .7f, Fade(Color.white, 1 - p));
            if (dust) Chips(mesh, from, Vector2.up * .55f, size, p, Paper, effect.Sequence, 6);
        }

        private static void DrawToss(VertexHelper mesh, Effect effect, Vector2 from, Vector2 to, float size, int age)
        {
            bool crumpled = effect.Visual == "paper";
            Color material = effect.Visual switch
            {
                "kennel" => new Color32(215, 144, 95, 255),
                "thread" => new Color32(218, 141, 148, 255),
                "bubbles" => new Color32(164, 219, 226, 220),
                _ => Paper
            };
            float travel = Mathf.Clamp01(age / (float)TossTravelMs);
            from += Vector2.up * size * .37f;
            Chips(mesh, from, (to - from).normalized * .6f, size * .6f,
                Mathf.Clamp01(age / 145f), material, effect.Sequence, 3);
            if (travel < 1)
            {
                Vector2 point = Vector2.Lerp(from, to, travel) + Vector2.up * Mathf.Sin(travel * Mathf.PI) * size;
                Polygon(mesh, point, new Vector2(.42f, .36f) * size, travel * 4, Cardboard, crumpled ? 7 : 10);
                Polygon(mesh, point + Vector2.up * size * .04f, Vector2.one * size * .30f, travel * 4,
                    material, crumpled ? 6 : 9);
                if (crumpled)
                    Polygon(mesh, point + new Vector2(.02f, .09f) * size, new Vector2(.16f, .10f) * size,
                        travel * 4, Color.Lerp(material, Cardboard, .5f), 3);
                return;
            }
            Chips(mesh, to, (to - from).normalized * .55f, size,
                Mathf.Clamp01((age - TossTravelMs) / 170f), material, effect.Sequence + 23, crumpled ? 6 : 4);
        }

        private static void DrawBrush(VertexHelper mesh, Vector2 center, Vector2 axes, float p, bool push)
        {
            for (int i = 0; i < 3; i++)
            {
                float slice = Mathf.Clamp01(p * 1.45f - i * .14f);
                if (slice <= 0 || slice >= 1) continue;
                Vector2 peak = center + Vector2.Scale(new Vector2((slice * 2 - 1) * (push ? .68f : .39f),
                    (i - 1) * .15f), axes);
                Vector2 wing = Vector2.Scale(new Vector2((push ? .22f : .53f), .20f - i * .035f), axes);
                Quad(mesh, peak - wing, peak + new Vector2(-wing.x * .8f, wing.y),
                    peak + new Vector2(wing.x, wing.y * .34f), peak + new Vector2(wing.x * .45f, -wing.y * .6f),
                    Fade(Brush, Mathf.Sin(slice * Mathf.PI) * (.72f - i * .13f)));
            }
        }

        private static void Chips(VertexHelper mesh, Vector2 center, Vector2 bias, float size, float p,
            Color color, int seed, int count)
        {
            if (p >= 1) return;
            float spread = 1 - (1 - p) * (1 - p);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f + seed % 17 * .51f;
                Vector2 ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 point = center + (ray + bias) * size * (.18f + spread * (1.15f + i % 3 * .27f));
                point.y -= size * p * p * .55f;
                Vector2 chip = new Vector2(.35f, .18f) * (size * (1 - p * .45f));
                Polygon(mesh, point, chip, angle + p * 2.4f,
                    Fade(i % 3 == 1 ? Cardboard : Color.Lerp(color, Cardboard, .24f), (1 - p) * .92f), 4);
            }
        }

        private static void DrawShadow(VertexHelper mesh, Vector2 center, float size, float height) =>
            Polygon(mesh, center, new Vector2(.74f, .22f) * size * (1 - Mathf.Min(.3f, height * .35f)),
                0, Fade(new Color32(73, 58, 41, 96), 1 - Mathf.Min(.55f, height * .35f)), 8);

        private static void DrawSprite(VertexHelper mesh, SceneSpritePalette palette, string visual,
            Vector2 center, float size, float angle, Color tint)
        {
            if (palette != null && palette.TryDraw(mesh, visual, center, Vector2.one * size * 2, angle, tint))
                return;
            if (tint.a >= 1) SiegeTokenArt.Draw(mesh, visual, center, size, angle);
        }

        private static float Progress(Effect effect, int now) =>
            Mathf.Clamp01((now - effect.StartMs) / (float)Math.Max(1, effect.UntilMs - effect.StartMs));

        private static Color Fade(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        private static void Polygon(VertexHelper mesh, Vector2 center, Vector2 radii, float angle,
            Color color, int count)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < count; i++)
            {
                float a = angle + i * Mathf.PI * 2 / count;
                mesh.AddVert(center + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), radii),
                    color, Vector2.zero);
                mesh.AddTriangle(start, start + i + 1, start + (i + 1) % count + 1);
            }
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero); mesh.AddVert(d, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
