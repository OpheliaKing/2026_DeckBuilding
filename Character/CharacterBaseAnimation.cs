using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class CharacterBase
    {
        [SerializeField]
        private Animator _animator;

        public Animator Animator
        {
            get
            {
                if (_animator == null)
                    _animator = GetComponentInChildren<Animator>();
                return _animator;
            }
        }

        public const string HitAnimationName = "Hit";

        /// <summary>
        /// CombatAnimStateBehaviour.AnimName 기준 논리 애니 참조 카운트.
        /// 분할 State 전환 중에도 같은 AnimName이면 재생 중으로 취급합니다.
        /// </summary>
        private readonly Dictionary<string, int> _combatAnimRefCounts = new();

        /// <summary>
        /// 카드 애니메이션 재생. 없으면 false.
        /// 데미지/버프 판정은 Animator 상태의 CombatAnimStateBehaviour에서 처리합니다.
        /// </summary>
        public bool TryPlayCardAnimation(string animationName)
        {
            return TryPlayAnimation(animationName);
        }

        /// <summary>피격 시 Hit 애니메이션 재생</summary>
        public bool PlayHitAnimation()
        {
            return TryPlayAnimation(HitAnimationName);
        }

        public bool TryPlayAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName))
                return false;

            var animator = Animator;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                Debug.LogWarning($"[Anim] Animator 없음: {name} / {animationName}");
                return false;
            }

            int stateHash = Animator.StringToHash(animationName);
            if (!animator.HasState(0, stateHash))
            {
                // Suriyun 등 일부 컨트롤러는 피격 상태가 Damage
                if (animationName == HitAnimationName)
                {
                    const string damageFallback = "Damage";
                    int damageHash = Animator.StringToHash(damageFallback);
                    if (animator.HasState(0, damageHash))
                    {
                        animator.Play(damageHash, 0, 0f);
                        animator.Update(0f);
                        return true;
                    }
                }

                Debug.LogWarning($"[Anim] State 없음: {name} / {animationName}");
                return false;
            }

            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
            return true;
        }

        public void NotifyCombatAnimEnter(string animName)
        {
            if (string.IsNullOrEmpty(animName))
                return;

            if (_combatAnimRefCounts.TryGetValue(animName, out int count))
                _combatAnimRefCounts[animName] = count + 1;
            else
                _combatAnimRefCounts[animName] = 1;
        }

        public void NotifyCombatAnimExit(string animName)
        {
            if (string.IsNullOrEmpty(animName))
                return;

            if (!_combatAnimRefCounts.TryGetValue(animName, out int count))
                return;

            count--;
            if (count <= 0)
                _combatAnimRefCounts.Remove(animName);
            else
                _combatAnimRefCounts[animName] = count;
        }

        /// <summary>
        /// 논리 AnimName 또는 Animator State 이름이 재생 중이면 true.
        /// 같은 AnimName의 분할 State 전환 중에도 true를 유지합니다.
        /// </summary>
        public bool IsCombatAnimPlaying(string animationName)
        {
            if (string.IsNullOrEmpty(animationName))
                return false;

            if (_combatAnimRefCounts.TryGetValue(animationName, out int count) && count > 0)
                return true;

            var animator = Animator;
            if (animator == null)
                return false;

            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.IsName(animationName))
                return true;

            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(animationName))
                    return true;
            }

            return false;
        }

        public IEnumerator WaitCurrentAnimationEnd(string animationName, float timeout = 10f)
        {
            var animator = Animator;
            if (animator == null)
                yield break;

            float elapsed = 0f;
            bool seenPlaying = false;

            // 상태 진입 / SMB Enter 통지 대기
            yield return null;

            while (elapsed < timeout)
            {
                bool playing = IsCombatAnimPlaying(animationName);

                if (playing)
                {
                    seenPlaying = true;

                    // 논리 AnimName으로 묶인 동안은 종료하지 않음.
                    // State 이름만 일치하고 ref가 없을 때만 normalizedTime으로 단일 State 종료 판정.
                    bool hasLogicalRef =
                        _combatAnimRefCounts.TryGetValue(animationName, out int refCount) && refCount > 0;

                    if (!hasLogicalRef)
                    {
                        var info = animator.GetCurrentAnimatorStateInfo(0);
                        if (info.IsName(animationName) &&
                            info.normalizedTime >= 1f &&
                            !animator.IsInTransition(0))
                        {
                            yield break;
                        }
                    }
                }
                else if (seenPlaying)
                {
                    // 한 번이라도 재생된 뒤 논리/State가 모두 끝나면 종료
                    // 전환 프레임 오차를 한 프레임 더 확인
                    yield return null;
                    if (!IsCombatAnimPlaying(animationName))
                        yield break;
                }
                else if (elapsed > 0.25f)
                {
                    // 시작조차 못 했으면 종료
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.LogWarning($"[Anim] 애니메이션 대기 타임아웃: {animationName}");
        }
    }
}
