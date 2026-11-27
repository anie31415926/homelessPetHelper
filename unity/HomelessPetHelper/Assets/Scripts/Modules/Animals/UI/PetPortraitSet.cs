using UnityEngine;

namespace HomelessPetHelper.Modules.Animals.UI
{
    public sealed class PetPortraitSet : ScriptableObject
    {
        [SerializeField] private Texture2D _catAtlas;
        [SerializeField] private Texture2D _shibaAtlas;
        [SerializeField] private Sprite _stationDog;
        [SerializeField] private Shader _coatShader;
        public Texture2D CatAtlas => _catAtlas;
        public Texture2D ShibaAtlas => _shibaAtlas;
        internal Shader CoatShader => _coatShader;
        public bool IsStationDog(Sprite art) => art != null && art == _stationDog;

        private static Rect FrameUv(int frame, int columns, int rows, bool facingLeft)
        {
            float width = 1f / columns, height = 1f / rows;
            return new Rect((frame % columns + (facingLeft ? 0 : 1)) * width,
                (rows - 1 - frame / columns) * height, facingLeft ? width : -width, height);
        }

        public static Rect ShibaFrameUv(int frame, bool facingLeft = true) => FrameUv(frame, 8, 5, facingLeft);

        internal static Rect CatPortraitUv(float seconds) => FrameUv((int)(Mathf.Repeat(seconds, 2) * 6), 4, 3, true);

        public static Rect ShibaWalkUv(float seconds) =>
            ShibaFrameUv(16 + Mathf.Min(15, (int)(Mathf.Repeat(seconds, .8f) * 20)));

        /// <summary>Standing idle: eight authored poses ping-pong at 4 fps, two seconds per cycle.</summary>
        public static Rect ShibaIdleUv(float seconds) =>
            ShibaFrameUv(Mathf.Min(7, Mathf.FloorToInt(Mathf.PingPong(seconds * 4, 8))));

    }
}
