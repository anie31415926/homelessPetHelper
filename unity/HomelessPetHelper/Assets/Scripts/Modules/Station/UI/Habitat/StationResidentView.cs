using System;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Station.Habitat;
using HomelessPetHelper.Modules.Station.UI.Affection;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    public sealed class StationResidentView : MonoBehaviour
    {
        [SerializeField] private StationHabitatRenderer _rendererPrefab;
        [SerializeField] private Texture2D _shibaAtlas;
        [SerializeField] private RawImage _motionImage;
        [SerializeField] private Image _stillImage;
        [SerializeField] private RectTransform _position;
        [SerializeField] private Text _name;
        [SerializeField] private GameObject _cage;
        [SerializeField] private GameObject _selected;
        [SerializeField] private Button _select;
        [SerializeField] private AnimalCoatView _stillCoat, _motionCoat;
        private StationHabitatRenderer _renderer;
        private DoudouAffectionView _expressionView, _expressionPrefab;
        private StationSceneResident _resident;
        private bool _suspended;
        private bool _inspecting;
        private int _slot;
        private int _place;
        private float _restSeconds;
        private AnimalExpression _expression, _targetExpression;
        private float _expressionSeconds;
        private const float ExpressionBlendSeconds = .2f;
        private StationRoomView _room;

        public event Action<int> Selected;
        public StationHabitatMotion Motion { get; private set; }
        public Guid AnimalId => _resident != null ? _resident.AnimalId : Guid.Empty;
        public bool Observing => _resident != null && _resident.Observing;
        public Button SelectButton => _select;
        public RawImage MotionImage => _motionImage;
        public float GroundY => _position.anchorMin.y;
        private bool HasShibaFrames => _resident.AnimatedShiba && _shibaAtlas != null;

        public void Bind(StationSceneResident resident, StationHabitatMotion motion, int slot, int place,
            StationRoomView room = null)
        {
            bool changedKind = _resident != null && (_resident.HasAnimation != resident.HasAnimation ||
                _resident.Appearance != resident.Appearance);
            _targetExpression = AnimalExpression.From(resident.Snapshot);
            if (_resident == null || _resident.AnimalId != resident.AnimalId)
            {
                _restSeconds = 0;
                _expressionSeconds = 0;
                _expression = _targetExpression;
            }
            _resident = resident;
            Motion = motion;
            _slot = slot;
            _place = place;
            _room = room;
            _name.text = resident.Name;
            _name.gameObject.SetActive(_room == null);
            _stillImage.sprite = resident.Art;
            bool layered = resident.ExpressionView != null;
            bool animated = !layered && (resident.HasAnimation || HasShibaFrames);
            if (changedKind || !resident.HasAnimation || layered) Release();
            if (!layered && HasShibaFrames) RenderShiba();
            else if (_motionImage.texture == _shibaAtlas) _motionImage.texture = null;
            _stillCoat.Show(layered ? null : resident.Snapshot);
            _motionCoat.Show(layered ? null : resident.Snapshot);
            _stillImage.gameObject.SetActive(!animated);
            _stillImage.canvasRenderer.cullTransparentMesh = !layered;
            _stillImage.canvasRenderer.SetAlpha(layered ? 0f : 1f);
            _motionImage.gameObject.SetActive(animated);
            BindExpression(resident);
            _cage.SetActive(resident.Observing);
            _position.sizeDelta = _room != null ? new Vector2(210, 210) :
                resident.Observing ? new Vector2(300, 300) : new Vector2(360, 360);
            Place();
        }

        public void SetSuspended(bool suspended)
        {
            _suspended = suspended;
            _select.interactable = !suspended && _resident != null;
            if (_expressionView != null) _expressionView.SetInput(false, suspended);
        }

        public void SetInspecting(bool inspecting)
        {
            _inspecting = inspecting;
            _selected.SetActive(inspecting);
        }

        private void OnEnable() => _select.onClick.AddListener(Select);
        private void OnDisable()
        {
            _select.onClick.RemoveListener(Select);
            Release();
        }
        private void OnDestroy() => Release();

        private void Update()
        {
            if (_resident == null || _suspended || _inspecting) return;
            float seconds = Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (Observing) _restSeconds += seconds;
            else Motion.Tick(seconds);
            if (_resident.ExpressionView != null || !_resident.HasAnimation || HasShibaFrames) return;
            _expression = _expression.MoveTowards(_targetExpression, seconds / ExpressionBlendSeconds);
            var appearance = _resident.Appearance != null ? _resident.Appearance : _rendererPrefab;
            // Wrap whole cycles of this appearance; different tail periods must not jump at wrap.
            float cycleWindow = appearance.Player.ExpressionPeriodSeconds * 100;
            _expressionSeconds = Mathf.Repeat(_expressionSeconds + seconds, cycleWindow);
        }

        private void LateUpdate()
        {
            if (_resident == null) return;
            Place();
            if (_resident.ExpressionView != null)
            {
                RenderExpressionPose();
                return;
            }
            if (HasShibaFrames) { RenderShiba(); return; }
            if (!_resident.HasAnimation) return;
            if (_renderer == null)
            {
                _renderer = Instantiate(_resident.Appearance != null ? _resident.Appearance : _rendererPrefab);
                _renderer.transform.position = new Vector3(10000 + _slot * 2000, 0, 0);
                _renderer.Bind(_motionImage);
                _renderer.gameObject.SetActive(true);
                return;
            }
            _renderer.Render(Observing ? HabitatAction.Sit : Motion.Pose,
                Observing ? _restSeconds : (float)Motion.PoseSeconds, Observing || Motion.FacingLeft,
                !Observing && Motion.RisingFromRest, _expression, _expressionSeconds);
        }

        private void BindExpression(StationSceneResident resident)
        {
            var prefab = resident.ExpressionView;
            if (_expressionView != null && prefab != null && _expressionPrefab != prefab)
            {
                _expressionView.gameObject.SetActive(false);
                Destroy(_expressionView.gameObject);
                _expressionView = null;
            }
            if (prefab == null)
            {
                if (_expressionView != null) _expressionView.gameObject.SetActive(false);
                return;
            }
            if (_expressionView == null)
            {
                _expressionPrefab = prefab;
                _expressionView = Instantiate(prefab, _stillImage.rectTransform, false);
            }
            _expressionView.SetMood(resident.Snapshot, resident.Mood, true);
            _expressionView.SetInput(false, _suspended);
            _expressionView.gameObject.SetActive(true);
            RenderExpressionPose();
        }

        private void RenderExpressionPose()
        {
            var pose = _expressionView.rectTransform;
            bool walking = !Observing && Motion.Pose == HabitatAction.Walk;
            float phase = (float)Motion.PoseSeconds * (2 * Mathf.PI / .46f);
            float sway = walking ? Mathf.Sin(phase) : 0;
            var position = pose.rect.width * new Vector2(sway * .014f, Mathf.Abs(sway) * .020f);
            var rotation = Quaternion.Euler(0, 0, sway * 4.5f);
            var scale = new Vector3(Observing || Motion.FacingLeft ? 1 : -1, 1, 1);
            if (pose.anchoredPosition != position) pose.anchoredPosition = position;
            if (pose.localRotation != rotation) pose.localRotation = rotation;
            if (pose.localScale != scale) pose.localScale = scale;
        }

        private void RenderShiba()
        {
            // Matches the shiba-station bake: 16 sit, 16 walk, 8 walk-in cells.
            Rect uv = PetPortraitSet.ShibaFrameUv(ShibaFrame(), Observing || Motion.FacingLeft);
            if (_motionImage.texture != _shibaAtlas) _motionImage.texture = _shibaAtlas;
            if (_motionImage.uvRect != uv) _motionCoat.SetFrameUv(_motionImage, uv);
        }

        private int ShibaFrame()
        {
            const float settleSeconds = 1.35f, walkCycleSeconds = .8f;
            var pose = Observing ? HabitatAction.Sit : Motion.Pose;
            float elapsed = Observing ? _restSeconds : (float)Motion.PoseSeconds;
            if (pose == HabitatAction.Sit || pose == HabitatAction.Lie)
                return Mathf.RoundToInt(15 * Mathf.Clamp01(elapsed / settleSeconds));
            if (pose != HabitatAction.Walk) return 0;
            if (Motion.RisingFromRest)
            {
                elapsed -= StationHabitatMotion.RiseSeconds;
                if (elapsed < 0)
                    return Mathf.RoundToInt(15 * Mathf.Clamp01(-elapsed / StationHabitatMotion.RiseSeconds));
            }
            if (elapsed < walkCycleSeconds * .5f) return 32 + (int)(elapsed / walkCycleSeconds * 16);
            return 16 + Mathf.Min(15, (int)(Mathf.Repeat(elapsed, walkCycleSeconds) / walkCycleSeconds * 16));
        }

        private void Place()
        {
            if (_room != null)
            {
                float size = Mathf.Max(144, _room.Content.rect.width * .21f);
                _position.sizeDelta = new Vector2(size, size);
            }
            Vector2 anchor = _room != null ? Observing ?
                _room.ObservationAnchor(_place, RoomCagePoint(_place)) :
                new Vector2((float)Motion.Position.X, (float)Motion.Position.Y) : Observing ? CagePoint(_place) :
                new Vector2(.07f + .60f * (float)Motion.Position.X,
                    .12f + .9f * (float)Motion.Position.Y + .075f * _place);
            if (_position.anchorMin != anchor)
            {
                _position.anchorMin = _position.anchorMax = anchor;
                _position.anchoredPosition = Vector2.zero;
            }
        }

        private static Vector2 RoomCagePoint(int index) => index < 2 ?
            new Vector2(.43f + index * .24f, .51f) : new Vector2(.25f + (index - 2) * .5f, .095f);

        private static Vector2 CagePoint(int index)
        {
            switch (index)
            {
                case 0: return new Vector2(.79f, .62f);
                case 1: return new Vector2(.79f, .40f);
                case 2: return new Vector2(.30f, .78f);
                default: return new Vector2(.79f, .80f);
            }
        }

        private void Select()
        {
            if (!_suspended && _resident != null) Selected?.Invoke(_resident.Index);
        }

        private void Release()
        {
            if (_renderer == null) return;
            _renderer.gameObject.SetActive(false);
            Destroy(_renderer.gameObject);
            _renderer = null;
        }
    }
}
