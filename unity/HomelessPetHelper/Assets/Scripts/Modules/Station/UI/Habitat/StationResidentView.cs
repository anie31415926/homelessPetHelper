using System;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Station.Habitat;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    public sealed class StationResidentView : MonoBehaviour
    {
        [SerializeField] private StationHabitatRenderer _rendererPrefab;
        [SerializeField] private RawImage _motionImage;
        [SerializeField] private Image _stillImage;
        [SerializeField] private RectTransform _position;
        [SerializeField] private Text _name;
        [SerializeField] private GameObject _cage;
        [SerializeField] private GameObject _selected;
        [SerializeField] private Button _select;
        [SerializeField] private AnimalCoatView _stillCoat, _motionCoat;
        private StationHabitatRenderer _renderer;
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
            _stillCoat.Show(resident.Snapshot);
            _motionCoat.Show(resident.Snapshot);
            _stillImage.gameObject.SetActive(!resident.HasAnimation);
            _motionImage.gameObject.SetActive(resident.HasAnimation);
            _cage.SetActive(resident.Observing);
            _position.sizeDelta = _room != null ? new Vector2(210, 210) :
                resident.Observing ? new Vector2(300, 300) : new Vector2(360, 360);
            if (changedKind || !resident.HasAnimation) Release();
            Place();
        }

        public void SetSuspended(bool suspended)
        {
            _suspended = suspended;
            _select.interactable = !suspended && _resident != null;
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
            _expression = _expression.MoveTowards(_targetExpression, seconds / ExpressionBlendSeconds);
            var appearance = _resident.Appearance != null ? _resident.Appearance : _rendererPrefab;
            // Wrap whole cycles of this appearance; different tail periods must not jump at wrap.
            float cycleWindow = appearance.Player.ExpressionPeriodSeconds * 100;
            _expressionSeconds = Mathf.Repeat(_expressionSeconds + seconds, cycleWindow);
            if (Observing) _restSeconds += seconds;
            else Motion.Tick(seconds);
        }

        private void LateUpdate()
        {
            if (_resident == null) return;
            Place();
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

        private void Place()
        {
            if (_room != null)
            {
                float size = Mathf.Max(144, _room.Content.rect.width * .21f);
                _position.sizeDelta = new Vector2(size, size);
            }
            Vector2 anchor = _room != null ? Observing ?
                RoomCagePoint(_place) :
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
