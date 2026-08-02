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

        private CharacterSelectData _selectedCharacter;
        private WeaponData _selectedWeapon;
        private bool _isSaving;

        /// <summary>슬롯 미리보기용. CharacterSelectObject 모델 갱신 등에 사용.</summary>
        public event Action<CharacterSelectData> OnCharacterPreviewChanged;

        /// <summary>무기 미리보기용. CharacterSelectObject 애니메이션 재생 등에 사용.</summary>
        public event Action<WeaponData> OnWeaponPreviewChanged;

        /// <summary>캐릭터 선택 단계 표시 시.</summary>
        public event Action OnCharacterStepShown;

        /// <summary>무기 선택 단계 표시 시.</summary>
        public event Action OnWeaponStepShown;

        /// <summary>유닛 세팅 완료(저장) 후.</summary>
        public event Action<UnitInfo> OnSetupCompleted;

        private void Awake()
        {
            ResolveChildUIs();
        }

        /// <summary>
        /// 캐릭터 선택 단계부터 시작한다.
        /// onContentReady: 첫 화면 리소스 로드가 끝났을 때(페이드인 등) 호출.
        /// </summary>
        public void BeginSetup(Action onContentReady = null)
        {
            ResolveChildUIs();
            _selectedCharacter = null;
            _selectedWeapon = null;
            _isSaving = false;
            BeginCharacterSelectAsync(onContentReady);
        }

        private void ResolveChildUIs()
        {
            if (_characterSelectUI == null)
                _characterSelectUI = GetComponentInChildren<CharacterSelectUI>(true);

            if (_weaponSelectUI == null)
                _weaponSelectUI = GetComponentInChildren<WeaponSelectUI>(true);
        }

        private async void BeginCharacterSelectAsync(Action onContentReady)
        {
            if (_characterSelectUI == null)
            {
                Debug.LogError("[UnitSetupUI] CharacterSelectUI가 없습니다.");
                onContentReady?.Invoke();
                return;
            }

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[UnitSetupUI] GameManager.Instance가 없습니다.");
                onContentReady?.Invoke();
                return;
            }

            CharacterSelectDataSO characterSO = await gameManager.GetSOAsync<CharacterSelectDataSO>(
                PublicVariable.Address.CharacterSelectDataSO);

            if (characterSO == null || characterSO.Count == 0)
            {
                Debug.LogError("[UnitSetupUI] CharacterSelectDataSO 로드 실패.");
                onContentReady?.Invoke();
                return;
            }

            ShowCharacterStep();

            var list = new List<CharacterSelectData>(characterSO.CharacterSelectDatas);
            _characterSelectUI.Setup(list, OnCharacterConfirmed, OnCharacterPreview);
            onContentReady?.Invoke();
        }

        private void OnCharacterPreview(CharacterSelectData data)
        {
            if (data == null)
                return;

            // 이미 선택된 캐릭터면 모델 갱신/디졸브 재재생하지 않음
            if (IsSameCharacter(_selectedCharacter, data))
                return;

            _selectedCharacter = data;
            OnCharacterPreviewChanged?.Invoke(data);
        }

        private static bool IsSameCharacter(CharacterSelectData a, CharacterSelectData b)
        {
            if (a == null || b == null)
                return false;

            if (!string.IsNullOrEmpty(a.Tid) && !string.IsNullOrEmpty(b.Tid))
                return a.Tid == b.Tid;

            if (!string.IsNullOrEmpty(a.PrefabPath) && !string.IsNullOrEmpty(b.PrefabPath))
                return a.PrefabPath == b.PrefabPath;

            return ReferenceEquals(a, b);
        }

        private void OnCharacterConfirmed(CharacterSelectData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[UnitSetupUI] 확정된 캐릭터 데이터가 null입니다.");
                return;
            }

            if (!IsSameCharacter(_selectedCharacter, data))
            {
                _selectedCharacter = data;
                OnCharacterPreviewChanged?.Invoke(data);
            }
            else
            {
                _selectedCharacter = data;
            }

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

            var list = new List<WeaponData>(weaponSO.WeaponDatas);
            _weaponSelectUI.Setup(list, OnWeaponConfirmed, OnWeaponBack, OnWeaponPreview);
        }

        private void OnWeaponPreview(WeaponData weapon)
        {
            if (weapon == null)
                return;

            OnWeaponPreviewChanged?.Invoke(weapon);
        }

        private void OnWeaponBack()
        {
            if (_isSaving)
                return;

            _selectedWeapon = null;
            ShowCharacterStep();

            if (_selectedCharacter != null)
                _characterSelectUI?.SetSelected(_selectedCharacter);
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

            // StageNodeUI 전환 전 즉시 가림 → 맵 준비 후 SignalContentReady로 페이드인
            UIManager uiManager = gameManager.UIManager;
            if (uiManager != null)
                uiManager.BeginFadeCover(() => CommitPlayerSetup(gameManager, unitTid));
            else
                CommitPlayerSetup(gameManager, unitTid);
        }

        private void CommitPlayerSetup(GameManager gameManager, string unitTid)
        {
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
            if (_characterSelectUI != null)
                _characterSelectUI.gameObject.SetActive(true);

            if (_weaponSelectUI != null)
                _weaponSelectUI.gameObject.SetActive(false);

            OnCharacterStepShown?.Invoke();
        }

        private void ShowWeaponStep()
        {
            if (_characterSelectUI != null)
                _characterSelectUI.gameObject.SetActive(false);

            if (_weaponSelectUI != null)
                _weaponSelectUI.gameObject.SetActive(true);

            OnWeaponStepShown?.Invoke();
        }

        private void CloseSelf()
        {
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager != null && uiManager.Current == this)
            {
                // 페이드 커버 중에는 StartUI 등 이전 화면을 다시 켜지 않음
                bool revealPrevious = !uiManager.IsWaitingFadeReady;
                uiManager.Close(revealPrevious);
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
