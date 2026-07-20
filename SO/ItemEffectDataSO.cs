using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "ItemEffectDataSO", menuName = "SHIN/Item Effect Data SO")]
    public class ItemEffectDataSO : ScriptableObject
    {
        [SerializeField] private List<ItemEffectData> _itemEffectDatas = new();

        public IReadOnlyList<ItemEffectData> ItemEffectDatas => _itemEffectDatas;
        public int Count => _itemEffectDatas.Count;

        public ItemEffectData GetItemEffectData(int index)
        {
            if (index < 0 || index >= _itemEffectDatas.Count)
            {
                Debug.LogError($"[ItemEffectDataSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _itemEffectDatas[index];
        }

        public ItemEffectData GetItemEffectData(string effectTid)
        {
            if (string.IsNullOrEmpty(effectTid))
            {
                Debug.LogError("[ItemEffectDataSO] effectTid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _itemEffectDatas.Count; i++)
            {
                if (_itemEffectDatas[i].Tid == effectTid)
                    return _itemEffectDatas[i];
            }

            Debug.LogError($"[ItemEffectDataSO] effectTid를 찾을 수 없습니다: {effectTid}");
            return null;
        }

        public bool TryGetItemEffectData(string effectTid, out ItemEffectData itemEffectData)
        {
            itemEffectData = null;

            if (string.IsNullOrEmpty(effectTid))
                return false;

            for (int i = 0; i < _itemEffectDatas.Count; i++)
            {
                if (_itemEffectDatas[i].Tid == effectTid)
                {
                    itemEffectData = _itemEffectDatas[i];
                    return true;
                }
            }

            return false;
        }
    }
}
