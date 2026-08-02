using System.Threading.Tasks;
using UnityEngine;

namespace SHIN
{
    public partial class CharacterBase
    {
        public CHARACTER_EQUIP_TYPE EquipType =>
            _unitInfo != null ? _unitInfo.EquipType : CHARACTER_EQUIP_TYPE.NONE;

        /// <summary>
        /// 현재 장비 타입에 맞는 Animator Controller와 무기 모델을 초기화합니다.
        /// </summary>
        public async Task InitializeEquipmentAsync(ResourceManager resourceManager)
        {
            if (resourceManager == null)
            {
                Debug.LogError($"[Equip] ResourceManager가 없습니다: {name}");
                return;
            }

            if (_unitInfo == null)
            {
                Debug.LogError($"[Equip] UnitInfo가 없습니다: {name}");
                return;
            }

            string animatorAddress = GetAnimatorControllerAddress(_unitInfo.EquipType);
            if (!string.IsNullOrEmpty(animatorAddress))
            {
                var controller = await resourceManager.LoadAsync<RuntimeAnimatorController>(animatorAddress);
                if (controller == null)
                {
                    Debug.LogError($"[Equip] Animator Controller 로드 실패: {name} / {animatorAddress}");
                    return;
                }

                var animator = Animator;
                if (animator == null)
                {
                    Debug.LogError($"[Equip] Animator가 없습니다: {name}");
                    return;
                }

                animator.runtimeAnimatorController = controller;
                animator.Rebind();
                animator.Update(0f);
            }

            await EquipWeaponModelAsync(resourceManager);
        }

        public void SetEquipType(CHARACTER_EQUIP_TYPE equipType)
        {
            if (_unitInfo == null)
            {
                Debug.LogError($"[Equip] UnitInfo가 없어 장비를 설정할 수 없습니다: {name}");
                return;
            }

            _unitInfo.SetEquipType(equipType);
        }

        private static string GetAnimatorControllerAddress(CHARACTER_EQUIP_TYPE equipType)
        {
            switch (equipType)
            {
                case CHARACTER_EQUIP_TYPE.SWORD:
                    return PublicVariable.Address.PlayerAnimatorSword;
                case CHARACTER_EQUIP_TYPE.GUN:
                    return PublicVariable.Address.PlayerAnimatorBow;
                default:
                    return null;
            }
        }

        /// <summary>
        /// WeaponDataSO의 PrefabEntries를 CharacterWeaponSlot 소켓에 장착한다.
        /// </summary>
        private async Task EquipWeaponModelAsync(ResourceManager resourceManager)
        {
            var slot = GetComponentInChildren<CharacterWeaponSlot>(true);
            if (slot == null)
            {
                Debug.LogWarning($"[Equip] CharacterWeaponSlot이 없습니다: {name}");
                return;
            }

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError($"[Equip] GameManager가 없습니다: {name}");
                return;
            }

            WeaponDataSO weaponSO = await gameManager.GetSOAsync<WeaponDataSO>(
                PublicVariable.Address.WeaponDataSO);
            if (weaponSO == null ||
                !weaponSO.TryGetWeaponData(_unitInfo.EquipType, out WeaponData weaponData) ||
                weaponData == null)
            {
                slot.ClearEquipped(resourceManager);
                return;
            }

            await slot.EquipAsync(weaponData, resourceManager);
        }
    }

    public enum CHARACTER_EQUIP_TYPE
    {
        NONE,
        SWORD,
        GUN,
    }
}
