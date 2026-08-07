using System;
using UnityEngine;

namespace SHIN
{
    public enum ParticleSpawnSpace
    {
        Child = 0,
        World = 1,
    }

    public enum SkillCameraCueAction
    {
        Play = 0,
        Release = 1,
    }

    /// <summary>
    /// Animator 상태에 붙여 전투 판정을 보냅니다.
    /// 여러 State를 하나의 논리 애니로 묶을 때 AnimName을 같게 설정하세요. (예: Attack001)
    /// </summary>
    public class CombatAnimStateBehaviour : StateMachineBehaviour
    {
        [Serializable]
        public class JudgmentCue
        {
            [Range(0f, 1f)]
            [Tooltip("상태 normalizedTime (0~1)")]
            public float NormalizedTime = 0.5f;

            public CombatJudgmentType Type = CombatJudgmentType.Hit;

            [Tooltip("Hit 판정 시 카메라 흔들림. None이면 흔들지 않음")]
            public CameraShakeLevel CameraShake = CameraShakeLevel.None;
        }

        [Serializable]
        public class ParticleCue
        {
            [Range(0f, 1f)]
            [Tooltip("파티클을 생성할 상태 normalizedTime (0~1)")]
            public float NormalizedTime = 0.5f;

            [Tooltip("Addressables 파티클 프리팹 주소. 카드 AttackParticlePath가 있으면 그쪽으로 오버라이드된다.")]
            public string ParticleAddress;

            [Tooltip("Child: Animator 자식으로 생성 / World: 월드에 독립 생성")]
            public ParticleSpawnSpace SpawnSpace = ParticleSpawnSpace.World;

            [Tooltip("Animator 기준 위치 오프셋")]
            public Vector3 PositionOffset;

            [Tooltip("Animator 기준 회전 오프셋")]
            public Vector3 RotationOffset;
        }

        [Serializable]
        public class SkillCameraCue
        {
            [Range(0f, 1f)]
            [Tooltip("상태 normalizedTime (0~1)")]
            public float NormalizedTime;

            public SkillCameraCueAction Action = SkillCameraCueAction.Play;

            [Tooltip("Play 시 Addressables 경로. 비면 카드 SkillCameraPath를 사용한다.")]
            public string CameraAddress;
        }

        [Serializable]
        public class CameraShakeCue
        {
            [Range(0f, 1f)]
            [Tooltip("상태 normalizedTime (0~1)")]
            public float NormalizedTime = 0.5f;

            [Tooltip("공격 Hit와 동일한 CameraShakeLevel (Level1~3)")]
            public CameraShakeLevel Level = CameraShakeLevel.Level1;
        }

        [Header("Logical Anim")]
        [Tooltip("카드 AnimationName과 동일한 논리 이름. 비우면 Animator State 이름을 사용합니다.")]
        [SerializeField]
        private string _animName;

        [Header("Hit Setup")]
        [Tooltip("히트 배율 CSV. 예: 1,1,2. 비우면 Setup을 보내지 않습니다(분할 State 후반부용).")]
        [SerializeField]
        private string _hitWeightsCsv = "1";

        [Header("Judgment Timings")]
        [Tooltip("normalizedTime 순으로 Hit/Buff 등을 발사합니다. Hit 개수는 Setup 배율 개수와 맞추세요.")]
        [SerializeField]
        private JudgmentCue[] _judgments = Array.Empty<JudgmentCue>();

        [Header("Particle Timings")]
        [Tooltip("normalizedTime 순으로 파티클 프리팹을 생성합니다.")]
        [SerializeField]
        private ParticleCue[] _particleCues = Array.Empty<ParticleCue>();

        [Header("Skill Camera Timings")]
        [Tooltip("normalizedTime 순으로 스킬 Virtual Camera Play/Release.")]
        [SerializeField]
        private SkillCameraCue[] _skillCameraCues = Array.Empty<SkillCameraCue>();

        [Header("Camera Shake Timings")]
        [Tooltip("normalizedTime 순으로 카메라 흔들림. 공격 Hit와 같은 Level1~3 프리셋.")]
        [SerializeField]
        private CameraShakeCue[] _cameraShakeCues = Array.Empty<CameraShakeCue>();

        public string AnimName => _animName;

