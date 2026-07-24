using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 캐릭터 선택 → 무기 선택 흐름을 조율한다.
    /// 자식 UI는 선택 데이터만 콜백으로 올리고, GameManager 저장은 여기서 처리한다.
    /// </summary>
    public class UnitSetupUI : UIBase
    {
        [SerializeField]
        private CharacterSelectUI _characterSelectUI;

        [SerializeField]
        private WeaponSelectUI _weaponSelectUI;

        [SerializeField]
        private UnityEngine.UI.Button _confirmButton;

        private CharacterSelectData _selectedCharacter;
        private WeaponData _selectedWeapon;
        private bool _isSaving;
        private bool _isWeaponStep;

        /// <summary>슬롯 미리보기용. CharacterSelectObject 모델 갱신 등에 사용.</summary>
        public event Action<CharacterSelectData> OnCharacterPreviewChanged;

        /// <summary>유닛 세팅 완료(저장) 후.</summary>
        public event Action<UnitInfo> OnSetupCompleted;

        private void Awake()
        {
            ResolveChildUIs();
            BindConfirmButton();
        }

        /// <summary>
        /// 캐릭터 선택 단계부터 시작한다.
        /// </summary>
        public void BeginSetup()
        {
            ResolveChildUIs();
            BindConfirmButton();
            _selectedCharacter = null;
            _selectedWeapon = null;
            _isSaving = false;
            _isWeaponStep = false;
            BeginCharacterSelectAsync();
        }

        /// <summary>
        /// 공유 확정 버튼. Inspector 또는 SelectButton에서 연결.
        /// </summary>
        public void OnClickConfirm()
        {
            if (_isWeaponStep)
                _weaponSelectUI?.OnClickConfirm();
            else
                _characterSelectUI?.OnClickConfirm();
        }

        private void BindConfirmButton()
        {
            if (_confirmButton == null)
                _confirmButton = FindSelectButton(transform);

            if (_confirmButton == null)
                return;

            // 단계 전환 시에도 보이도록 UnitSetupUI 하위로 유지
            if (_confirmButton.transform.parent != transform)
                _confirmButton.transform.SetParent(transform, true);

            _confirmButton.onClick.RemoveListener(OnClickConfirm);
            _confirmButton.onClick.AddListener(OnClickConfirm);
        }

        private static UnityEngine.UI.Button FindSelectButton(Transform root)
        {
            UnityEngine.UI.Button[] buttons = root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.name == "SelectButton")
                    return buttons[i];
            }

            return null;
        }

        private void ResolveChildUIs()
        {
            if (_characterSelectUI == null)
                _characterSelectUI = GetComponentInChildren<CharacterSelectUI>(true);

            if (_weaponSelectUI == null)
                _weaponSelectUI = GetComponentInChildren<WeaponSelectUI>(true);
        }

        private async void BeginCharacterSelectAsync()
        {
            if (_characterSelectUI == null)
            {
                Debug.LogError("[UnitSetupUI] CharacterSelectUI가 없습니다.");
                return;
            }

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[UnitSetupUI] GameManager.Instance가 없습니다.");
                return;
            }

            CharacterSelectDataSO characterSO = await gameManager.GetSOAsync<CharacterSelectDataSO>(
                PublicVariable.Address.CharacterSelectDataSO);

            if (characterSO == null || characterSO.Count == 0)
            {
                Debug.LogError("[UnitSetupUI] CharacterSelectDataSO 로드 실패.");
                return;
            }

            ShowCharacterStep();

            var list = new List<CharacterSelectData>(characterSO.CharacterSelectDatas);
            _characterSelectUI.Setup(list, OnCharacterConfirmed, OnCharacterPreview);
        }

        private void OnCharacterPreview(CharacterSelectData data)
        {
            if (data == null)
                return;

            _selectedCharacter = data;
            OnCharacterPreviewChanged?.Invoke(data);
        }

        private void OnCharacterConfirmed(CharacterSelectData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[UnitSetupUI] 확정된 캐릭터 데이터가 null입니다.");
                return;
            }

            _selectedCharacter = data;
            OnCharacterPreviewChanged?.Invoke(data);
            BeginWeaponSelectAsync();
        }

        private async void BeginWeaponSelectAsync()
        {
            if (_weaponSelectUI == null)
            {
                Debug.LogError("[UnitSetupUI] WeaponSelectUI가 없습니다.");
                return;
            }

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[UnitSetupUI] GameManager.Instance가 없습니다.");
                return;
            }

            WeaponDataSO weaponSO = await gameManager.GetSOAsync<WeaponDataSO>(
                PublicVariable.Address.WeaponDataSO);

            if (weaponSO == null || weaponSO.Count == 0)
            {
                Debug.LogError("[UnitSetupUI] WeaponDataSO 로드 실패.");
                return;
            }

            ShowWeaponStep();
            _isWeaponStep = true;

            var list = new List<WeaponData>(weaponSO.WeaponDatas);
            _weaponSelectUI.Setup(list, OnWeaponConfirmed);
        }

        private void OnWeaponConfirmed(WeaponData weapon)
        {
            if (_isSaving)
                return;

            if (weapon == null)
            {
                Debug.LogWarning("[UnitSetupUI] 확정된 무기 데이터가 null입니다.");
                return;
            }

            if (_selectedCharacter == null)
            {
                Debug.LogError("[UnitSetupUI] 캐릭터가 선택되지 않았습니다.");
                return;
            }

            _selectedWeapon = weapon;
            SaveToGameManager();
        }

        private void SaveToGameManager()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[UnitSetupUI] GameManager.Instance가 없습니다.");
                return;
            }

            string unitTid = _selectedCharacter.UnitDataSOTid;
            if (string.IsNullOrEmpty(unitTid))
            {
                Debug.LogError("[UnitSetupUI] UnitDataSOTid가 비어 있습니다.");
                return;
            }

            _isSaving = true;
            gameManager.SetupPlayerCharacter(
                unitTid,
                _selectedWeapon.WeaponType,
                _selectedWeapon.CardDeckList,
                unitInfo =>
                {
                    _isSaving = false;
                    OnSetupCompleted?.Invoke(unitInfo);
                    CloseSelf();
                });
        }

        private void ShowCharacterStep()
        {
            _isWeaponStep = false;

            if (_characterSelectUI != null)
                _characterSelectUI.gameObject.SetActive(true);

            if (_weaponSelectUI != null)
                _weaponSelectUI.gameObject.SetActive(false);
        }

        private void ShowWeaponStep()
        {
            if (_characterSelectUI != null)
                _characterSelectUI.gameObject.SetActive(false);

            if (_weaponSelectUI != null)
                _weaponSelectUI.gameObject.SetActive(true);

            if (_confirmButton != null)
                _confirmButton.gameObject.SetActive(true);
        }

        private void CloseSelf()
        {
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager != null && uiManager.Current == this)
            {
                uiManager.Close();
                return;
            }

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(OnClickConfirm);
        }
    }
}
