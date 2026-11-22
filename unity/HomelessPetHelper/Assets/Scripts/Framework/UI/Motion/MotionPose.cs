using UnityEngine;

namespace HomelessPetHelper.Framework.UI.Motion
{
    public readonly struct MotionPose
    {
        public readonly Vector2 Offset, Scale;
        public readonly float AngleDegrees, Opacity;

        public MotionPose(Vector2 offset, Vector2 scale, float angleDegrees, float opacity)
        {
            Offset = offset;
            Scale = scale;
            AngleDegrees = angleDegrees;
            Opacity = opacity;
        }

        public static MotionPose Identity => new MotionPose(Vector2.zero, Vector2.one, 0, 1);

        public static MotionPose Lerp(MotionPose from, MotionPose to, float weight) => new MotionPose(
            Vector2.LerpUnclamped(from.Offset, to.Offset, weight),
            Vector2.LerpUnclamped(from.Scale, to.Scale, weight),
            Mathf.LerpUnclamped(from.AngleDegrees, to.AngleDegrees, weight),
            Mathf.LerpUnclamped(from.Opacity, to.Opacity, weight));
    }
}
