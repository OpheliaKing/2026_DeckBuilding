using System;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// Animator 상태에 붙여 전투 판정을 보냅니다.
    /// Animator Controller → 해당 Attack State → Add Behaviour → CombatAnimStateBehaviour
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

        [Header("Hit Setup")]
        [Tooltip("히트 배율 CSV. 예: 1,1,2 → 총 데미지를 1:1:2로 분할")]
        [SerializeField]
        private string _hitWeightsCsv = "1";

        [Header("Judgment Timings")]
        [Tooltip("normalizedTime 순으로 Hit/Buff 등을 발사합니다. Hit 개수는 Setup 배율 개수와 맞추세요.")]
        [SerializeField]
        private JudgmentCue[] _judgments = Array.Empty<JudgmentCue>();

        private CharacterBase _character;
        private bool _setupSent;
        private int _nextCueIndex;
        private float _lastNormalizedTime;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _character = ResolveCharacter(animator);
            _setupSent = false;
            _nextCueIndex = 0;
            _lastNormalizedTime = 0f;

            SortJudgmentsByTime();
            SendSetup();
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_character == null)
                _character = ResolveCharacter(animator);

            if (_judgments == null || _judgments.Length == 0)
                return;

            float t = stateInfo.normalizedTime;
            // 루프 상태면 사이클마다 큐 리셋
            if (stateInfo.loop && Mathf.FloorToInt(t) > Mathf.FloorToInt(_lastNormalizedTime))
            {
                _nextCueIndex = 0;
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
        }

        private void SendSetup()
        {
            if (_setupSent || _character == null)
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
    }
}
