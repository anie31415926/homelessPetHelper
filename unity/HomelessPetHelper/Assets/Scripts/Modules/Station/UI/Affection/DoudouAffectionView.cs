using HomelessPetHelper.Modules.Animals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Affection
{
    public sealed class DoudouAffectionView : MaskableGraphic, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IInitializePotentialDragHandler
    {
        [SerializeField] private Texture2D _atlas;
        [SerializeField] private TextAsset _layoutSource;
        [SerializeField] private TextAsset _furSource;
        private readonly PetAffectionReaction _reaction = new PetAffectionReaction();
        private DoudouAffectionMesh _mesh;
        private DoudouTouchFur _fur;
        private PetAffectionMood _mood;
        private AnimalDisposition _disposition;
        private bool _showRecentTouch;
        private bool _paused, _hasPoint;
        private int? _pointer;
        private Vector2 _lastPoint;
        private float _seconds, _smile, _laugh, _lean, _tilt, _amount, _bend, _wag;
        private float _flowerAt = -10;
        private AffectionFace _previousFace;
        public bool HasArt => _atlas != null && _layoutSource != null;
        public bool HasRaisedFur => _fur?.Visible == true;
        public AffectionFace CurrentFace
        {
            get
            {
                var face = _reaction.Face(_seconds);
                return face != AffectionFace.Calm ? face :
                    _mood?.Face(_disposition, Time.unscaledTimeAsDouble, _showRecentTouch) ?? AffectionFace.Calm;
            }
        }
        public override Texture mainTexture => _atlas != null ? _atlas : base.mainTexture;

        public void SetInput(bool acceptsTouch, bool paused)
        {
            _paused = paused;
            raycastTarget = acceptsTouch && !paused;
            if (!raycastTarget && _pointer.HasValue) EndContact();
        }

        internal void SetMood(AnimalSnapshot animal, PetAffectionMood mood, bool showRecentTouch = false)
        {
            if (_mood != mood) ResetReaction();
            _mood = mood;
            _disposition = animal?.Disposition;
            _showRecentTouch = showRecentTouch;
        }

        protected override void OnDisable()
        {
            ResetReaction();
            base.OnDisable();
        }

        private void ResetReaction()
        {
            _reaction.Reset();
            _fur?.Reset();
            _pointer = null;
            _hasPoint = false;
            _seconds = _smile = _laugh = _lean = _tilt = _amount = _bend = _wag = 0;
            _flowerAt = -10;
            _previousFace = AffectionFace.Calm;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (_paused) return;
            float dt = Mathf.Min(.05f, Time.unscaledDeltaTime);
            _seconds += dt;
            var face = CurrentFace;
            if (face == AffectionFace.Laugh && _previousFace != AffectionFace.Laugh) _flowerAt = _seconds;
            _previousFace = face;
            float faceEase = 1 - Mathf.Exp(-dt / .034f);
            float motionEase = 1 - Mathf.Exp(-dt / .090f);
            bool dirty = Approach(ref _smile, face == AffectionFace.Smile ? 1 : 0, faceEase);
            dirty |= Approach(ref _laugh, face == AffectionFace.Laugh ? 1 : 0, faceEase);
            dirty |= Approach(ref _tilt, _lean, motionEase);
            float strength = face == AffectionFace.Laugh ? 1 : face == AffectionFace.Smile ? .45f : 0;
            dirty |= Approach(ref _bend, strength * _amount, motionEase);
            dirty |= Approach(ref _wag, _reaction.Wag(_seconds), faceEase);
            dirty |= _fur?.Advance(dt) == true;
            if (dirty || _wag != 0 || _seconds - _flowerAt <= DoudouAffectionMesh.FlowerDuration + dt)
                SetVerticesDirty();
        }

        private static bool Approach(ref float value, float target, float ease)
        {
            float next = Mathf.Lerp(value, target, ease);
            if (Mathf.Abs(next - target) < .002f) next = target;
            if (next == value) return false;
            value = next;
            return true;
        }

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;

        public void OnPointerDown(PointerEventData data)
        {
            if (!raycastTarget || _pointer.HasValue || data.button != PointerEventData.InputButton.Left ||
                !TryPoint(data, out var point)) return;
            _pointer = data.pointerId;
            _hasPoint = true;
            _lastPoint = point;
            _reaction.Begin(_seconds);
            if (_fur == null && _furSource != null) _fur = new DoudouTouchFur(_furSource);
        }

        public void OnDrag(PointerEventData data)
        {
            if (_pointer != data.pointerId) return;
            if (!TryPoint(data, out var point))
            {
                _hasPoint = false;
                _amount = _lean = 0;
                return;
            }
            if (_hasPoint)
            {
                bool head = Inside(point, .388f, .283f, .232f, .208f);
                _amount = head ? 1 : .36f;
                _lean = head ? Mathf.Clamp((point.x - .388f) * 7, -1, 1) : 0;
                if (_reaction.Stroke(_seconds, Vector2.Distance(point, _lastPoint) * 512)) _flowerAt = _seconds;
                if (_reaction.Face(_seconds) != AffectionFace.Calm) _mood?.Enjoy(Time.unscaledTimeAsDouble);
                _fur?.Stroke(_lastPoint * 512, point * 512);
            }
            _lastPoint = point;
            _hasPoint = true;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (_pointer == data.pointerId) EndContact();
        }

        private void EndContact()
        {
            _pointer = null;
            _hasPoint = false;
            _lean = _amount = 0;
            if (_reaction.Face(_seconds) != AffectionFace.Calm) _mood?.Enjoy(Time.unscaledTimeAsDouble);
            _reaction.End(_seconds);
        }

        private bool TryPoint(PointerEventData data, out Vector2 point)
        {
            point = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position,
                    data.pressEventCamera, out var local)) return false;
            var rect = GetPixelAdjustedRect();
            float side = Mathf.Min(rect.width, rect.height);
            if (side <= 0) return false;
            point = new Vector2(.5f + (local.x - rect.center.x) / side, .5f - (local.y - rect.center.y) / side);
            return Inside(point, .388f, .283f, .232f, .208f) || Inside(point, .514f, .604f, .330f, .237f);
        }

        private static bool Inside(Vector2 point, float x, float y, float rx, float ry) =>
            new Vector2((point.x - x) / rx, (point.y - y) / ry).sqrMagnitude <= 1;

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!HasArt) return;
            _mesh ??= new DoudouAffectionMesh(_layoutSource, _atlas.width, _atlas.height);
            _mesh.Draw(helper, GetPixelAdjustedRect(), _smile, _laugh, _tilt, _bend,
                Mathf.Sin(_seconds * 14) * _wag, _seconds - _flowerAt, color, _fur);
        }
    }
}
