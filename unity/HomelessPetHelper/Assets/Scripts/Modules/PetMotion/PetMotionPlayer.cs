using System;
using UnityEngine;

namespace HomelessPetHelper.Modules.PetMotion
{
    /// <summary>
    /// 播放一只动物的六段动作，并在动作之间做过渡。
    /// 同一姿态内的动作共用骨架，按姿势缓冲混合；跨姿态骨架不同，只能在渲染层做透明度交叉淡入。
    /// </summary>
    public sealed class PetMotionPlayer : MonoBehaviour
    {
        private const int PostureStand = 0;
        private const int PostureSit = 1;
        private const int PostureSide = 2;
        private const int PostureCount = 3;
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        [SerializeField] private Transform _rig;
        [SerializeField] private AnimationClip[] _clips;
        [SerializeField] private GameObject _standing;
        [SerializeField] private GameObject _sit;
        [SerializeField] private GameObject _side;
        [SerializeField] private GameObject[] _sources;
        [SerializeField] private Transform[] _bones;
        [SerializeField] private float _actionSeconds = .2f;
        [SerializeField] private float _postureSeconds = .14f;
        private Vector3[] _restPositions;
        private int _action;
        private float _time;
        private bool _source;
        private bool _applicationPaused;
        private bool _hasFocus = true;

        private Pose _poseA;
        private Pose _poseB;
        private int _fromAction;
        private float _fromTime;
        private float _blend;
        private float _blendSeconds;
        private bool _blending;
        private bool _fromSnapshot;
        private Vector3 _rootFrom;
        private Renderer[][] _postureRenderers;
        private int[][] _restSortingOrders;
        private int _postureSortingSpan;
        private readonly float[] _opacity = { 1, 0, 0 };
        private MaterialPropertyBlock _block;

        public int Action => _action;
        public float TimeSeconds => _time;
        public float Duration => _clips[_action].length;
        public bool Playing { get; set; } = true;
        public float Speed { get; private set; } = 1;
        public bool Source => _source;
        public int Revision { get; private set; }

        /// <summary>当前是否处于动作过渡中。</summary>
        public bool Transitioning => _blending;

        private void Awake()
        {
            _restPositions = new Vector3[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
                _restPositions[i] = _bones[i].localPosition;
            _poseA = new Pose(_bones.Length);
            _poseB = new Pose(_bones.Length);
            _block = new MaterialPropertyBlock();
            _postureRenderers = new Renderer[PostureCount][];
            _postureRenderers[PostureStand] = Collect(_standing);
            _postureRenderers[PostureSit] = Collect(_sit);
            _postureRenderers[PostureSide] = Collect(_side);
            _restSortingOrders = new int[PostureCount][];
            int lowest = 0, highest = 0;
            for (int posture = 0; posture < PostureCount; posture++)
            {
                Renderer[] renderers = _postureRenderers[posture];
                _restSortingOrders[posture] = new int[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    int order = renderers[i].sortingOrder;
                    _restSortingOrders[posture][i] = order;
                    lowest = Mathf.Min(lowest, order);
                    highest = Mathf.Max(highest, order);
                }
            }
            _postureSortingSpan = highest - lowest + 1;
            // 属性块默认不写就是 1，先把缓存值和渲染器对齐，否则首次淡入会跳过写入而整只弹出。
            for (int posture = 0; posture < PostureCount; posture++)
                ApplyOpacity(posture, posture == PostureStand ? 1 : 0);
        }

        private void Start()
        {
            if (_rig == null || _clips == null || _clips.Length != 6 || _sources.Length != 3)
                throw new InvalidOperationException("Incomplete pet motion preview bindings.");
            Select(0);
        }

        private void Update()
        {
            if (!Playing || _source || _applicationPaused || !_hasFocus)
                return;
            float delta = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _time = Mathf.Repeat(_time + delta * Speed, Duration);
            if (!_blending)
            {
                Sample();
                return;
            }
            // 过渡进度按真实时间推进，不受调速影响，否则慢放时过渡会拖得很长。
            _fromTime = Mathf.Repeat(_fromTime + delta * Speed, _clips[_fromAction].length);
            _blend = Mathf.Min(1, _blend + delta / _blendSeconds);
            if (_blend >= 1)
                EndBlend();
            Sample();
        }

        private void OnApplicationPause(bool paused) => _applicationPaused = paused;
        private void OnApplicationFocus(bool focused) => _hasFocus = focused;

        /// <summary>立即切到指定动作，不经过过渡。</summary>
        public void Select(int action)
        {
            if (action < 0 || action >= _clips.Length)
                throw new ArgumentOutOfRangeException(nameof(action));
            _blending = false;
            _fromSnapshot = false;
            _blend = 1;
            _action = action;
            _time = 0;
            SetVisible();
            SetSorting(PostureOf(action));
            Sample();
        }

        /// <summary>
        /// 在默认时长内过渡到指定动作。跨姿态的透明度双影比姿势混合更容易看出来，默认更短。
        /// </summary>
        public void TransitionTo(int action) =>
            TransitionTo(action, IsSamePosture(_action, action) ? _actionSeconds : _postureSeconds);

        /// <summary>
        /// 在给定时长内过渡到指定动作。过渡中再次调用会从当前混合姿势接着淡向新目标，不排队。
        /// </summary>
        public void TransitionTo(int action, float seconds)
        {
            if (action < 0 || action >= _clips.Length)
                throw new ArgumentOutOfRangeException(nameof(action));
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            // 暂停时 Update 不再推进过渡，淡入会永远停在起点，看起来像没切换，所以直接到位。
            if (_source || !Playing || seconds <= 0)
            {
                Select(action);
                return;
            }
            if (_blending && action == _action)
                return;
            if (!_blending && action == _action)
            {
                Select(action);
                return;
            }
            int previous = _action;
            // 同姿态打断时把当前混合结果冻结成起点，避免跳回纯 B 姿势。
            _fromSnapshot = _blending && IsSamePosture(previous, action);
            if (_fromSnapshot)
                Capture(_poseA);
            else
                _fromTime = _time;
            _fromAction = previous;
            _action = action;
            _time = 0;
            _rootFrom = _rig.localPosition;
            _blend = 0;
            _blendSeconds = seconds;
            _blending = true;
            SetVisible();
            SetSorting(PostureOf(action));
            Sample();
        }

        public void SetSource(bool source)
        {
            _source = source;
            _blending = false;
            _fromSnapshot = false;
            SetVisible();
            SetSorting(PostureOf(_action));
            Sample();
        }

        public void SetSpeed(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed))
                throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = Mathf.Clamp(speed, .5f, 1.5f);
        }

