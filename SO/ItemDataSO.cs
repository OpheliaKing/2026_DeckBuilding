using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "ItemDataSO", menuName = "SHIN/Item Data SO")]
    public class ItemDataSO : ScriptableObject
    {
        [SerializeField] private List<ItemData> _itemDatas = new();

        /// <summary>
        /// 일반 스테이지 리워드용 등급별 아이템 캐시.
        /// ITEM_REWARD_TYPE.NONE만 포함. BOSS/EVENT는 별도 구현.
        /// 런타임 전용(직렬화하지 않음).
        /// </summary>
        [NonSerialized]
        private Dictionary<ITEM_GRADE, List<ItemData>> _rewardItemsByGrade;

        [NonSerialized]
        private bool _isRewardIndexBuilt;

        public IReadOnlyList<ItemData> ItemDatas => _itemDatas;
        public int Count => _itemDatas.Count;
        public bool IsRewardIndexBuilt => _isRewardIndexBuilt;

        /// <summary>
        /// 리워드용 등급별 딕셔너리를 빌드한다.
        /// ITEM_REWARD_TYPE.NONE만 넣고, BOSS/EVENT는 제외한다.
        /// </summary>
        public void BuildRewardIndex()
        {
            if (_rewardItemsByGrade == null)
                _rewardItemsByGrade = new Dictionary<ITEM_GRADE, List<ItemData>>();
            else
                _rewardItemsByGrade.Clear();

            if (_itemDatas == null)
            {
                _isRewardIndexBuilt = true;
                return;
            }

            int rewardCount = 0;
            for (int i = 0; i < _itemDatas.Count; i++)
            {
                ItemData item = _itemDatas[i];
                if (item == null)
                    continue;

                if (item.ItemRewardType != ITEM_REWARD_TYPE.NONE)
                    continue;

                ITEM_GRADE grade = item.ItemGrade;
                if (!_rewardItemsByGrade.TryGetValue(grade, out List<ItemData> list))
                {
                    list = new List<ItemData>();
                    _rewardItemsByGrade[grade] = list;
                }

                list.Add(item);
                rewardCount++;
            }

            _isRewardIndexBuilt = true;
            Debug.Log(
                $"[ItemDataSO] 리워드 인덱스 빌드 완료: 리워드 후보 {rewardCount}개, 등급 {_rewardItemsByGrade.Count}종");
        }

        /// <summary>
        /// 해당 등급의 일반 리워드 아이템 목록.
        /// </summary>
        public IReadOnlyList<ItemData> GetRewardItemsByGrade(ITEM_GRADE grade)
        {
            EnsureRewardIndex();

            if (_rewardItemsByGrade.TryGetValue(grade, out List<ItemData> list))
                return list;

            return Array.Empty<ItemData>();
        }

        /// <summary>
        /// 해당 등급에서 리워드 아이템을 랜덤으로 하나 뽑는다.
        /// </summary>
        public bool TryGetRandomRewardItem(ITEM_GRADE grade, out ItemData itemData)
        {
            itemData = null;

            IReadOnlyList<ItemData> list = GetRewardItemsByGrade(grade);
            if (list == null || list.Count == 0)
                return false;

            itemData = list[UnityEngine.Random.Range(0, list.Count)];
            return itemData != null;
        }

        public ItemData GetItemData(int index)
        {
            if (index < 0 || index >= _itemDatas.Count)
            {
                Debug.LogError($"[ItemDataSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _itemDatas[index];
        }

        public ItemData GetItemData(string itemTid)
        {
            if (string.IsNullOrEmpty(itemTid))
            {
                Debug.LogError("[ItemDataSO] itemTid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _itemDatas.Count; i++)
            {
                if (_itemDatas[i].Tid == itemTid)
                    return _itemDatas[i];
            }

            Debug.LogError($"[ItemDataSO] itemTid를 찾을 수 없습니다: {itemTid}");
            return null;
        }

        public bool TryGetItemData(string itemTid, out ItemData itemData)
        {
            itemData = null;

            if (string.IsNullOrEmpty(itemTid))
                return false;

            for (int i = 0; i < _itemDatas.Count; i++)
            {
                if (_itemDatas[i].Tid == itemTid)
                {
                    itemData = _itemDatas[i];
                    return true;
                }
            }

            return false;
        }

        private void EnsureRewardIndex()
        {
            if (_isRewardIndexBuilt && _rewardItemsByGrade != null)
                return;

            BuildRewardIndex();
        }
    }
}
