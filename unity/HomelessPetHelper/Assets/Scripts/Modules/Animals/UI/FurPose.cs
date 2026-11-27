using UnityEngine;

namespace HomelessPetHelper.Modules.Animals.UI
{
    public enum FurMode { Brush, Pet }

    public sealed class FurPose
    {
        public struct Tuft
        {
            public float Roughness, Lift, Curl, Flick, Linger;
        }

        private readonly FurProfile.Patch[] _patches;
        private readonly Tuft[] _tufts;
        public FurMode Mode { get; private set; }
        public int Count => _tufts.Length;
        public Tuft this[int index] => _tufts[index];

        public FurPose(FurProfile.Patch[] patches)
        {
            _patches = patches;
            _tufts = new Tuft[patches.Length];
            Settle(FurMode.Brush, true);
        }

        public void Settle(FurMode mode, bool reset = false)
        {
            Mode = mode;
            for (int i = 0; i < Count; i++)
            {
                ref var tuft = ref _tufts[i];
                if (reset) tuft.Roughness = 1;
                tuft.Flick = tuft.Linger = 0;
                tuft.Lift = mode == FurMode.Brush ? _patches[i].initial * tuft.Roughness : 0;
                tuft.Curl = mode == FurMode.Brush ? _patches[i].roll * Mathf.Sqrt(tuft.Roughness) : 0;
            }
        }

        public bool Stroke(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1) return false;
            var direction = delta / distance;
            bool changed = false;
            for (int i = 0; i < Count; i++)
            {
                var patch = _patches[i];
                float alignment = Vector2.Dot(patch.Grain, direction);
                changed |= Mode == FurMode.Pet
                    ? Pet(patch, ref _tufts[i], from, direction, distance, alignment)
                    : Brush(patch, ref _tufts[i], from, delta, distance, alignment);
            }
            return changed;
        }

        private static bool Pet(FurProfile.Patch patch, ref Tuft tuft, Vector2 from, Vector2 direction, float distance, float alignment)
        {
            var center = patch.Root + patch.Grain * (patch.length * .43f) - from;
            float radius = 14 + patch.width * .49f;
            float cross = center.x * direction.y - center.y * direction.x;
            float span = radius * radius - cross * cross;
            if (span <= 0) return false;
            float reach = Mathf.Sqrt(span);
            float along = Vector2.Dot(center, direction);
            float start = Mathf.Max(-along, -reach), end = Mathf.Min(distance - along, reach);
            if (start >= end) return false;
            float contact = (span * (end - start) - (end * end * end - start * start * start) / 3) / (radius * radius);
            float strength = .10f + .90f * Smooth((.82f - alignment) / 1.82f);
            tuft.Flick = Mathf.Clamp01(tuft.Flick + contact * strength / 24);
            tuft.Linger = Mathf.Max(tuft.Linger, (44 + 45 * patch.seed) * strength);
            return true;
        }

        private static bool Brush(FurProfile.Patch patch, ref Tuft tuft, Vector2 from, Vector2 delta, float distance, float alignment)
        {
            if (Mathf.Abs(alignment) <= .32f) return false;
            int samples = Mathf.Min(64, Mathf.CeilToInt(distance / 7));
            float contact = 0;
            for (int sample = 0; sample < samples; sample++)
            {
                var offset = from + delta * ((sample + .5f) / samples) - patch.Root;
                float forward = Vector2.Dot(offset, patch.Grain);
                float across = Mathf.Abs(Vector2.Dot(offset, patch.Cross));
                contact += Smooth(1 - across / (patch.width * .57f)) * Smooth((forward + 18) / 21) *
                    Smooth((patch.length + 17 - forward) / 28);
            }
            float travel = Mathf.Max(24, patch.length) * (alignment > 0 ? 1 : 1.16f);
            float change = contact * distance / samples * Mathf.Sign(alignment) * (Mathf.Abs(alignment) - .32f) / travel;
            float roughness = Mathf.Clamp01(tuft.Roughness - change);
            if (roughness == tuft.Roughness) return false;
            tuft.Roughness = alignment > 0 && roughness < .045f ? 0 : roughness;
            return true;
        }

        public bool Advance(float milliseconds)
        {
            bool moving = false;
            float follow = Mathf.Exp(-milliseconds / (Mode == FurMode.Brush ? 116 : 50));
            for (int i = 0; i < Count; i++)
                moving |= AdvanceTuft(_patches[i], ref _tufts[i], Mode, milliseconds, follow);
            return moving;
        }

        private static bool AdvanceTuft(FurProfile.Patch patch, ref Tuft tuft, FurMode mode, float milliseconds, float follow)
        {
            if (tuft.Flick > 0)
            {
                float fade = Mathf.Max(0, milliseconds - tuft.Linger);
                tuft.Linger = Mathf.Max(0, tuft.Linger - milliseconds);
                if (fade > 0) tuft.Flick *= Mathf.Exp(-fade / (125 + 32 * patch.seed));
                if (tuft.Flick < .004f) tuft.Flick = 0;
            }
            float lift = patch.initial * (mode == FurMode.Brush ? tuft.Roughness : tuft.Flick);
            float curl = patch.roll * (mode == FurMode.Brush ? Mathf.Sqrt(tuft.Roughness) : tuft.Flick * .65f);
            tuft.Lift = lift + (tuft.Lift - lift) * follow;
            tuft.Curl = curl + (tuft.Curl - curl) * follow;
            if (Mathf.Abs(tuft.Lift - lift) + Mathf.Abs(tuft.Curl - curl) > .002f || tuft.Flick > 0) return true;
            tuft.Lift = lift;
            tuft.Curl = curl;
            return false;
        }

        private static float Smooth(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3 - 2 * t);
        }
    }
}
