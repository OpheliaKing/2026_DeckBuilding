using System.Collections;
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

        /// <summary>
        /// 카드 애니메이션 재생. 없으면 false.
        /// 데미지/버프 판정은 Animator 상태의 CombatAnimStateBehaviour에서 처리합니다.
        /// </summary>
        public bool TryPlayCardAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName))
                return false;

            var animator = Animator;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                Debug.LogWarning($"[Anim] Animator 없음: {name} / {animationName}");
                return false;
            }

            animator.Play(animationName, 0, 0f);
            animator.Update(0f);
            return true;
        }

        public IEnumerator WaitCurrentAnimationEnd(string animationName, float timeout = 10f)
        {
            var animator = Animator;
            if (animator == null)
                yield break;

            float elapsed = 0f;
            // 상태 진입 대기
            yield return null;

            while (elapsed < timeout)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.IsName(animationName))
                {
                    if (info.normalizedTime >= 1f && !animator.IsInTransition(0))
                        yield break;
                }
                else if (elapsed > 0.15f)
                {
                    // 다른 상태로 넘어갔으면 종료로 간주
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.LogWarning($"[Anim] 애니메이션 대기 타임아웃: {animationName}");
        }
    }
}
