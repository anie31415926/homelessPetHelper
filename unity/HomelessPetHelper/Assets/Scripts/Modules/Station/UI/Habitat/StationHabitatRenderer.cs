using HomelessPetHelper.Modules.PetMotion;
using HomelessPetHelper.Modules.Station.Habitat;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    /// <summary>Owns one bounded offscreen target; rig and clips remain shared immutable assets.</summary>
    public sealed class StationHabitatRenderer : MonoBehaviour
    {
        [SerializeField] private PetMotionPlayer _player;
        [SerializeField] private Camera _camera;
        [SerializeField] private Renderer[] _renderers;
        private RawImage _surface;
        private RenderTexture _target;
        private int _action = -1;
        private float _foot;
        private float _center;
        private float _renderedSeconds = -1;
        private bool _renderedLeft;
        private Mesh _boundsMesh;

        public PetMotionPlayer Player => _player;

        public void Bind(RawImage surface)
        {
            _surface = surface;
            _player.Playing = false;
        }

        public void Render(HabitatAction action, float seconds, bool left)
        {
            int clip = action == HabitatAction.Walk ? 2 : action == HabitatAction.Sit ? 4 :
                action == HabitatAction.Lie ? 5 : action == HabitatAction.Attention ? 1 : 0;
            if (_target != null && _action == clip && _renderedSeconds == seconds && _renderedLeft == left)
                return;
            if (_action != clip || _player.Action != clip)
            {
                _player.Select(clip);
                _player.Seek(0);
                Bounds bounds = VisibleBounds();
                _foot = bounds.min.y;
                _center = bounds.center.x;
                _action = clip;
            }
            _player.Seek(Mathf.Repeat(seconds, _player.Duration) / _player.Duration);
            if (_target == null)
            {
                _target = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32)
                { name = "StationHabitatCat", useMipMap = false, autoGenerateMips = false };
                _target.Create();
                _camera.targetTexture = _target;
                _surface.texture = _target;
            }
            // A fixed feet line and framing across poses avoids recentering the cat when it lies down.
            _camera.aspect = 1;
            _camera.orthographicSize = 320;
            _camera.transform.position = new Vector3(_center, _foot + 256, transform.position.z - 10);
            // The accepted source cat faces left. Flip UVs, not the rig through zero scale.
            var uv = left ? new Rect(0, 0, 1, 1) : new Rect(1, 0, -1, 1);
            if (_surface.uvRect != uv) _surface.uvRect = uv;
            _camera.Render();
            _renderedSeconds = seconds;
            _renderedLeft = left;
        }

        private Bounds VisibleBounds()
        {
            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in _renderers)
            {
                if (!renderer.gameObject.activeInHierarchy) continue;
                Bounds actual = renderer.bounds;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    // Renderer.bounds may still describe a previously inactive pose before its first render.
                    if (_boundsMesh == null) _boundsMesh = new Mesh { name = "HabitatPoseBounds" };
                    skin.BakeMesh(_boundsMesh);
                    Bounds local = _boundsMesh.bounds;
                    actual = new Bounds(skin.transform.TransformPoint(local.center),
                        Vector3.Scale(local.size, skin.transform.lossyScale));
                }
                if (!found) { bounds = actual; found = true; }
                else bounds.Encapsulate(actual);
            }
            return bounds;
        }

        private void OnDisable() => Release();
        private void OnDestroy()
        {
            Release();
            if (_boundsMesh != null) Destroy(_boundsMesh);
        }

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
