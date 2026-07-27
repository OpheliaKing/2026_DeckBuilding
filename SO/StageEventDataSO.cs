using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "StageEventDataSO", menuName = "SHIN/Stage Event Data SO")]
    public class StageEventDataSO : ScriptableObject
    {
        [SerializeField] private List<StageEventData> _eventDatas = new();

        public IReadOnlyList<StageEventData> EventDatas => _eventDatas;
        public int Count => _eventDatas.Count;

        public StageEventData GetEventData(int index)
        {
            if (index < 0 || index >= _eventDatas.Count)
            {
                Debug.LogError($"[StageEventDataSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _eventDatas[index];
        }

        

        public StageEventData GetEventData(string eventTid)
        {
            if (string.IsNullOrEmpty(eventTid))
            {
                Debug.LogError("[StageEventDataSO] eventTid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _eventDatas.Count; i++)
            {
                if (_eventDatas[i].Tid == eventTid)
                    return _eventDatas[i];
            }

            Debug.LogError($"[StageEventDataSO] eventTid를 찾을 수 없습니다: {eventTid}");
            return null;
        }

        public bool TryGetEventData(string eventTid, out StageEventData eventData)
        {
            eventData = null;

            if (string.IsNullOrEmpty(eventTid))
                return false;

            for (int i = 0; i < _eventDatas.Count; i++)
            {
                if (_eventDatas[i].Tid == eventTid)
                {
                    eventData = _eventDatas[i];
                    return true;
                }
            }

            return false;
        }
    }
}