        private CharacterBase _character;
        private string _resolvedAnimName;
        private bool _setupSent;
        private int _nextCueIndex;
        private int _nextParticleCueIndex;
        private int _nextSkillCameraCueIndex;
        private int _nextCameraShakeCueIndex;
        private float _lastNormalizedTime;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _character = ResolveCharacter(animator);
            _setupSent = false;
            _nextCueIndex = 0;
            _nextParticleCueIndex = 0;
            _nextSkillCameraCueIndex = 0;
            _nextCameraShakeCueIndex = 0;
            _lastNormalizedTime = 0f;
            _resolvedAnimName = ResolveAnimName(stateInfo);

            SortJudgmentsByTime();
            SortParticleCuesByTime();
            SortSkillCameraCuesByTime();
            SortCameraShakeCuesByTime();

            if (_character != null && !string.IsNullOrEmpty(_resolvedAnimName))
                _character.NotifyCombatAnimEnter(_resolvedAnimName);

            SendSetup();
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_character == null)
                _character = ResolveCharacter(animator);

            bool hasJudgments = _judgments != null && _judgments.Length > 0;
            bool hasParticleCues = _particleCues != null && _particleCues.Length > 0;
            bool hasSkillCameraCues = _skillCameraCues != null && _skillCameraCues.Length > 0;
            bool hasCameraShakeCues = _cameraShakeCues != null && _cameraShakeCues.Length > 0;
            if (!hasJudgments && !hasParticleCues && !hasSkillCameraCues && !hasCameraShakeCues)
                return;

            float t = stateInfo.normalizedTime;
            // 루프 상태면 사이클마다 큐 리셋
            if (stateInfo.loop && Mathf.FloorToInt(t) > Mathf.FloorToInt(_lastNormalizedTime))
            {
                _nextCueIndex = 0;
                _nextParticleCueIndex = 0;
                _nextSkillCameraCueIndex = 0;
                _nextCameraShakeCueIndex = 0;
                _setupSent = false;
                SendSetup();
            }

            float cycleTime = t - Mathf.Floor(t);
            _lastNormalizedTime = t;

            while (hasJudgments && _nextCueIndex < _judgments.Length)
            {
                var cue = _judgments[_nextCueIndex];
                if (cycleTime + 1e-4f < cue.NormalizedTime)
                    break;

                FireJudgment(cue.Type, cue.CameraShake);
                _nextCueIndex++;
            }

            while (hasParticleCues && _nextParticleCueIndex < _particleCues.Length)
            {
                var cue = _particleCues[_nextParticleCueIndex];
                if (cue == null)
                {
                    _nextParticleCueIndex++;
                    continue;
                }

                if (cycleTime + 1e-4f < cue.NormalizedTime)
                    break;

                SpawnParticle(animator, cue);
                _nextParticleCueIndex++;
            }

            while (hasSkillCameraCues && _nextSkillCameraCueIndex < _skillCameraCues.Length)
            {
                var cue = _skillCameraCues[_nextSkillCameraCueIndex];
                if (cue == null)
                {
                    _nextSkillCameraCueIndex++;
                    continue;
                }

                if (cycleTime + 1e-4f < cue.NormalizedTime)
                    break;

                FireSkillCameraCue(cue);
                _nextSkillCameraCueIndex++;
            }

            while (hasCameraShakeCues && _nextCameraShakeCueIndex < _cameraShakeCues.Length)
            {
                var cue = _cameraShakeCues[_nextCameraShakeCueIndex];
                if (cue == null)
                {
                    _nextCameraShakeCueIndex++;
                    continue;
                }

                if (cycleTime + 1e-4f < cue.NormalizedTime)
                    break;

                FireCameraShake(cue.Level);
                _nextCameraShakeCueIndex++;
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_character == null)
                _character = ResolveCharacter(animator);

