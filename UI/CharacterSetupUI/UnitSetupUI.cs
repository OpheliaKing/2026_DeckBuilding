using System;
using System.Collections;
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

        [Header("Stage Transition")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("선택 화면 → 검정으로 FadeOut 시간(초)")]
        private float _stageCoverFadeOutDuration = 0.6f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("완전히 가린 뒤 StageUI 준비 전 대기(초). 보이스 재생 여유 등")]
        private float _postCoverHoldDuration = 1.5f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("StageUI 공개 시 FadeIn 시간(초). FadeUI 기본값 대신 이 값을 씀")]
        private float _stageRevealFadeInDuration = 0.5f;

        private CharacterSelectData _selectedCharacter;
        private WeaponData _selectedWeapon;
        private bool _isSaving;
        private Coroutine _stageTransitionRoutine;

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

            // 이미 선택된 캐릭터면 모델 갱신하지 않음
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

            PlayCharacterSelectEndVoice(unitTid);

            // FadeOut 완료 → 홀드 → Stage 준비 → SignalContentReady로 페이드인
            UIManager uiManager = gameManager.UIManager;
            if (uiManager != null)
            {
                uiManager.SetFadeOutDuration(_stageCoverFadeOutDuration);
                uiManager.BeginFadeOutCover(() =>
                {
                    uiManager.SetFadeInDuration(_stageRevealFadeInDuration);
                    if (_stageTransitionRoutine != null)
                        StopCoroutine(_stageTransitionRoutine);
                    _stageTransitionRoutine = StartCoroutine(
                        CommitPlayerSetupAfterHold(gameManager, unitTid));
                });
            }
            else
            {
                CommitPlayerSetup(gameManager, unitTid);
            }
        }

        private IEnumerator CommitPlayerSetupAfterHold(GameManager gameManager, string unitTid)
        {
            if (_postCoverHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(_postCoverHoldDuration);

            _stageTransitionRoutine = null;
            CommitPlayerSetup(gameManager, unitTid);
        }

        private static void PlayCharacterSelectEndVoice(string unitTid)
        {
            SoundManager soundManager = GameManager.Instance?.SoundManager;
            if (soundManager == null)
            {
                Debug.LogWarning("[UnitSetupUI] SoundManager가 없어 선택 완료 보이스를 재생할 수 없습니다.");
                return;
            }

            string path =
                $"{PublicVariable.Address.VoiceRoot}{unitTid}/character_select_end_001.wav";
            soundManager.PlayVoice(path);
        }

        private void CommitPlayerSetup(GameManager gameManager, string unitTid)
        {
            List<string> cardDeck = BuildStartingCardDeck(_selectedWeapon, _selectedCharacter);

            gameManager.SetupPlayerCharacter(
                unitTid,
                _selectedWeapon.WeaponType,
                cardDeck,
                _selectedCharacter.HaveItemList,
                unitInfo =>
                {
                    _isSaving = false;
                    OnSetupCompleted?.Invoke(unitInfo);
                    CloseSelf();
                });
        }

        private static List<string> BuildStartingCardDeck(
            WeaponData weapon,
            CharacterSelectData character)
        {
            var cards = new List<string>();

            if (weapon?.CardDeckList != null)
            {
                for (int i = 0; i < weapon.CardDeckList.Count; i++)
                {
                    string tid = weapon.CardDeckList[i];
                    if (!string.IsNullOrEmpty(tid))
                        cards.Add(tid);
                }
            }

            IReadOnlyList<string> bonusCards = character?.HaveCardList;
            if (bonusCards != null)
            {
                for (int i = 0; i < bonusCards.Count; i++)
                {
                    string tid = bonusCards[i];
                    if (!string.IsNullOrEmpty(tid))
                        cards.Add(tid);
                }
            }

            return cards;
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
