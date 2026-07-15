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
        private AnimationCurve _deathDissolveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField]
        private Color _deathDissolveEdgeColor = new Color(2.2f, 0.85f, 0.25f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        private float _deathDissolveNoiseStrength = 0.55f;

        [SerializeField]
        private float _deathDissolveBlur = 0.18f;

        [SerializeField]
        private Texture _deathDissolveNoise;

        private bool _isDissolving;
        private readonly List<Material> _dissolveMaterials = new();

        public bool IsDissolving => _isDissolving;

        public const string DeathAnimationName = "Die";

        /// <summary>
        /// lilToon Dissolve로 사망 연출합니다.
        /// </summary>
        public IEnumerator PlayDeathDissolve()
        {
            if (_isDissolving)
                yield break;

            _isDissolving = true;

            if (!TryPlayAnimation(DeathAnimationName))
                TryPlayAnimation("Death");

            if (!CollectAndPrepareDissolveMaterials())
            {
                Debug.LogWarning($"[Death] lilToon Dissolve 머티리얼 없음 → 애니만 대기 후 종료: {name}");
                yield return WaitCurrentAnimationEnd(DeathAnimationName, 2.5f);
                _isDissolving = false;
                yield break;
            }

            float duration = Mathf.Max(0.5f, _deathDissolveDuration);
            float elapsed = 0f;

            ApplyDissolveAmount(0f);
            yield return null;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ApplyDissolveAmount(EvaluateDissolveCurve(t));
                yield return null;
            }

            ApplyDissolveAmount(1f);

            for (int i = 0; i < _dissolveMaterials.Count; i++)
                LilToonDissolveUtility.SetInvisible(_dissolveMaterials[i], true);

            _isDissolving = false;
        }

        private float EvaluateDissolveCurve(float t)
        {
            if (_deathDissolveCurve == null || _deathDissolveCurve.length <= 0)
                return t;

            float v = _deathDissolveCurve.Evaluate(t);
            if (v <= 0.0001f && t > 0.05f)
                return t;

            return Mathf.Clamp01(v);
        }

        private bool CollectAndPrepareDissolveMaterials()
        {
            CleanupDissolveMaterials(destroyInstances: true);

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer)
                    continue;

                // materials → 인스턴스 (공유 에셋 오염 방지)
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
                        Debug.Log($"[Death] lilToon Dissolve 적용: {renderer.name} / {mat.shader.name}");
                    }
                }

                if (anyPrepared)
                    renderer.materials = mats;
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
        }

        private void OnDestroy()
        {
            CleanupDissolveMaterials(destroyInstances: true);
        }
    }
}
