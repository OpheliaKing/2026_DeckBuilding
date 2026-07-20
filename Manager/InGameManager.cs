using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SHIN
{
    public partial class InGameManager : MonoBehaviour
    {
        private readonly List<CharacterBase> _playerCharacters = new();
        private readonly List<CharacterBase> _enemyCharacters = new();

        private GroupPosition _playerGroupPosition;
        private GroupPosition _enemyGroupPosition;

        [SerializeField] private InGamePlayerUI _playerUI;
        private GameObject _playerUIObject;

        public IReadOnlyList<CharacterBase> PlayerCharacters => _playerCharacters;
        public IReadOnlyList<CharacterBase> EnemyCharacters => _enemyCharacters;

        public InGamePlayerUI PlayerUI => _playerUI;

        private IEnumerator _stageSettingCo;

        #region Stage Setting

        public void StageInit(StageData stageData)
        {
            CacheGroupPositions();
            _stageSettingCo = StageSettingCo(stageData);
            StartCoroutine(_stageSettingCo);
        }

        private IEnumerator StageSettingCo(StageData stageData)
        {
            var playerSettingTask = PlayerSettingAsync();
            var enemySettingTask = EnemySettingAsync(stageData);

            yield return new WaitUntil(() => playerSettingTask.IsCompleted && enemySettingTask.IsCompleted);

            var characterInitializeTask = InitializeCharactersAsync();
            yield return new WaitUntil(() => characterInitializeTask.IsCompleted);

            if (characterInitializeTask.IsFaulted)
            {
                Debug.LogException(characterInitializeTask.Exception);
                yield break;
            }

            InGameBattleStart();
        }

        /// <summary>
        /// 생성된 플레이어 캐릭터의 장비에 맞춰 Animator와 무기 모델을 초기화합니다.
        /// </summary>
        private async Task InitializeCharactersAsync()
        {
            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[InGameManager] 캐릭터 장비 초기화에 필요한 ResourceManager가 없습니다.");
                return;
            }

            for (int i = 0; i < _playerCharacters.Count; i++)
            {
                var character = _playerCharacters[i];
                if (character == null)
                    continue;

                await character.InitializeEquipmentAsync(resourceManager);
            }
        }

        private void CacheGroupPositions()
        {
            _playerGroupPosition = null;
            _enemyGroupPosition = null;

            var groupPositions = GetComponentsInChildren<GroupPosition>(true);
            for (int i = 0; i < groupPositions.Length; i++)
            {
                var group = groupPositions[i];
                switch (group.GroupPositionType)
                {
                    case GROUP_POSITION_TYPE.PLAYER:
                        _playerGroupPosition = group;
                        break;
                    case GROUP_POSITION_TYPE.ENEMY:
                        _enemyGroupPosition = group;
                        break;
                }
            }
        }

        private async Task PlayerSettingAsync()
        {
            _playerCharacters.Clear();

            var playerInfos = GameManager.Instance.PlayerCharacters;
            if (playerInfos == null || playerInfos.Count == 0)
            {
                Debug.LogWarning("[InGameManager] 배치할 플레이어가 없습니다.");
                return;
            }

            if (_playerGroupPosition == null)
            {
                Debug.LogError("[InGameManager] PLAYER GroupPosition을 찾을 수 없습니다.");
                return;
            }

            await SpawnPlayersAsync(playerInfos);
        }

        private async Task SpawnPlayersAsync(IReadOnlyList<UnitInfo> playerInfos)
        {
            Debug.Log($"[InGameManager] 플레이어 배치 시작: {playerInfos.Count}명");
            var slots = _playerGroupPosition.GetFormationSlots(playerInfos.Count);
            if (slots.Count == 0)
            {
                Debug.LogError("[InGameManager] 플레이어 포메이션 슬롯이 없습니다.");
                return;
            }

            if (playerInfos.Count > slots.Count)
            {
                Debug.LogWarning(
                    $"[InGameManager] 플레이어 수({playerInfos.Count})가 슬롯 수({slots.Count})보다 많아 일부만 배치합니다.");
            }

            var resourceManager = GameManager.Instance.ResourceManager;
            int spawnCount = Mathf.Min(playerInfos.Count, slots.Count);

            for (int i = 0; i < spawnCount; i++)
            {
                var unitInfo = playerInfos[i];
                if (unitInfo?.UnitData == null)
                    continue;

                var unitData = unitInfo.UnitData;
                if (string.IsNullOrEmpty(unitData.unitPrefabPath))
                {
                    Debug.LogError($"[InGameManager] unitPrefabPath가 비어 있습니다: {unitData.unitTid}");
                    continue;
                }

                var slot = slots[i];
                var playerObject = await resourceManager.InstantiateAsync(
                    unitData.unitPrefabPath,
                    slot.position,
                    slot.rotation,
                    slot);

                if (playerObject == null)
                {
                    Debug.LogError($"[InGameManager] 플레이어 생성 실패: {unitData.unitTid} / {unitData.unitPrefabPath}");
                    continue;
                }

                var character = playerObject.GetComponentInChildren<CharacterBase>();
                if (character != null)
                {
                    _playerCharacters.Add(character);
                    character.InitCharacter(unitInfo);
                }
            }

            Debug.Log($"[InGameManager] 플레이어 배치 완료: {_playerCharacters.Count}명");

            //await
        }

        private async Task EnemySettingAsync(StageData stageData)
        {
            _enemyCharacters.Clear();

            if (stageData?.enemyTids == null || stageData.enemyTids.Count == 0)
            {
                Debug.LogWarning("[InGameManager] 배치할 적이 없습니다.");
                return;
            }

            if (_enemyGroupPosition == null)
            {
                Debug.LogError("[InGameManager] ENEMY GroupPosition을 찾을 수 없습니다.");
                return;
            }

            var unitDataSO = await GameManager.Instance.GetSOAsync<UnitDataSO>(PublicVariable.Address.UnitDataSO);
            if (unitDataSO == null)
            {
                Debug.LogError("[InGameManager] UnitDataSO 로드 실패");
                return;
            }

            await SpawnEnemiesAsync(stageData.enemyTids, unitDataSO);
        }

        private async Task SpawnEnemiesAsync(List<string> enemyTids, UnitDataSO unitDataSO)
        {
            var slots = _enemyGroupPosition.GetFormationSlots(enemyTids.Count);
            if (slots.Count == 0)
            {
                Debug.LogError("[InGameManager] 적 포메이션 슬롯이 없습니다.");
                return;
            }

            if (enemyTids.Count > slots.Count)
            {
                Debug.LogWarning(
                    $"[InGameManager] 적 수({enemyTids.Count})가 슬롯 수({slots.Count})보다 많아 일부만 배치합니다.");
            }

            var resourceManager = GameManager.Instance.ResourceManager;
            int spawnCount = Mathf.Min(enemyTids.Count, slots.Count);

            for (int i = 0; i < spawnCount; i++)
            {
                var unitData = unitDataSO.GetUnitData(enemyTids[i]);
                if (unitData == null)
                    continue;

                if (string.IsNullOrEmpty(unitData.unitPrefabPath))
                {
                    Debug.LogError($"[InGameManager] unitPrefabPath가 비어 있습니다: {enemyTids[i]}");
                    continue;
                }

                var slot = slots[i];
                var enemyObject = await resourceManager.InstantiateAsync(
                    unitData.unitPrefabPath,
                    slot.position,
                    slot.rotation,
                    slot);

                if (enemyObject == null)
                {
                    Debug.LogError($"[InGameManager] 적 생성 실패: {enemyTids[i]} / {unitData.unitPrefabPath}");
                    continue;
                }

                var character = enemyObject.GetComponentInChildren<CharacterBase>();
                if (character != null)
                {
                    _enemyCharacters.Add(character);
                    character.InitCharacter(unitData);
                }
            }

            Debug.Log($"[InGameManager] 적 배치 완료: {_enemyCharacters.Count}마리");
        }

        #endregion

        /// <summary>
        /// 인게임 모든 리소스, 데이터 정리 후 시작하는 함수
        /// </summary>
        private async void InGameBattleStart()
        {
            Debug.Log("[InGameManager] 스테이지 세팅 완료. 전투 시작");

            await EnsurePlayerUIAsync();
            InitCombatDecks();
            InitTurnSystem();
            await BattleStartTimingAsync();
            StartNextTurn();
        }

        private async Task EnsurePlayerUIAsync()
        {
            if (_playerUI != null)
                return;

            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[InGameManager] Canvas를 찾을 수 없어 PlayerUI를 생성할 수 없습니다.");
                return;
            }

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[InGameManager] ResourceManager를 찾을 수 없습니다.");
                return;
            }

            var playerUIObject = await resourceManager.InstantiateAsync(
                PublicVariable.Address.PlayerUIPrefab,
                canvas.transform);

            if (playerUIObject == null)
            {
                Debug.LogError($"[InGameManager] PlayerUI 생성 실패: {PublicVariable.Address.PlayerUIPrefab}");
                return;
            }

            _playerUIObject = playerUIObject;
            _playerUI = playerUIObject.GetComponent<InGamePlayerUI>();
            if (_playerUI == null)
                _playerUI = playerUIObject.GetComponentInChildren<InGamePlayerUI>(true);

            if (_playerUI == null)
            {
                Debug.LogError("[InGameManager] PlayerUI 프리팹에 InGamePlayerUI가 없습니다.");
                resourceManager.ReleaseInstance(playerUIObject);
                _playerUIObject = null;
            }
        }

        private void ReleasePlayerUI()
        {
            if (_playerUIObject == null)
            {
                _playerUI = null;
                return;
            }

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager != null)
                resourceManager.ReleaseInstance(_playerUIObject);
            else
                Destroy(_playerUIObject);

            _playerUIObject = null;
            _playerUI = null;
        }

        private void OnDestroy()
        {
            ReleasePlayerUI();
        }

        private void InitCombatDecks()
        {
            InitCombatDecksForList(_playerCharacters);
            InitCombatDecksForList(_enemyCharacters);
        }

        private void InitCombatDecksForList(IReadOnlyList<CharacterBase> characters)
        {
            if (characters == null)
                return;

            for (int i = 0; i < characters.Count; i++)
            {
                var unitInfo = characters[i]?.UnitInfo;
                if (unitInfo == null)
                    continue;

                if (unitInfo.DeckCardList.Count == 0)
                {
                    Debug.LogWarning($"[InGameManager] 마스터 덱이 비어 있습니다: {characters[i].name}");
                    continue;
                }

                unitInfo.InitCombatDeck();
            }
        }
    }
}
