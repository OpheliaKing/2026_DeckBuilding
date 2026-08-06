using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "BuffDataSO", menuName = "SHIN/Buff Data SO")]
    public class BuffDataSO : ScriptableObject
    {
        [SerializeField] private List<BuffData> _buffDatas = new();

        public IReadOnlyList<BuffData> BuffDatas => _buffDatas;
        public int Count => _buffDatas.Count;

        public BuffData GetBuffData(int index)
        {
            if (index < 0 || index >= _buffDatas.Count)
            {
                Debug.LogError($"[BuffDataSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _buffDatas[index];
        }

        public BuffData GetBuffData(string buffTid)
        {
            if (string.IsNullOrEmpty(buffTid))
            {
                Debug.LogError("[BuffDataSO] buffTid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _buffDatas.Count; i++)
            {
                if (_buffDatas[i].Tid == buffTid)
                    return _buffDatas[i];
            }

            Debug.LogError($"[BuffDataSO] buffTid를 찾을 수 없습니다: {buffTid}");
            return null;
        }

        public bool TryGetBuffData(string buffTid, out BuffData buffData)
        {
            buffData = null;

            if (string.IsNullOrEmpty(buffTid))
                return false;

            for (int i = 0; i < _buffDatas.Count; i++)
            {
                if (_buffDatas[i].Tid == buffTid)
                {
                    buffData = _buffDatas[i];
                    return true;
                }
            }

            return false;
        }
    }
}
