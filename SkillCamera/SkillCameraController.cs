using Cinemachine;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 스킬용 Virtual Camera 프리팹 루트에 붙입니다.
    /// Follow/LookAt 바인딩과 TrackedDolly PathPosition 커브 재생을 담당합니다.
    /// </summary>
    public class SkillCameraController : MonoBehaviour
    {
        [Header("Type")]
        [SerializeField]
        private SkillCameraType _cameraType = SkillCameraType.TrackedDolly;

        [Header("Target Binding")]
        [Tooltip("Follow / LookAt에 공통으로 쓸 플레이어 Transform. 없으면 Bind 시 인자로 받습니다.")]
        [SerializeField]
        private Transform _playerTarget;

        [SerializeField]
        private bool _bindFollow = true;

        [SerializeField]
        private bool _bindLookAt = true;

        [Header("Virtual Camera")]
        [SerializeField]
        private CinemachineVirtualCamera _virtualCamera;

        [Tooltip("재생 시 메인 전투 카메라보다 높게 올립니다. 0이면 프리팹 Priority 유지.")]
        [SerializeField]
        private int _playPriorityBoost = 50;

        [Header("Tracked Dolly")]
        [Tooltip("X: 재생 시간 비율(0~1), Y: Normalized PathPosition(0~1)")]
        [SerializeField]
        private AnimationCurve _pathPositionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("커브 한 번 재생에 걸리는 시간(초)")]
        [SerializeField]
        private float _dollyDuration = 1.5f;

        [Tooltip("Bind/Reset 시 시작 PathPosition. 커브 Evaluate(0) 대신 쓰려면 사용.")]
        [SerializeField]
        private float _startPathPosition;

        [SerializeField]
        private bool _useCurveStartValue = true;

        [SerializeField]
        private bool _loopPath;

        [SerializeField]
        private bool _playDollyOnBind = true;

        [SerializeField]
        private bool _useUnscaledTime;

        private CinemachineTrackedDolly _trackedDolly;
        private int _defaultPriority;
        private bool _defaultPriorityCached;
        private bool _dollyPlaying;
        private bool _bound;
        private float _dollyElapsed;

        public SkillCameraType CameraType => _cameraType;
        public Transform PlayerTarget => _playerTarget;
        public CinemachineVirtualCamera VirtualCamera => _virtualCamera;
        public bool IsDollyPlaying => _dollyPlaying;
        public AnimationCurve PathPositionCurve => _pathPositionCurve;

        public float DollyDuration
        {
            get => _dollyDuration;
            set => _dollyDuration = Mathf.Max(0.01f, value);
        }

        /// <summary>호환용. 초당 PathPosition 증가량으로 Duration을 역산합니다 (1/speed).</summary>
        public float PathPositionSpeed
        {
            get => _dollyDuration > 0f ? 1f / _dollyDuration : 0f;
            set
            {
                if (value <= 0f)
                    return;
                DollyDuration = 1f / value;
            }
        }

        public float PathPosition
        {
            get => _trackedDolly != null ? _trackedDolly.m_PathPosition : 0f;
            set
            {
                if (_trackedDolly == null)
                    return;
                _trackedDolly.m_PathPosition = value;
            }
        }

        private void Awake()
        {
            CacheReferences();
            EnsureDefaultCurve();
        }

        private void Update()
        {
            if (!_dollyPlaying || _cameraType != SkillCameraType.TrackedDolly || _trackedDolly == null)
                return;

            float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _dollyElapsed += dt;

            float duration = Mathf.Max(0.01f, _dollyDuration);
            float t = _dollyElapsed / duration;

            if (_loopPath)
            {
                t = Mathf.Repeat(t, 1f);
            }
            else if (t >= 1f)
            {
                t = 1f;
                _dollyPlaying = false;
            }

            _trackedDolly.m_PathPosition = EvaluatePathPosition(t);
        }

        /// <summary>
        /// 플레이어를 Follow/LookAt에 바인딩하고 재생을 시작합니다.
        /// </summary>
        public void Bind(Transform playerRoot)
        {
            CacheReferences();
            EnsureDefaultCurve();

            if (playerRoot == null)
            {
                Debug.LogWarning("[SkillCameraController] playerRoot가 null입니다.", this);
                return;
            }

            _playerTarget = playerRoot;

            if (_virtualCamera == null)
            {
                Debug.LogError("[SkillCameraController] CinemachineVirtualCamera가 없습니다.", this);
                return;
            }

            if (_bindFollow)
                _virtualCamera.Follow = _playerTarget;

            if (_bindLookAt)
                _virtualCamera.LookAt = _playerTarget;

            ApplyCameraTypeHints();
            BoostPriority();

            if (_cameraType == SkillCameraType.TrackedDolly && _trackedDolly != null)
            {
                _trackedDolly.m_PositionUnits = CinemachinePathBase.PositionUnits.Normalized;
                ResetDolly();
                _dollyPlaying = _playDollyOnBind && _dollyDuration > 0f;
            }
            else
            {
                _dollyPlaying = false;
            }

            _bound = true;
            if (!_virtualCamera.enabled)
                _virtualCamera.enabled = true;
        }

        public void SetPathPositionSpeed(float speed)
        {
            PathPositionSpeed = speed;
            if (_bound && _cameraType == SkillCameraType.TrackedDolly && speed > 0f)
                _dollyPlaying = true;
        }

        public void SetDollyDuration(float duration)
        {
            DollyDuration = duration;
            if (_bound && _cameraType == SkillCameraType.TrackedDolly && duration > 0f)
                _dollyPlaying = true;
        }

        public void SetPathPositionCurve(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
                return;

            _pathPositionCurve = curve;
        }

        public void PlayDolly()
        {
            if (_cameraType != SkillCameraType.TrackedDolly || _trackedDolly == null)
                return;

            _dollyElapsed = 0f;
            ApplyPathPositionAt(0f);
            _dollyPlaying = _dollyDuration > 0f;
        }

        public void PauseDolly()
        {
            _dollyPlaying = false;
        }

        public void ResetDolly(float pathPosition = -1f)
        {
            _dollyElapsed = 0f;

            if (_trackedDolly == null)
                return;

            if (pathPosition >= 0f)
                _trackedDolly.m_PathPosition = pathPosition;
            else
                ApplyPathPositionAt(0f);
        }

        public void RestorePriority()
        {
            if (_virtualCamera == null)
                return;

            _virtualCamera.Priority = _defaultPriority;
        }

        private float EvaluatePathPosition(float normalizedTime)
        {
            if (_pathPositionCurve == null || _pathPositionCurve.length == 0)
                return Mathf.Clamp01(normalizedTime);

            return _pathPositionCurve.Evaluate(normalizedTime);
        }

        private void ApplyPathPositionAt(float normalizedTime)
        {
            if (_trackedDolly == null)
                return;

            if (_useCurveStartValue || normalizedTime > 0f)
                _trackedDolly.m_PathPosition = EvaluatePathPosition(normalizedTime);
            else
                _trackedDolly.m_PathPosition = _startPathPosition;
        }

        private void CacheReferences()
        {
            if (_virtualCamera == null)
                _virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>(true);

            if (_virtualCamera != null)
            {
                if (!_defaultPriorityCached)
                {
                    _defaultPriority = _virtualCamera.Priority;
                    _defaultPriorityCached = true;
                }

                _trackedDolly = _virtualCamera.GetCinemachineComponent<CinemachineTrackedDolly>();
            }
        }

        private void BoostPriority()
        {
            if (_virtualCamera == null || _playPriorityBoost <= 0)
                return;

            _virtualCamera.Priority = _defaultPriority + _playPriorityBoost;
        }

        private void EnsureDefaultCurve()
        {
            if (_pathPositionCurve != null && _pathPositionCurve.length > 0)
                return;

            _pathPositionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        /// <summary>
        /// 프리팹에 이미 Body가 붙어 있다고 가정하고, 타입만 기록/검증합니다.
        /// Body 컴포넌트 교체는 프리팹 쪽에서 구성합니다.
        /// </summary>
        private void ApplyCameraTypeHints()
        {
            if (_virtualCamera == null)
                return;

            switch (_cameraType)
            {
                case SkillCameraType.TrackedDolly:
                    if (_trackedDolly == null)
                    {
                        Debug.LogWarning(
                            "[SkillCameraController] TrackedDolly 타입인데 CinemachineTrackedDolly가 없습니다.",
                            this);
                    }
                    break;

                case SkillCameraType.Follow:
                    if (_virtualCamera.GetCinemachineComponent<CinemachineTransposer>() == null &&
                        _virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>() == null)
                    {
                        Debug.LogWarning(
                            "[SkillCameraController] Follow 타입인데 Transposer/FramingTransposer가 없습니다.",
                            this);
                    }
                    break;

                case SkillCameraType.HardLock:
                    if (_virtualCamera.GetCinemachineComponent<CinemachineHardLockToTarget>() == null)
                    {
                        Debug.LogWarning(
                            "[SkillCameraController] HardLock 타입인데 HardLockToTarget이 없습니다.",
                            this);
                    }
                    break;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_virtualCamera == null)
                _virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>(true);

            if (_dollyDuration < 0.01f)
                _dollyDuration = 0.01f;

            EnsureDefaultCurve();
        }
#endif
    }
}
