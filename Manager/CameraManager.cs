using System;
using System.Threading.Tasks;
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
    /// Cinemachine Impulse 흔들림 + 스킬 Virtual Camera 스폰을 관리합니다.
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
            new CameraShakePreset { Force = 0.2f, Duration = 0.12f },
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

        private SkillCameraController _activeSkillCamera;
        private int _skillCameraPlayId;

        /// <summary>현재 재생 중인 스킬 카메라. 없으면 null.</summary>
        public SkillCameraController ActiveSkillCamera => _activeSkillCamera;

        private void Awake()
        {
            EnsureImpulseSource();
            WarnIfNoListener();
        }

        /// <summary>
        /// Addressables 스킬 카메라 프리팹을 플레이어 루트 자식으로 생성하고 Follow/LookAt을 바인딩합니다.
        /// 동시에 하나만 유지하며, 새 재생 시 기존 카메라를 해제합니다.
        /// </summary>
        public async Task<SkillCameraController> PlaySkillCameraAsync(
            string address,
            Transform playerRoot,
            float? pathPositionSpeedOverride = null)
        {
            if (playerRoot == null)
            {
                Debug.LogError("[CameraManager] PlaySkillCameraAsync: playerRoot가 null입니다.");
                return null;
            }

            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError("[CameraManager] PlaySkillCameraAsync: address가 비어 있습니다.");
                return null;
            }

            ReleaseSkillCamera();
            int playId = _skillCameraPlayId;

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[CameraManager] ResourceManager가 없습니다.");
                return null;
            }

            GameObject instance = await resourceManager.InstantiateAsync(
                address,
                playerRoot,
                instantiateInWorldSpace: false);

            // Release가 await 중에 호출되면 이 인스턴스는 폐기
            if (playId != _skillCameraPlayId)
            {
                if (instance != null)
                    resourceManager.ReleaseInstance(instance);
                return null;
            }

            if (instance == null)
                return null;

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var controller = instance.GetComponent<SkillCameraController>();
            if (controller == null)
                controller = instance.AddComponent<SkillCameraController>();

            if (pathPositionSpeedOverride.HasValue)
                controller.SetPathPositionSpeed(pathPositionSpeedOverride.Value);

            controller.Bind(playerRoot);
            _activeSkillCamera = controller;
            return controller;
        }

        /// <summary>콜백 방식 PlaySkillCamera.</summary>
        public void PlaySkillCamera(
            string address,
            Transform playerRoot,
            Action<SkillCameraController> onComplete = null,
            float? pathPositionSpeedOverride = null)
        {
            PlaySkillCameraInternal(address, playerRoot, onComplete, pathPositionSpeedOverride);
        }

        private async void PlaySkillCameraInternal(
            string address,
            Transform playerRoot,
            Action<SkillCameraController> onComplete,
            float? pathPositionSpeedOverride)
        {
            var controller = await PlaySkillCameraAsync(address, playerRoot, pathPositionSpeedOverride);
            onComplete?.Invoke(controller);
        }

        /// <summary>현재 스킬 카메라를 해제하고 전투 카메라로 복귀합니다.</summary>
        public void ReleaseSkillCamera()
        {
            _skillCameraPlayId++;

            if (_activeSkillCamera == null)
                return;

            var go = _activeSkillCamera.gameObject;
            _activeSkillCamera.RestorePriority();
            _activeSkillCamera = null;

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager != null)
                resourceManager.ReleaseInstance(go);
            else if (go != null)
                Destroy(go);
        }

        private void OnDestroy()
        {
            ReleaseSkillCamera();
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
                UnityEngine.Random.Range(-_directionRandomness, _directionRandomness),
                UnityEngine.Random.Range(-_directionRandomness, _directionRandomness),
                UnityEngine.Random.Range(-_directionRandomness, _directionRandomness));

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

