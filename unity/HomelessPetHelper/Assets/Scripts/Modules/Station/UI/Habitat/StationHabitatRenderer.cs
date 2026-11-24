using HomelessPetHelper.Modules.Station.Habitat;
using HomelessPetHelper.Modules.Animals.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    /// <summary>Owns one bounded offscreen target; one shared skeleton is used in every station pose.</summary>
    public sealed class StationHabitatRenderer : MonoBehaviour
    {
        [SerializeField] private StationCatPlayer _player;
        [SerializeField] private Camera _camera;
        private RawImage _surface;
        private RenderTexture _target;
        private HabitatAction _action = (HabitatAction)(-1);
        private float _renderedSeconds = -1;
        private bool _renderedLeft, _renderedRising;
        private bool _pendingSkinRefresh;
        private int _sampleFrame;
        private AnimalExpression _expression;
        private float _expressionSeconds;
        public StationCatPlayer Player => _player;

        public void Bind(RawImage surface) => _surface = surface;

        public void Render(HabitatAction action, float seconds, bool left, bool rising = false,
            AnimalExpression expression = default, float expressionSeconds = 0)
        {
            if (expression.TailSwish == 0) expressionSeconds = 0;
            bool changed = _target == null || _action != action || _renderedSeconds != seconds ||
                _renderedLeft != left || _renderedRising != rising ||
                _expression.TailRaise != expression.TailRaise || _expression.TailSwish != expression.TailSwish ||
                _expressionSeconds != expressionSeconds;
            if (!changed && !_pendingSkinRefresh) return;
            if (changed)
            {
                _player.Sample(action, seconds, rising, expression, expressionSeconds);
                _sampleFrame = Time.frameCount;
                _pendingSkinRefresh = true;
            }
            else if (Time.frameCount > _sampleFrame) _pendingSkinRefresh = false;
            // Unity uploads skinned vertices once per frame. When time freezes immediately
            // after sampling, render once more next frame to show the final sampled pose.
            if (_target == null) CreateTarget();
            // Framing belongs to the appearance's saved camera, authored from its profile.
            _camera.aspect = 1;
            // The source rig's positive X points toward the cat's nose (source image left).
            var uv = left ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
            if (_surface.uvRect != uv) _surface.uvRect = uv;
            _camera.Render();
            _action = action;
            _renderedSeconds = seconds;
            _renderedLeft = left;
            _renderedRising = rising;
            _expression = expression;
            _expressionSeconds = expressionSeconds;
        }

        private void CreateTarget()
        {
            _target = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32)
            { name = "StationHabitatCat", useMipMap = false, autoGenerateMips = false };
            _target.Create();
            _camera.targetTexture = _target;
            _surface.texture = _target;
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        private void Release()
        {
            if (_surface != null) _surface.texture = null;
            if (_camera != null) _camera.targetTexture = null;
            if (_target == null) return;
            _target.Release();
            Destroy(_target);
            _target = null;
        }
    }
}
