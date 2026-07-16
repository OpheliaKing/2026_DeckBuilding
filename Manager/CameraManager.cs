using Cinemachine;
using UnityEngine;

namespace SHIN
{
    public enum CameraShakeLevel
    {
        None = 0,
        Level1 = 1,
        Level2 = 2,
        Level3 = 3,
    }

    [System.Serializable]
    public struct CameraShakePreset
    {
        [Tooltip("Impulse 세기 (1 = 기본)")]
        public float Force;

        [Tooltip("흔들림 지속 시간(초)")]
        public float Duration;
    }

    /// <summary>
    /// Cinemachine Impulse로 카메라 흔들림을 관리합니다.
    /// Virtual Camera에 Cinemachine Impulse Listener 확장이 필요합니다.
    /// </summary>
    public class CameraManager : ManagerBase
    {
        [SerializeField]
        private CinemachineImpulseSource _impulseSource;

        [Header("Shake Presets (Index 0 = Level1)")]
        [SerializeField]
        private CameraShakePreset[] _shakePresets =
        {
            new CameraShakePreset { Force = 0.35f, Duration = 0.12f },
            new CameraShakePreset { Force = 0.75f, Duration = 0.18f },
            new CameraShakePreset { Force = 1.25f, Duration = 0.28f },
        };

        [Header("Impulse")]
        [SerializeField]
        private Vector3 _shakeDirection = new Vector3(0.08f, -1f, 0.08f);

        [SerializeField]
        [Range(0f, 1f)]
        private float _directionRandomness = 0.25f;

        [Header("Test (Play Mode)")]
        [SerializeField]
        private CameraShakeLevel _testShakeLevel = CameraShakeLevel.Level1;

        private void Awake()
        {
            EnsureImpulseSource();
            WarnIfNoListener();
        }

        /// <summary>None이면 무시, Level1~3으로 흔들림</summary>
        public void Shake(CameraShakeLevel level)
        {
            if (level == CameraShakeLevel.None)
                return;

            if (!TryGetPreset(level, out var preset))
                return;

            FireImpulse(preset);
        }

        /// <summary>0=None, 1~3단계 카메라 흔들림</summary>
        public void Shake(int level)
        {
            if (level < 0 || level > 3)
            {
                Debug.LogWarning($"[CameraManager] Shake level은 0~3만 지원합니다: {level}");
                return;
            }

            Shake((CameraShakeLevel)level);
        }

        /// <summary>Inspector Test Level 기준으로 현재 프리셋 값을 즉시 재생합니다.</summary>
        [ContextMenu("Test Shake (Selected Level)")]
        public void TestShakeSelected()
        {
            EnsureImpulseSource();
            Shake(_testShakeLevel);
            Debug.Log($"[CameraManager] Test Shake: {_testShakeLevel}");
        }

        [ContextMenu("Test Shake / Level1")]
        public void TestShakeLevel1() => TestShake(CameraShakeLevel.Level1);

        [ContextMenu("Test Shake / Level2")]
        public void TestShakeLevel2() => TestShake(CameraShakeLevel.Level2);

        [ContextMenu("Test Shake / Level3")]
        public void TestShakeLevel3() => TestShake(CameraShakeLevel.Level3);

        public void TestShake(CameraShakeLevel level)
        {
            EnsureImpulseSource();
            Shake(level);
            Debug.Log($"[CameraManager] Test Shake: {level}");
        }

        private void FireImpulse(CameraShakePreset preset)
        {
            if (_impulseSource == null)
            {
                Debug.LogWarning("[CameraManager] CinemachineImpulseSource가 없습니다.");
                return;
            }

            var definition = _impulseSource.m_ImpulseDefinition;
            if (definition != null)
                definition.m_ImpulseDuration = Mathf.Max(0.01f, preset.Duration);

            _impulseSource.m_DefaultVelocity = BuildShakeVelocity(preset.Force);
            _impulseSource.GenerateImpulse();
        }

        private Vector3 BuildShakeVelocity(float force)
        {
            var baseDir = _shakeDirection.sqrMagnitude > 1e-6f
                ? _shakeDirection.normalized
                : Vector3.down;

            if (_directionRandomness <= 0f)
                return baseDir * force;

            var randomOffset = new Vector3(
                Random.Range(-_directionRandomness, _directionRandomness),
                Random.Range(-_directionRandomness, _directionRandomness),
                Random.Range(-_directionRandomness, _directionRandomness));

            return (baseDir + randomOffset).normalized * force;
        }

        private bool TryGetPreset(CameraShakeLevel level, out CameraShakePreset preset)
        {
            preset = default;
            int index = (int)level - 1;

            if (_shakePresets == null || index < 0 || index >= _shakePresets.Length)
            {
                Debug.LogWarning($"[CameraManager] Shake preset이 없습니다: {level}");
                return false;
            }

            preset = _shakePresets[index];
            return true;
        }

        private void EnsureImpulseSource()
        {
            if (_impulseSource != null)
                return;

            _impulseSource = GetComponent<CinemachineImpulseSource>();
            if (_impulseSource != null)
                return;

            _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
            ConfigureDefaultImpulse(_impulseSource);
        }

        private static void ConfigureDefaultImpulse(CinemachineImpulseSource source)
        {
            var definition = source.m_ImpulseDefinition;
            definition.m_ImpulseChannel = 1;
            definition.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            definition.m_ImpulseDuration = 0.15f;
            definition.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            source.m_DefaultVelocity = Vector3.down;
        }

        private void WarnIfNoListener()
        {
            if (FindObjectOfType<CinemachineImpulseListener>() != null)
                return;

            Debug.LogWarning(
                "[CameraManager] CinemachineImpulseListener가 없습니다. " +
                "Virtual Camera → Add Extension → Cinemachine Impulse Listener를 추가하세요.");
        }
    }
}
