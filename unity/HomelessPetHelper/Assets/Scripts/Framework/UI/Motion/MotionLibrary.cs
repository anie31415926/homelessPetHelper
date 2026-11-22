using System;
using UnityEngine;

namespace HomelessPetHelper.Framework.UI.Motion
{
    /// <summary>Read-only presentation definitions. Playback time and state belong to each instance.</summary>
    public sealed class MotionLibrary : ScriptableObject
    {
        [Serializable]
        private sealed class Definition
        {
            public MotionCue cue;
            public float durationSeconds;
            public Vector2 offset, scaleDelta;
            public float angleDegrees, opacity = 1;
            public bool loop;
            public AnimationCurve weight;
        }

        [SerializeField] private Definition[] _definitions = Array.Empty<Definition>();
        [SerializeField, Range(5, 60)] private int _sceneFramesPerSecond = 20;
        private Definition[] _lookup;
        public float SceneFrameSeconds => 1f / _sceneFramesPerSecond;

        public float Duration(MotionCue cue) => cue == MotionCue.None ? 0 : Get(cue).durationSeconds;
        public MotionPose Target(MotionCue cue)
        {
            if (cue == MotionCue.None) return MotionPose.Identity;
            var definition = Get(cue);
            return new MotionPose(definition.offset, Vector2.one + definition.scaleDelta,
                definition.angleDegrees, definition.opacity);
        }

        public float Weight(MotionCue cue, float seconds)
        {
            if (!Finite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            var definition = Get(cue);
            float phase = Mathf.Max(0, seconds) / definition.durationSeconds;
            phase = definition.loop ? Mathf.Repeat(phase, 1) : Mathf.Clamp01(phase);
            return Mathf.Clamp(definition.weight.Evaluate(phase), -1, 1);
        }

        public MotionPose Sample(MotionCue cue, float seconds) => cue == MotionCue.None ? MotionPose.Identity :
            MotionPose.Lerp(MotionPose.Identity, Target(cue), Weight(cue, seconds));

        private Definition Get(MotionCue cue)
        {
            if (_lookup == null) Validate();
            int index = (int)cue;
            if (index <= 0 || index >= _lookup.Length || _lookup[index] == null)
                throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unconfigured presentation motion.");
            return _lookup[index];
        }

        public void Validate()
        {
            var lookup = new Definition[Enum.GetValues(typeof(MotionCue)).Length];
            if (_sceneFramesPerSecond < 5 || _sceneFramesPerSecond > 60)
                throw new InvalidOperationException("Presentation sampling rate must be within 5..60 fps.");
            foreach (var definition in _definitions)
            {
                if (definition == null) throw new InvalidOperationException("Missing motion definition.");
                int index = (int)definition.cue;
                if (index <= 0 || index >= lookup.Length || lookup[index] != null)
                    throw new InvalidOperationException("Invalid or duplicate motion cue: " + definition.cue);
                ValidateDefinition(definition);
                lookup[index] = definition;
            }
            for (int index = 1; index < lookup.Length; index++)
                if (lookup[index] == null) throw new InvalidOperationException("Missing motion cue: " + index);
            _lookup = lookup;
        }

        private static void ValidateDefinition(Definition definition)
        {
            if (!Finite(definition.durationSeconds) || definition.durationSeconds <= 0 ||
                !Finite(definition.offset.x) || !Finite(definition.offset.y) ||
                !Finite(definition.scaleDelta.x) || !Finite(definition.scaleDelta.y) ||
                Mathf.Abs(definition.scaleDelta.x) >= 1 || Mathf.Abs(definition.scaleDelta.y) >= 1 ||
                !Finite(definition.angleDegrees) || !Finite(definition.opacity) ||
                definition.opacity < 0 || definition.opacity > 1 ||
                definition.weight == null || definition.weight.length < 2)
                throw new InvalidOperationException("Invalid presentation definition: " + definition.cue);
            foreach (var key in definition.weight.keys)
                if (!Finite(key.time) || !Finite(key.value) || !Finite(key.inTangent) || !Finite(key.outTangent))
                    throw new InvalidOperationException("Non-finite presentation curve: " + definition.cue);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnEnable() => _lookup = null;
        private void OnValidate() => _lookup = null;
    }
}
