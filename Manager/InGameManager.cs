using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public class InGameManager : MonoBehaviour
    {
        private const string UnitDataSoAddress = "Assets/Addressables/SO/UnitDataSO.asset";

        private readonly List<CharacterBase> _enemyCharacters = new();

        private GroupPosition _playerGroupPosition;
        private GroupPosition _enemyGroupPosition;

        public IReadOnlyList<CharacterBase> EnemyCharacters => _enemyCharacters;

        public void StageInit(StageData stageData)
        {
            CacheGroupPositions();
            EnemySetting(stageData);
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

        private void EnemySetting(StageData stageData)
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

            GameManager.Instance.GetSOAsync<UnitDataSO>(UnitDataSoAddress, unitDataSO =>
            {
                if (unitDataSO == null)
                {
                    Debug.LogError("[InGameManager] UnitDataSO 로드 실패");
                    return;
                }

                SpawnEnemies(stageData.enemyTids, unitDataSO);
            });
        }

        private async void SpawnEnemies(List<string> enemyTids, UnitDataSO unitDataSO)
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


        /// <summary>
        /// 인게임 모든 리소스, 데이터 정리 후 시작하는 함수
        /// </summary>
        private void InGameBattleStart()
        {
        }
    }
}
