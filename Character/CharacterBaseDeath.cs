using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class CharacterBase
    {
        [Header("Death Dissolve")]
        [SerializeField]
        private float _deathDissolveDuration = 1.5f;

        [SerializeField]
        private Color _deathDissolveEdgeColor = new Color(2.2f, 0.85f, 0.25f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        private float _deathDissolveNoiseStrength = 0.7f;

        [SerializeField]
        private float _deathDissolveBlur = 0.18f;

        [SerializeField]
        private Texture _deathDissolveNoise;

        private bool _isDissolving;
        private bool _deathVisualCompleted;
        private readonly List<Material> _dissolveMaterials = new();
        private readonly List<Renderer> _dissolveRenderers = new();

        public bool IsDissolving => _isDissolving;
        public bool HasCompletedDeathVisual => _deathVisualCompleted;

        public const string DeathAnimationName = "Die";

        /// <summary>
        /// Die 애니 + lilToon Dissolve. 한 캐릭터당 한 번만 실행됩니다.
        /// </summary>
        public IEnumerator PlayDeathDissolve(bool playDeathAnimation = true)
        {
            if (_isDissolving || _deathVisualCompleted)
                yield break;

            _isDissolving = true;

            if (playDeathAnimation)
            {
                if (!TryPlayAnimation(DeathAnimationName))
                    TryPlayAnimation("Death");
            }

            if (!CollectAndPrepareDissolveMaterials())
            {
                Debug.LogWarning($"[Death] lilToon Dissolve 머티리얼 없음 → Die만 대기: {name}");
                if (playDeathAnimation)
                    yield return WaitCurrentAnimationEnd(DeathAnimationName, 2.5f);
                FinishDeathVisual();
                yield break;
            }

            float duration = Mathf.Max(0.5f, _deathDissolveDuration);
            float elapsed = 0f;

            ApplyDissolveAmount(0f);
            yield return null;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // 커브 직렬화 깨짐으로 항상 0이 나오던 이슈가 있어 선형 보간 사용
                float t = Mathf.Clamp01(elapsed / duration);
                ApplyDissolveAmount(t);
                yield return null;
            }

            ApplyDissolveAmount(1f);

            for (int i = 0; i < _dissolveMaterials.Count; i++)
                LilToonDissolveUtility.SetInvisible(_dissolveMaterials[i], true);

            FinishDeathVisual();
        }

        private void FinishDeathVisual()
        {
            for (int i = 0; i < _dissolveRenderers.Count; i++)
            {
                if (_dissolveRenderers[i] != null)
                    _dissolveRenderers[i].enabled = false;
            }

            var allRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null)
                    allRenderers[i].enabled = false;
            }

            var animator = Animator;
            if (animator != null)
                animator.enabled = false;

            _deathVisualCompleted = true;
            _isDissolving = false;
        }

        private bool CollectAndPrepareDissolveMaterials()
        {
            _dissolveMaterials.Clear();
            _dissolveRenderers.Clear();

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer)
                    continue;

                var mats = renderer.materials;
                bool anyPrepared = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null)
                        continue;

                    if (!LilToonDissolveUtility.IsLilToonMaterial(mat))
                        continue;

                    if (LilToonDissolveUtility.PrepareForDissolve(
                            mat,
                            _deathDissolveEdgeColor,
                            _deathDissolveNoiseStrength,
                            _deathDissolveNoise))
                    {
                        _dissolveMaterials.Add(mat);
                        anyPrepared = true;
                    }
                }

                if (anyPrepared)
                {
                    renderer.materials = mats;
                    _dissolveRenderers.Add(renderer);
                }
            }

            Debug.Log($"[Death] Dissolve 준비: {_dissolveMaterials.Count} materials / {name}");
            return _dissolveMaterials.Count > 0;
        }

        private void ApplyDissolveAmount(float amount01)
        {
            amount01 = Mathf.Clamp01(amount01);

            for (int i = 0; i < _dissolveMaterials.Count; i++)
            {
                var mat = _dissolveMaterials[i];
                if (mat != null)
                    LilToonDissolveUtility.SetDissolveAmount(mat, amount01, _deathDissolveBlur);
            }
        }

        private void CleanupDissolveMaterials(bool destroyInstances)
        {
            if (destroyInstances)
            {
                for (int i = 0; i < _dissolveMaterials.Count; i++)
                {
                    if (_dissolveMaterials[i] != null)
                        Destroy(_dissolveMaterials[i]);
                }
            }

            _dissolveMaterials.Clear();
            _dissolveRenderers.Clear();
        }

        private void OnDestroy()
        {
            CleanupDissolveMaterials(destroyInstances: true);
        }
    }
}
