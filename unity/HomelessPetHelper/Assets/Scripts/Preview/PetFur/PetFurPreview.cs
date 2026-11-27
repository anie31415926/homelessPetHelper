using HomelessPetHelper.Modules.Animals.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Preview.PetFur
{
    public sealed class PetFurPreview : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler,
        IInitializePotentialDragHandler, IEndDragHandler
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _overlay;
        [SerializeField] private Texture2D _portrait;
        [SerializeField] private TextAsset _data;
        [SerializeField] private Material _template;
        [SerializeField] private Image _petButton, _brushButton;
        [SerializeField] private Sprite _selected, _unselected;

        private FurProfile _profile;
        private FurPose _pose;
        private FurMesh _batch;
        private Material _material;
        private Texture2D _edge;
        private int? _pointer;
        private Vector2 _previous;
        private bool _moving;
        private float _aspect;

        public Camera PreviewCamera => _camera;
        public int PatchCount => _pose.Count;
        public int VertexCount => _batch.Mesh.vertexCount;
        public int MeshUpdates { get; private set; }
        public FurMode Mode => _pose.Mode;

        private void Awake()
        {
            _profile = FurProfile.Read(_data);
            _pose = new FurPose(_profile.patches);
            _batch = new FurMesh(_profile);
            _edge = _profile.CreateEdgeTexture();
            _material = new Material(_template);
            _material.SetTexture("_MainTex", _portrait);
            _material.SetTexture("_EdgeTex", _edge);
            float angle = _profile.light.angle * Mathf.Deg2Rad;
            _material.SetVector("_Light", new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), _profile.light.shadow, _profile.edge.rows));
            _overlay.sharedMaterial = _material;
            _meshFilter.sharedMesh = _batch.Mesh;
            FitCamera();
            RefreshPose();
        }

        private void Update()
        {
            if (!Mathf.Approximately(_aspect, _camera.aspect)) FitCamera();
            if (!_moving) return;
            _moving = _pose.Advance(Mathf.Min(48, Time.unscaledDeltaTime * 1000));
            DrawMesh();
        }

        private void FitCamera()
        {
            _aspect = _camera.aspect;
            float width = _profile.crop.width / FurMesh.PixelsPerUnit;
            float height = _profile.crop.height / FurMesh.PixelsPerUnit;
            _camera.orthographicSize = Mathf.Max(height * .64f, width * .52f / _aspect);
        }

        public Vector2 ToScreen(Vector2 pixel) => _camera.WorldToScreenPoint(FurMesh.ToWorld(_profile.crop, pixel));
        private Vector2 ToPixel(Vector2 screen) => FurMesh.ToPixel(_profile.crop, _camera.ScreenToWorldPoint(screen));

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointer.HasValue || eventData.button != PointerEventData.InputButton.Left) return;
            _pointer = eventData.pointerId;
            _previous = ToPixel(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_pointer != eventData.pointerId) return;
            var point = ToPixel(eventData.position);
            if ((point - _previous).sqrMagnitude < 1) return;
            _moving |= _pose.Stroke(_previous, point);
            _previous = point;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_pointer == eventData.pointerId) _pointer = null;
        }

        public void OnEndDrag(PointerEventData eventData) => OnPointerUp(eventData);
        private void OnApplicationFocus(bool focused) { if (!focused) _pointer = null; }
        private void OnApplicationPause(bool paused) { if (paused) _pointer = null; }
        private void OnDisable() => _pointer = null;
        public void Pet() => ChangeMode(FurMode.Pet);
        public void Brush() => ChangeMode(FurMode.Brush);
        public void ResetFur() => ChangeMode(_pose.Mode, true);

        private void ChangeMode(FurMode mode, bool reset = false)
        {
            _pointer = null;
            _moving = false;
            _pose.Settle(mode, reset);
            RefreshPose();
        }

        private void RefreshPose()
        {
            _petButton.sprite = Mode == FurMode.Pet ? _selected : _unselected;
            _brushButton.sprite = Mode == FurMode.Brush ? _selected : _unselected;
            DrawMesh();
        }

        private void DrawMesh()
        {
            _batch.Upload(_pose);
            _overlay.enabled = VertexCount > 0;
            MeshUpdates++;
        }

        private void OnDestroy()
        {
            if (_batch != null) Destroy(_batch.Mesh);
            if (_material != null) Destroy(_material);
            if (_edge != null) Destroy(_edge);
        }
    }
}
