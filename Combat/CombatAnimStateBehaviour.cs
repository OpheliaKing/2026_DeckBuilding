using System;
using UnityEngine;

namespace SHIN
{
    public enum ParticleSpawnSpace
    {
        Child = 0,
        World = 1,
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

            [Tooltip("Addressables 파티클 프리팹 주소")]
            public string ParticleAddress;

            [Tooltip("Child: Animator 자식으로 생성 / World: 월드에 독립 생성")]
            public ParticleSpawnSpace SpawnSpace = ParticleSpawnSpace.World;

            [Tooltip("Animator 기준 위치 오프셋")]
            public Vector3 PositionOffset;

            [Tooltip("Animator 기준 회전 오프셋")]
            public Vector3 RotationOffset;
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

        public string AnimName => _animName;

        private CharacterBase _character;
        private string _resolvedAnimName;
        private bool _setupSent;
        private int _nextCueIndex;
        private int _nextParticleCueIndex;
        private float _lastNormalizedTime;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _character = ResolveCharacter(animator);
            _setupSent = false;
            _nextCueIndex = 0;
            _nextParticleCueIndex = 0;
            _lastNormalizedTime = 0f;
            _resolvedAnimName = ResolveAnimName(stateInfo);

            SortJudgmentsByTime();
            SortParticleCuesByTime();

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
            if (!hasJudgments && !hasParticleCues)
                return;

            float t = stateInfo.normalizedTime;
            // 루프 상태면 사이클마다 큐 리셋
            if (stateInfo.loop && Mathf.FloorToInt(t) > Mathf.FloorToInt(_lastNormalizedTime))
            {
                _nextCueIndex = 0;
                _nextParticleCueIndex = 0;
                _setupSent = false;
                SendSetup();
            }

            float cycleTime = t - Mathf.Floor(t);
            _lastNormalizedTime = t;

            while (_nextCueIndex < _judgments.Length)
            {
                var cue = _judgments[_nextCueIndex];
                if (cycleTime + 1e-4f < cue.NormalizedTime)
                    break;

                FireJudgment(cue.Type, cue.CameraShake);
                _nextCueIndex++;
            }

            while (_nextParticleCueIndex < _particleCues.Length)
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
            if (animator == null || cue == null || string.IsNullOrWhiteSpace(cue.ParticleAddress))
                return;

            if (_character == null)
                _character = ResolveCharacter(animator);

            if (_character == null)
            {
                Debug.LogWarning($"[CombatAnim] 파티클 스폰용 CharacterBase 없음: {cue.ParticleAddress}");
                return;
            }

            _character.SpawnParticleEffect(
                cue.ParticleAddress,
                cue.SpawnSpace,
                cue.PositionOffset,
                cue.RotationOffset,
                animator.transform);
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
    }
}