            if (_character != null && !string.IsNullOrEmpty(_resolvedAnimName))
                _character.NotifyCombatAnimExit(_resolvedAnimName);
        }

        private string ResolveAnimName(AnimatorStateInfo stateInfo)
        {
            if (!string.IsNullOrEmpty(_animName))
                return _animName;

            // State 이름 해시는 직접 복원이 어려워, 비어 있으면 빈 문자열
            // (대기 쪽은 State 이름 IsName 폴백을 사용)
            return _animName;
        }

        private void SendSetup()
        {
            if (_setupSent || _character == null)
                return;

            // 비어 있으면 Setup 생략 → 분할 State 후반에서 Hit 인덱스 리셋 방지
            if (string.IsNullOrWhiteSpace(_hitWeightsCsv))
                return;

            GameManager.Instance?.InGameManager?.OnAnimCombatSetup(_character, _hitWeightsCsv);
            _setupSent = true;
        }

        private void FireJudgment(CombatJudgmentType type, CameraShakeLevel cameraShake)
        {
            if (_character == null)
                return;

            GameManager.Instance?.InGameManager?.OnAnimCombatJudgment(_character, type, 1f, cameraShake);
        }

        private void SpawnParticle(Animator animator, ParticleCue cue)
        {
            if (animator == null || cue == null)
                return;

            if (_character == null)
                _character = ResolveCharacter(animator);

            if (_character == null)
            {
                Debug.LogWarning("[CombatAnim] 파티클 스폰용 CharacterBase 없음");
                return;
            }

            string address = ResolveParticleAddress(cue);
            if (string.IsNullOrWhiteSpace(address))
                return;

            _character.SpawnParticleEffect(
                address,
                cue.SpawnSpace,
                cue.PositionOffset,
                cue.RotationOffset,
                animator.transform);
        }

        /// <summary>
        /// 카드 AttackParticlePath가 있으면 오버라이드, 없으면 Cue 기본 주소.
        /// </summary>
        private string ResolveParticleAddress(ParticleCue cue)
        {
            CardData resolvingCard = GameManager.Instance?.InGameManager?.GetResolvingCard(_character);
            if (resolvingCard != null && !string.IsNullOrWhiteSpace(resolvingCard.AttackParticlePath))
                return resolvingCard.AttackParticlePath;

            return cue != null ? cue.ParticleAddress : null;
        }

        private void FireSkillCameraCue(SkillCameraCue cue)
        {
            if (_character == null || cue == null)
                return;

            var inGame = GameManager.Instance?.InGameManager;
            if (inGame == null)
                return;

            if (cue.Action == SkillCameraCueAction.Release)
            {
                inGame.OnAnimSkillCameraRelease(_character);
                return;
            }

            string address = ResolveSkillCameraAddress(cue);
            inGame.OnAnimSkillCameraPlay(_character, address);
        }

        /// <summary>
        /// 카드 SkillCameraPath가 있으면 우선, 없으면 Cue.CameraAddress.
        /// </summary>
        private string ResolveSkillCameraAddress(SkillCameraCue cue)
        {
            CardData resolvingCard = GameManager.Instance?.InGameManager?.GetResolvingCard(_character);
            if (resolvingCard != null && !string.IsNullOrWhiteSpace(resolvingCard.SkillCameraPath))
                return resolvingCard.SkillCameraPath;

            return cue != null ? cue.CameraAddress : null;
        }

        private static CharacterBase ResolveCharacter(Animator animator)
        {
            if (animator == null)
                return null;

            var character = animator.GetComponentInParent<CharacterBase>();
            if (character == null)
                character = animator.GetComponent<CharacterBase>();
            return character;
        }

        private void SortJudgmentsByTime()
        {
            if (_judgments == null || _judgments.Length <= 1)
                return;

            Array.Sort(_judgments, (a, b) => a.NormalizedTime.CompareTo(b.NormalizedTime));
        }

        private void SortParticleCuesByTime()
        {
            if (_particleCues == null || _particleCues.Length <= 1)
                return;

            Array.Sort(
                _particleCues,
                (a, b) =>
                {
                    if (a == null)
                        return b == null ? 0 : 1;
                    if (b == null)
                        return -1;
                    return a.NormalizedTime.CompareTo(b.NormalizedTime);
                });
        }

        private void SortSkillCameraCuesByTime()
        {
            if (_skillCameraCues == null || _skillCameraCues.Length <= 1)
                return;

            Array.Sort(
                _skillCameraCues,
                (a, b) =>
                {
                    if (a == null)
                        return b == null ? 0 : 1;
                    if (b == null)
                        return -1;
                    return a.NormalizedTime.CompareTo(b.NormalizedTime);
                });
        }

        private void FireCameraShake(CameraShakeLevel level)
        {
            if (level == CameraShakeLevel.None)
                return;

            GameManager.Instance?.CameraManager?.Shake(level);
        }

        private void SortCameraShakeCuesByTime()
        {
            if (_cameraShakeCues == null || _cameraShakeCues.Length <= 1)
                return;

            Array.Sort(
                _cameraShakeCues,
                (a, b) =>
                {
                    if (a == null)
                        return b == null ? 0 : 1;
                    if (b == null)
                        return -1;
                    return a.NormalizedTime.CompareTo(b.NormalizedTime);
                });
        }
    }
}
