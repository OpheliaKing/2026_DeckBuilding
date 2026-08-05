using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class GameManager
    {
        private List<UnitInfo> _playerCharacters = new List<UnitInfo>();
        public IReadOnlyList<UnitInfo> PlayerCharacters => _playerCharacters;

        private int _playerGold;
        public int PlayerGold => _playerGold;

        public void AddGold(int amount)
        {
            if (amount == 0)
                return;

            _playerGold = Mathf.Max(0, _playerGold + amount);
        }

        public bool TrySpendGold(int amount)
        {
            if (amount <= 0)
                return true;

            if (_playerGold < amount)
                return false;

            _playerGold -= amount;
            return true;
        }

        /// <summary>
        /// 새 런 시작 전 플레이어 목록을 비운다.
        /// </summary>
        public void ClearPlayerCharacters()
        {
            _playerCharacters.Clear();
        }

        /// <summary>
        /// 유닛 세팅 UI에서 캐릭터/무기 확정 시 호출.
        /// UnitInfo 생성 → 장비 타입 → 기본 덱(+캐릭터 보너스) 카드 추가 → 보너스 아이템 추가.
        /// </summary>
        public void SetupPlayerCharacter(
            string unitTid,
            CHARACTER_EQUIP_TYPE equipType,
            IReadOnlyList<string> cardTids,
            Action<UnitInfo> onComplete = null)
        {
            SetupPlayerCharacter(unitTid, equipType, cardTids, null, onComplete);
        }

        /// <summary>
        /// 유닛 세팅 UI에서 캐릭터/무기 확정 시 호출.
        /// startingItemTids: 캐릭터 선택 보너스 아이템.
        /// </summary>
        public void SetupPlayerCharacter(
            string unitTid,
            CHARACTER_EQUIP_TYPE equipType,
            IReadOnlyList<string> cardTids,
            IReadOnlyList<string> startingItemTids,
            Action<UnitInfo> onComplete = null)
        {
            AddPlayerCharacter(unitTid, unitInfo =>
            {
                if (unitInfo == null)
                {
                    onComplete?.Invoke(null);
                    return;
                }

                unitInfo.SetEquipType(equipType);

                var cardList = CollectNonEmptyTids(cardTids);
                void AfterCards(UnitInfo info)
                {
                    ApplyStartingItems(info, startingItemTids, onComplete);
                }

                if (cardList.Count == 0)
                {
                    AfterCards(unitInfo);
                    return;
                }

                AddCard(unitInfo, cardList, AfterCards);
            });
        }

        private static List<string> CollectNonEmptyTids(IReadOnlyList<string> tids)
        {
            var list = new List<string>();
            if (tids == null)
                return list;

            for (int i = 0; i < tids.Count; i++)
            {
                if (!string.IsNullOrEmpty(tids[i]))
                    list.Add(tids[i]);
            }

            return list;
        }

        private void ApplyStartingItems(
            UnitInfo unitInfo,
            IReadOnlyList<string> itemTids,
            Action<UnitInfo> onComplete)
        {
            var items = CollectNonEmptyTids(itemTids);
            if (unitInfo == null || items.Count == 0)
            {
                onComplete?.Invoke(unitInfo);
                return;
            }

            GetSOAsync<ItemDataSO>(PublicVariable.Address.ItemDataSO, itemDataSO =>
            {
                if (itemDataSO == null)
                {
                    Debug.LogError("[GameManager] ItemDataSO 로드 실패 — 시작 아이템을 지급하지 않습니다.");
                    onComplete?.Invoke(unitInfo);
                    return;
                }

                unitInfo.SetItemDataSO(itemDataSO);

                for (int i = 0; i < items.Count; i++)
                {
                    if (!unitInfo.AddItem(items[i]))
                        Debug.LogWarning($"[GameManager] 시작 아이템 추가 실패: {items[i]}");
                }

                onComplete?.Invoke(unitInfo);
            });
        }

        private void AddPlayerCharacter(string unitTid, Action<UnitInfo> onComplete = null)
        {
            if (string.IsNullOrEmpty(unitTid))
            {
                onComplete?.Invoke(null);
                return;
            }

            GetSOAsync<UnitDataSO>(PublicVariable.Address.UnitDataSO, unitDataSO =>
            {
                if (unitDataSO == null)
                {
                    Debug.LogError("[GameManager] UnitDataSO 로드 실패");
                    onComplete?.Invoke(null);
                    return;
                }

                var data = unitDataSO.GetUnitData(unitTid);
                if (data == null)
                {
                    Debug.LogError($"[GameManager] UnitData 로드 실패: {unitTid}");
                    onComplete?.Invoke(null);
                    return;
                }

                var unitInfo = new UnitInfo(data);
                unitInfo.SetUnitType(UNIT_TYPE.PLAYER);
                AddPlayerCharacter(unitInfo);
                onComplete?.Invoke(unitInfo);
            });
        }

        private void AddPlayerCharacter(UnitInfo unitInfo)
        {
            _playerCharacters.Add(unitInfo);
        }
    }
}
