using System.Collections;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 전투/연출용 시간 배율.
    /// - CharacterTimeScale: 히트스톱 등 캐릭터 애니·이펙트 속도
    /// - PauseTimeScale: 일시정지 (0 = 정지, 1 = 정상). Unity Time.timeScale에 반영
    /// </summary>
    public class TimeManager : ManagerBase
    {
        [Header("Character Time")]
        [SerializeField]
        [Tooltip("캐릭터 애니/이펙트 속도. 히트스톱 시 잠깐 0으로 둡니다.")]
        private float _characterTimeScale = 1f;

        [SerializeField]
        [Tooltip("히트스톱 기본 시간(초). 비스케일 시간 기준.")]
        private float _defaultHitStopDuration = 0.05f;

        [Header("Pause Time")]
        [SerializeField]
        [Tooltip("일시정지 배율. 0 = 일시정지, 1 = 정상.")]
        private float _pauseTimeScale = 1f;

        private Coroutine _hitStopRoutine;
        private float _characterTimeScaleBeforeHitStop = 1f;
        private bool _isHitStopping;

        /// <summary>캐릭터/이펙트용 시간 배율 (히트스톱에 사용)</summary>
        public float CharacterTimeScale => _characterTimeScale;

        /// <summary>일시정지용 시간 배율 (0=정지, 1=정상)</summary>
        public float PauseTimeScale => _pauseTimeScale;

        public bool IsPaused => _pauseTimeScale <= 0f;

        /// <summary>
        /// 캐릭터에 실제로 적용할 배율.
        /// 일시정지면 0, 아니면 CharacterTimeScale.
        /// </summary>
        public float EffectiveCharacterTimeScale =>
            IsPaused ? 0f : Mathf.Max(0f, _characterTimeScale);

        private void Awake()
        {
            ApplyPauseToUnityTime();
        }

        private void OnDisable()
        {
            if (_isHitStopping)
            {
                _characterTimeScale = _characterTimeScaleBeforeHitStop;
                _isHitStopping = false;
            }
        }

        /// <summary>캐릭터 시간 배율 설정 (히트스톱 중이 아닐 때)</summary>
        public void SetCharacterTimeScale(float scale)
        {
            scale = Mathf.Max(0f, scale);
            _characterTimeScale = scale;

            if (!_isHitStopping)
                _characterTimeScaleBeforeHitStop = scale;
        }

        /// <summary>일시정지 배율 설정. 0=일시정지, 1=정상. Time.timeScale에 반영됩니다.</summary>
        public void SetPauseTimeScale(float scale)
        {
            _pauseTimeScale = Mathf.Clamp01(scale);
            ApplyPauseToUnityTime();
        }

        public void SetPaused(bool paused)
        {
            SetPauseTimeScale(paused ? 0f : 1f);
        }

        /// <summary>
        /// 카메라 쉐이크와 맞추는 히트스톱.
        /// CharacterTimeScale만 잠깐 0으로 두고, Pause/Time.timeScale은 건드리지 않습니다.
        /// </summary>
        public void HitStop(float duration = -1f)
        {
            if (duration < 0f)
                duration = _defaultHitStopDuration;

            if (duration <= 0f || IsPaused)
                return;

            if (_hitStopRoutine != null)
                StopCoroutine(_hitStopRoutine);

            _hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            if (!_isHitStopping)
                _characterTimeScaleBeforeHitStop = _characterTimeScale;

            _isHitStopping = true;
            _characterTimeScale = 0f;

            yield return new WaitForSecondsRealtime(duration);

            _characterTimeScale = _characterTimeScaleBeforeHitStop;
            _isHitStopping = false;
            _hitStopRoutine = null;
        }

        private void ApplyPauseToUnityTime()
        {
            Time.timeScale = Mathf.Max(0f, _pauseTimeScale);
        }
    }
}
