using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Station.Habitat;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    public sealed class StationHabitatView : MonoBehaviour
    {
        [Serializable] private sealed class Copy
        {
            public string empty, willing, quiet;
            public string[] states;
        }

        [SerializeField] private TextAsset _text;
        [SerializeField] private StationHabitatRenderer _actorPrefab;
        [SerializeField] private RectTransform _actor;
        [SerializeField] private RawImage _image;
        [SerializeField] private RectTransform _shadow;
        [SerializeField] private Button _attention;
        [SerializeField] private Text _status;
        [SerializeField] private Text _empty;
        private readonly Dictionary<Guid, StationHabitatMotion> _motions = new Dictionary<Guid, StationHabitatMotion>();
        private StationHabitatRenderer _renderer;
        private Copy _copy;
        private Guid _id;
        private string _name;
        private bool _acceptsTouch;
        private bool _suspended;
        private bool _focused = true;
        private bool _applicationPaused;
        private HabitatAction _shownAction = (HabitatAction)(-1);
        private HabitatResponse _shownResponse = (HabitatResponse)(-1);

        public Guid AnimalId => _id;
        public StationHabitatMotion Motion { get; private set; }
        public RawImage Image => _image;
        public bool Suspended => _suspended || !_focused || _applicationPaused || !isActiveAndEnabled;

        public void ShowResident(Guid id, string displayName, bool acceptsTouch)
        {
            if (_copy == null)
            {
                _copy = JsonUtility.FromJson<Copy>(_text.text);
                if (_copy == null || _copy.states == null || _copy.states.Length != 5 ||
                    string.IsNullOrWhiteSpace(_copy.empty) || string.IsNullOrWhiteSpace(_copy.willing) ||
                    string.IsNullOrWhiteSpace(_copy.quiet))
                    throw new InvalidOperationException("Invalid StationHabitat text configuration.");
            }
            if (id != Guid.Empty && string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A habitat resident needs a display name.", nameof(displayName));
            bool changed = _id != id || _name != displayName;
            _id = id;
            _name = displayName;
            _acceptsTouch = acceptsTouch;
            Motion = null;
            if (id != Guid.Empty)
            {
                if (!_motions.TryGetValue(id, out var motion))
                {
                    motion = new StationHabitatMotion(id.GetHashCode());
                    _motions.Add(id, motion);
                }
                Motion = motion;
            }
            if (Motion != null) PlaceActor();
            _actor.gameObject.SetActive(Motion != null);
            _shadow.gameObject.SetActive(Motion != null);
            _empty.gameObject.SetActive(Motion == null);
            _empty.text = _copy.empty;
            if (changed) _shownAction = (HabitatAction)(-1);
            RefreshStatus();
            if (Motion == null) ReleaseActor();
        }

        public void SetSuspended(bool suspended)
        {
            _suspended = suspended;
            _attention.interactable = !Suspended;
        }

        public void ClearResidents()
        {
            _motions.Clear();
            ShowResident(Guid.Empty, string.Empty, false);
        }

        private void OnEnable() => _attention.onClick.AddListener(Attention);
        private void OnDisable()
        {
            _attention.onClick.RemoveListener(Attention);
            ReleaseActor();
        }
        private void OnDestroy() => ReleaseActor();
        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            _attention.interactable = !Suspended;
        }
        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            _attention.interactable = !Suspended;
        }

        private void Update()
        {
            if (Motion == null || Suspended) return;
            Motion.Tick(Mathf.Min(Time.unscaledDeltaTime, .05f));
            RefreshStatus();
        }

        private void LateUpdate()
        {
            if (Motion == null) return;
            RefreshStatus();
            if (_renderer == null)
            {
                _renderer = Instantiate(_actorPrefab);
                _renderer.transform.position = new Vector3(10000, 0, 0);
                _renderer.Bind(_image);
                return;
            }
            PlaceActor();
            _renderer.Render(Motion.Pose, (float)Motion.PoseSeconds, Motion.FacingLeft, Motion.RisingFromRest);
        }

        private void PlaceActor()
        {
            // Fractional anchors stay correct even when layout changes before the next LateUpdate.
            var anchor = new Vector2((float)Motion.Position.X, (float)Motion.Position.Y);
            if (_actor.anchorMin == anchor) return;
            _actor.anchorMin = _actor.anchorMax = anchor;
            _shadow.anchorMin = _shadow.anchorMax = anchor;
            _actor.anchoredPosition = _shadow.anchoredPosition = Vector2.zero;
        }

        private void Attention()
        {
            if (Suspended || Motion == null || !Motion.TryGetAttention(_acceptsTouch)) return;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (Motion == null) { _status.text = string.Empty; return; }
            if (_shownAction == Motion.Action && _shownResponse == Motion.Response) return;
            _shownAction = Motion.Action;
            _shownResponse = Motion.Response;
            string state = Motion.Response == HabitatResponse.Willing ? _copy.willing :
                Motion.Response == HabitatResponse.NeedsSpace ? _copy.quiet : _copy.states[(int)Motion.Action];
            _status.text = _name + " · " + state;
        }

        private void ReleaseActor()
        {
            if (_renderer == null) return;
            _renderer.gameObject.SetActive(false);
            Destroy(_renderer.gameObject);
            _renderer = null;
        }
    }
}
