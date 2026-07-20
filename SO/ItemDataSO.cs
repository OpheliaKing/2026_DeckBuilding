using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "ItemDataSO", menuName = "SHIN/Item Data SO")]
    public class ItemDataSO : ScriptableObject
    {
        [SerializeField] private List<ItemData> _itemDatas = new();

        public IReadOnlyList<ItemData> ItemDatas => _itemDatas;
        public int Count => _itemDatas.Count;

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
    }
}
