using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [Serializable]
    public class StageEventData
    {
        public string Tid;
        public string EventName;
        public string EventDescription;
        public string EventImagePath;
        public List<StageEventChoice> Choices = new();
    }

    /// <summary>
    /// 이벤트 UI에서 버튼 1개에 대응하는 선택지 데이터.
    /// </summary>
    [Serializable]
    public class StageEventChoice
    {
        public string ButtonText;
        /// <summary>
        /// 선택 시 Effects를 순회하며 각 항목을 독립 확률로 판정한다.
        /// </summary>
        public List<StageEventEffect> Effects = new();
    }

    [Serializable]
    public class StageEventEffect
    {
        public STAGE_EVENT_RESULT_TYPE ResultType;

        /// <summary>GET_GOLD, LOSE_GOLD, GET_HP, LOSE_HP 등 수치형 결과.</summary>
        public int IntValue;

        /// <summary>GET_ITEM, GET_CARD 등 Tid 기반 결과.</summary>
        public string TidValue;

        /// <summary>0~1. 선택 시 이 효과가 발동할 독립 확률.</summary>
        [Range(0f, 1f)]
        public float Probability;
    }

    public enum STAGE_EVENT_RESULT_TYPE
    {
        NONE,
        GET_GOLD,
        GET_ITEM,
        GET_CARD,
        GET_HP,
        LOSE_GOLD,
        LOSE_HP,
    }
}
