using UnityEngine;

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

        /// <summary>표시용 초기화. 모델을 즉시 켠다.</summary>
        public void InitializeModel()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }
    }
}
