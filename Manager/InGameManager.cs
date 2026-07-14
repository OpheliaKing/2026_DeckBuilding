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

        public IReadOnlyList<CharacterBase> PlayerCharacters => _playerCharacters;
        public IReadOnlyList<CharacterBase> EnemyCharacters => _enemyCharacters;

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

            InGameBattleStart();
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

            var unitDataSO = await GameManager.Instance.GetSOAsync<UnitDataSO>(GameManager.Instance.UnitDataSoAddress);
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
        private void InGameBattleStart()
        {
            Debug.Log("[InGameManager] 스테이지 세팅 완료. 전투 시작");

            InitTurnSystem();
            BattleStartTiming();
            AdvanceToNextTurn();
        }
    }
}
