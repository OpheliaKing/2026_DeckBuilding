using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "UnitDataSO", menuName = "SHIN/Unit Data SO")]
    public class UnitDataSO : ScriptableObject
    {
        [SerializeField] private List<UnitData> _unitDatas = new();

        public IReadOnlyList<UnitData> UnitDatas => _unitDatas;
        public int Count => _unitDatas.Count;

        public UnitData GetUnitData(int index)
        {
            if (index < 0 || index >= _unitDatas.Count)
            {
                Debug.LogError($"[UnitDataSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _unitDatas[index];
        }

        public UnitData GetUnitData(string unitTid)
        {
            if (string.IsNullOrEmpty(unitTid))
            {
                Debug.LogError("[UnitDataSO] unitTid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _unitDatas.Count; i++)
            {
                if (_unitDatas[i].unitTid == unitTid)
                    return _unitDatas[i];
            }

            Debug.LogError($"[UnitDataSO] unitTid를 찾을 수 없습니다: {unitTid}");
            return null;
        }

        public bool TryGetUnitData(string unitTid, out UnitData unitData)
        {
            unitData = null;

            if (string.IsNullOrEmpty(unitTid))
                return false;

            for (int i = 0; i < _unitDatas.Count; i++)
            {
                if (_unitDatas[i].unitTid == unitTid)
                {
                    unitData = _unitDatas[i];
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public class UnitData
    {
        public string unitTid;
        public int unitBaseAttack;
        public int unitBaseDefense;
        public int unitBaseHp;
        public int unitBaseSpeed;
        public int unitBaseMaxCardCost;
        public string unitName;
        public string unitIcon;
        public string unitPrefabPath;
        public List<string> unitCardList;
    }
}
