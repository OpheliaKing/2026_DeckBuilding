using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "StageDataSO", menuName = "SHIN/Stage Data SO")]
    public class StageDataSO : ScriptableObject
    {
        [SerializeField] private List<StageData> _stageDatas = new();

        public IReadOnlyList<StageData> StageDatas => _stageDatas;
        public int Count => _stageDatas.Count;

        public StageData GetStageData(int index)
        {
            if (index < 0 || index >= _stageDatas.Count)
            {
                Debug.LogError($"[StageDataSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _stageDatas[index];
        }

        public StageData GetStageData(string stageTid)
        {
            if (string.IsNullOrEmpty(stageTid))
            {
                Debug.LogError("[StageDataSO] stageTid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _stageDatas.Count; i++)
            {
                if (_stageDatas[i].stageTid == stageTid)
                    return _stageDatas[i];
            }

            Debug.LogError($"[StageDataSO] stageTid를 찾을 수 없습니다: {stageTid}");
            return null;
        }

        public bool TryGetStageData(string stageTid, out StageData stageData)
        {
            stageData = null;

            if (string.IsNullOrEmpty(stageTid))
                return false;

            for (int i = 0; i < _stageDatas.Count; i++)
            {
                if (_stageDatas[i].stageTid == stageTid)
                {
                    stageData = _stageDatas[i];
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public class StageData
    {
        public string stageTid;
        public List<string> enemyTids;
        public List<string> rewardTids;
        public string stagePrefabPath;
    }


}
