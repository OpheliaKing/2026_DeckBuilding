using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 캐릭터 선택 화면용 3D 모델.
    /// InitializeModel에서 lilToon 등장 디졸브를 재생합니다.
    /// </summary>
    public class CharacterSelectModel : MonoBehaviour
    {
        [Header("Appear Dissolve")]
        [SerializeField]
        private Color _appearDissolveEdgeColor = new Color(2.2f, 0.85f, 0.25f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        private float _appearDissolveNoiseStrength = 0.7f;

        [SerializeField]
        private float _appearDissolveBlur = 0.18f;

        [SerializeField]
        private Texture _appearDissolveNoise;

        private CharacterSelectData _data;
        private readonly List<Material> _dissolveMaterials = new();
        private bool _dissolvePrepared;
        private Coroutine _appearRoutine;

        public CharacterSelectData Data => _data;
        public bool IsAppearing => _appearRoutine != null;

        /// <summary>보이스/유닛 공통 TID. CharacterSelectData.UnitDataSOTid (== UnitData.unitTid).</summary>
        public string UnitTid => _data?.UnitDataSOTid;

        private Animator _animator;
        private CharacterWeaponSlot _weaponSlot;
        private int _weaponEquipVersion;

        public void Initialize(CharacterSelectData data)
        {
            _data = data;
        }

        /// <summary>
        /// 무기 미리보기용 애니메이션 재생. AnimName은 Animator State 이름.
        /// </summary>
        public bool PlayAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName))
                return false;

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            if (_animator == null || !_animator.isActiveAndEnabled)
            {
                Debug.LogWarning($"[CharacterSelectModel] Animator 없음: {name} / {animationName}");
                return false;
            }

            int stateHash = Animator.StringToHash(animationName);
            if (!_animator.HasState(0, stateHash))
            {
                Debug.LogWarning($"[CharacterSelectModel] State 없음: {name} / {animationName}");
                return false;
            }

            _animator.Play(stateHash, 0, 0f);
            _animator.Update(0f);
            return true;
        }

        /// <summary>
        /// 선택 화면 무기 미리보기. PrefabEntries를 CharacterWeaponSlot에 장착한다.
        /// </summary>
        public async void EquipWeaponPreview(WeaponData weaponData)
        {
            int version = ++_weaponEquipVersion;

            if (_weaponSlot == null)
                _weaponSlot = GetComponentInChildren<CharacterWeaponSlot>(true);

            if (_weaponSlot == null)
            {
                Debug.LogWarning($"[CharacterSelectModel] CharacterWeaponSlot 없음: {name}");
                return;
            }

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError($"[CharacterSelectModel] ResourceManager 없음: {name}");
                return;
            }

            await _weaponSlot.EquipAsync(weaponData, resourceManager);
            if (version != _weaponEquipVersion)
                return;
        }

        /// <summary>캐릭터 선택 단계로 돌아갈 때 무기만 숨긴다(캐시 유지).</summary>
        public void HideWeaponPreview()
        {
            _weaponEquipVersion++;

            if (_weaponSlot == null)
                _weaponSlot = GetComponentInChildren<CharacterWeaponSlot>(true);

            _weaponSlot?.HideEquipped();
        }

        public void ClearWeaponPreview()
        {
            HideWeaponPreview();
        }

        /// <summary>
        /// 표시용 초기화. 등장 디졸브(완전 소멸 → 나타남)를 재생합니다.
        /// duration은 CharacterSelectObject.Appear Dissolve Duration에서 전달합니다.
        /// </summary>
        public void InitializeModel(float dissolveDuration)
        {
            float duration = Mathf.Max(0.01f, dissolveDuration);

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (!isActiveAndEnabled)
            {
                Debug.LogWarning($"[CharacterSelectModel] InitializeModel 실패(비활성): {name}");
                return;
            }

            if (_appearRoutine != null)
            {
                StopCoroutine(_appearRoutine);
                _appearRoutine = null;
            }

            _appearRoutine = StartCoroutine(AppearDissolveRoutine(duration));
        }

        /// <summary>진행 중인 등장 디졸브를 중단하고 완전히 보이게 둡니다.</summary>
        public void StopAppearDissolve(bool showFully = true)
        {
            if (_appearRoutine != null)
            {
                StopCoroutine(_appearRoutine);
                _appearRoutine = null;
            }

            if (showFully && _dissolvePrepared)
            {
                ApplyDissolveAmount(0f);
                for (int i = 0; i < _dissolveMaterials.Count; i++)
                    LilToonDissolveUtility.SetInvisible(_dissolveMaterials[i], false);
            }
        }

        private IEnumerator AppearDissolveRoutine(float durationSeconds)
        {
            float duration = Mathf.Max(0.01f, durationSeconds);

            if (!EnsureDissolveMaterials())
            {
                _appearRoutine = null;
                yield break;
            }

            // 1 = 안 보임 → 0 = 완전 표시
            ApplyDissolveAmount(1f);
            for (int i = 0; i < _dissolveMaterials.Count; i++)
                LilToonDissolveUtility.SetInvisible(_dissolveMaterials[i], false);

            // 한 프레임 반영 후 진행 (duration은 인자로 고정)
            yield return null;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // UI 연출이라 timescale/히트스톱에 영향받지 않음
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ApplyDissolveAmount(1f - t);
                yield return null;
            }

            ApplyDissolveAmount(0f);
            _appearRoutine = null;
        }

        private bool EnsureDissolveMaterials()
        {
            if (_dissolvePrepared && _dissolveMaterials.Count > 0)
            {
                // 인스턴스가 파괴됐으면 다시 수집
                for (int i = _dissolveMaterials.Count - 1; i >= 0; i--)
                {
                    if (_dissolveMaterials[i] == null)
                        _dissolveMaterials.RemoveAt(i);
                }

                if (_dissolveMaterials.Count > 0)
                    return true;

                _dissolvePrepared = false;
            }

            _dissolveMaterials.Clear();

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null || renderer is ParticleSystemRenderer)
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
                            _appearDissolveEdgeColor,
                            _appearDissolveNoiseStrength,
                            _appearDissolveNoise))
                    {
                        _dissolveMaterials.Add(mat);
                        anyPrepared = true;
                    }
                }

                if (anyPrepared)
                    renderer.materials = mats;
            }

            _dissolvePrepared = _dissolveMaterials.Count > 0;
            if (!_dissolvePrepared)
                Debug.LogWarning($"[CharacterSelectModel] lilToon Dissolve 머티리얼 없음: {name}");

            return _dissolvePrepared;
        }

        private void ApplyDissolveAmount(float amount01)
        {
            amount01 = Mathf.Clamp01(amount01);
            for (int i = 0; i < _dissolveMaterials.Count; i++)
            {
                if (_dissolveMaterials[i] != null)
                    LilToonDissolveUtility.SetDissolveAmount(
                        _dissolveMaterials[i],
                        amount01,
                        _appearDissolveBlur);
            }
        }

        private void OnDisable()
        {
            if (_appearRoutine != null)
            {
                StopCoroutine(_appearRoutine);
                _appearRoutine = null;
            }
        }

        private void OnDestroy()
        {
            StopAppearDissolve(showFully: false);

            for (int i = 0; i < _dissolveMaterials.Count; i++)
            {
                if (_dissolveMaterials[i] != null)
                    Destroy(_dissolveMaterials[i]);
            }

            _dissolveMaterials.Clear();
            _dissolvePrepared = false;
        }
    }
}
