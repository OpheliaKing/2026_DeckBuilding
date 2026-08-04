using UnityEngine;
using VRM;

namespace SHIN
{
    /// <summary>
    /// 캐릭터 선택 화면용 3D 모델.
    /// </summary>
    public class CharacterSelectModel : MonoBehaviour
    {
        private CharacterSelectData _data;

        public CharacterSelectData Data => _data;

        /// <summary>보이스/유닛 공통 TID. CharacterSelectData.UnitDataSOTid (== UnitData.unitTid).</summary>
        public string UnitTid => _data?.UnitDataSOTid;

        private Animator _animator;
        private CharacterWeaponSlot _weaponSlot;
        private VRMBlendShapeProxy _blendShapeProxy;
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

            EnsureAnimator();

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
        /// 캐시로 숨기기 직전 호출. SetActive(false) 전에 표정/애니 상태를 정리한다.
        /// (비활성 시 StateMachineBehaviour.OnStateExit가 스킵되어 Blink 등이 남을 수 있음)
        /// </summary>
        public void PrepareForHide()
        {
            ResetBlendShapes();
        }

        /// <summary>표시용 초기화. 모델을 즉시 켠 뒤 표정/애니를 기본 상태로 되돌린다.</summary>
        public void InitializeModel()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            PrepareForShow();
        }

        /// <summary>
        /// 다시 보여줄 때 호출. Animator를 기본으로 되돌리고 BlendShape를 0으로 초기화한다.
        /// </summary>
        public void PrepareForShow()
        {
            EnsureAnimator();

            // 이전 선택에서 mid-Blink state가 남아 OnStateUpdate로 다시 1이 되는 것을 막는다.
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.Rebind();
                _animator.Update(0f);
            }

            ResetBlendShapes();
        }

        /// <summary>VRMBlendShapeProxy의 모든 클립 Weight를 0으로 만든다.</summary>
        public void ResetBlendShapes()
        {
            if (_blendShapeProxy == null)
                _blendShapeProxy = GetComponentInChildren<VRMBlendShapeProxy>(true);

            if (_blendShapeProxy == null || _blendShapeProxy.BlendShapeAvatar == null)
                return;

            var clips = _blendShapeProxy.BlendShapeAvatar.Clips;
            if (clips == null)
                return;

            for (int i = 0; i < clips.Count; i++)
            {
                BlendShapeClip clip = clips[i];
                if (clip == null)
                    continue;

                _blendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromClip(clip), 0f);
            }
        }

        private void EnsureAnimator()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);
        }
    }
}