        public void Seek(float normalizedTime)
        {
            if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime))
                throw new ArgumentOutOfRangeException(nameof(normalizedTime));
            EndBlend();
            Playing = false;
            _time = Mathf.Clamp01(normalizedTime) * Duration;
            Sample();
        }

        public void ResetPreview()
        {
            Playing = true;
            Speed = 1;
            _source = false;
            Select(0);
        }

        private void EndBlend()
        {
            if (!_blending)
                return;
            _blending = false;
            _fromSnapshot = false;
            _blend = 1;
            SetVisible();
            SetSorting(PostureOf(_action));
        }

        private void Sample()
        {
            if (!_blending)
            {
                ApplyRest();
                if (!_source)
                    _clips[_action].SampleAnimation(_rig.gameObject, _time);
                Revision++;
                return;
            }
            if (_fromSnapshot || IsSamePosture(_fromAction, _action))
                SampleBlend();
            else
                SampleCrossPosture();
            Revision++;
        }

        /// <summary>
        /// 先让每个片段各自输出完整姿势（未驱动的骨骼回到 rest），再按权重插值。
        /// 片段覆盖的骨骼集合不同，直接在一次结果上做插值会让未覆盖的骨骼跳回原位。
        /// </summary>
        private void SampleBlend()
        {
            if (!_fromSnapshot)
            {
                ApplyRest();
                _clips[_fromAction].SampleAnimation(_rig.gameObject, _fromTime);
                Capture(_poseA);
            }
            ApplyRest();
            _clips[_action].SampleAnimation(_rig.gameObject, _time);
            Capture(_poseB);
            BlendPose(_poseA, _poseB, Smooth(_blend));
        }

        /// <summary>
        /// 两套骨架的骨骼无法对应，只能各自采样并同时显示，用透明度互换。
        /// 过渡期间把根位移收回原点，否则站姿的来回走会和坐卧静态姿势错位。
        /// </summary>
        private void SampleCrossPosture()
        {
            ApplyRest();
            _clips[_fromAction].SampleAnimation(_rig.gameObject, _fromTime);
            _clips[_action].SampleAnimation(_rig.gameObject, _time);
            float weight = Smooth(_blend);
            _rig.localPosition = Vector3.LerpUnclamped(_rootFrom, Vector3.zero, weight);
            _rig.localRotation = Quaternion.identity;
            _rig.localScale = Vector3.one;
            SetOpacity(PostureOf(_fromAction), 1 - weight);
            SetOpacity(PostureOf(_action), weight);
        }

        private void ApplyRest()
        {
            // Reset values omitted from the next clip (constant tracks are not exported).
            _rig.localPosition = Vector3.zero;
            _rig.localRotation = Quaternion.identity;
            _rig.localScale = Vector3.one;
            for (int i = 0; i < _bones.Length; i++)
            {
                _bones[i].localPosition = _restPositions[i];
                _bones[i].localRotation = Quaternion.identity;
                _bones[i].localScale = Vector3.one;
            }
        }

        private void Capture(Pose pose)
        {
            pose.RigPosition = _rig.localPosition;
            pose.RigRotation = _rig.localRotation;
            pose.RigScale = _rig.localScale;
            for (int i = 0; i < _bones.Length; i++)
            {
                pose.Positions[i] = _bones[i].localPosition;
                pose.Rotations[i] = _bones[i].localRotation;
                pose.Scales[i] = _bones[i].localScale;
            }
        }

        private void BlendPose(Pose from, Pose to, float weight)
        {
            _rig.localPosition = Vector3.LerpUnclamped(from.RigPosition, to.RigPosition, weight);
            _rig.localRotation = Quaternion.SlerpUnclamped(from.RigRotation, to.RigRotation, weight);
            _rig.localScale = Vector3.LerpUnclamped(from.RigScale, to.RigScale, weight);
            for (int i = 0; i < _bones.Length; i++)
            {
                Transform bone = _bones[i];
                bone.localPosition = Vector3.LerpUnclamped(from.Positions[i], to.Positions[i], weight);
                bone.localRotation = Quaternion.SlerpUnclamped(from.Rotations[i], to.Rotations[i], weight);
                bone.localScale = Vector3.LerpUnclamped(from.Scales[i], to.Scales[i], weight);
            }
        }

        private void SetVisible()
        {
            bool bridging = _blending && !_fromSnapshot && !IsSamePosture(_fromAction, _action);
            int target = _source ? -1 : PostureOf(_action);
            int from = bridging ? PostureOf(_fromAction) : -1;
            SetActive(_standing, target == PostureStand || from == PostureStand);
            SetActive(_sit, target == PostureSit || from == PostureSit);
            SetActive(_side, target == PostureSide || from == PostureSide);
            for (int i = 0; i < _sources.Length; i++)
                _sources[i].SetActive(_source && i == (_action < 4 ? 0 : _action - 3));
            for (int posture = 0; posture < PostureCount; posture++)
                SetOpacity(posture, posture == target || posture == from ? 1 : 0);
        }

        private void SetActive(GameObject posture, bool active)
        {
            if (posture != null && posture.activeSelf != active)
                posture.SetActive(active);
        }

        private void SetOpacity(int posture, float value)
        {
            if (Mathf.Abs(_opacity[posture] - value) < .001f)
                return;
            ApplyOpacity(posture, value);
        }

        private void ApplyOpacity(int posture, float value)
        {
            _opacity[posture] = value;
            Renderer[] renderers = _postureRenderers[posture];
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(_block);
                _block.SetFloat(OpacityId, value);
                renderers[i].SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// 跨姿态时统一提升目标组，同时保留作者设置的身体部件遮挡顺序。
        /// 结束或取消过渡后恢复原层级，避免透明网格退回不稳定的距离排序。
        /// </summary>
        private void SetSorting(int front)
        {
            bool bridging = _blending && !_fromSnapshot && !IsSamePosture(_fromAction, _action);
            for (int posture = 0; posture < PostureCount; posture++)
            {
                int offset = bridging && posture == front ? _postureSortingSpan : 0;
                Renderer[] renderers = _postureRenderers[posture];
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].sortingOrder = _restSortingOrders[posture][i] + offset;
            }
        }

        private static Renderer[] Collect(GameObject posture) =>
            posture == null ? Array.Empty<Renderer>() : posture.GetComponentsInChildren<Renderer>(true);

        private static int PostureOf(int action) =>
            action < 4 ? PostureStand : action == 4 ? PostureSit : PostureSide;

        private static bool IsSamePosture(int left, int right) => PostureOf(left) == PostureOf(right);

        private static float Smooth(float time) => time * time * (3 - 2 * time);

        private sealed class Pose
        {
            public Vector3 RigPosition;
            public Quaternion RigRotation;
            public Vector3 RigScale;
            public readonly Vector3[] Positions;
            public readonly Quaternion[] Rotations;
            public readonly Vector3[] Scales;

            public Pose(int count)
            {
                Positions = new Vector3[count];
                Rotations = new Quaternion[count];
                Scales = new Vector3[count];
            }
        }
    }
}
