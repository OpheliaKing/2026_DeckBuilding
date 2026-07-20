using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public class ItemEffectData
    {
        [SerializeField]
        private string _tid;
        public string Tid => _tid;
        [SerializeField]
        private ITEM_EFFECT_TIMING _effectTiming;
        public ITEM_EFFECT_TIMING EffectTiming => _effectTiming;
        [SerializeField]
        private ITEM_EFFECT_CONDITION _effectCondition;
        public ITEM_EFFECT_CONDITION EffectCondition => _effectCondition;
        [SerializeField]
        private int _effectConditionValue;
        public int EffectConditionValue => _effectConditionValue;
        [SerializeField]
        private string _effectCustomString;
        public string EffectCustomString => _effectCustomString;
        /// <summary>
        /// 인스펙터 창에서 해당 효과를 설명하기 위해 사용하는 변수
        /// </summary>
        [SerializeField]
        private string _effectDataDescription;
    }

    public enum ITEM_EFFECT_TIMING
    {
        NONE,
        BATTLE_START,
        BATTLE_END,
        TURN_START,
        TURN_END,
        ON_DAMAGE,
        ON_ATTACK,
        ON_DEATH,
        ON_KILL,
        ON_USE_CARD,
        HEALTH_LOW,
        HEALTH_HIGH,
    }

    public enum ITEM_EFFECT_CONDITION
    {
        NONE,//발동조건 없음(EFFECT_TIMING 조건에 따라 바로 발동)
        COUNT,//횟수 EX) On_HIT과 같이 사용하면 3번 맞음, ON_USE_CARD와 같이 사용시 카드 3장마다 발동
        PERCENTAGE,//퍼센트 EX) HEALTH_LOW와 같이 사용하면 체력이 20% 이하일 때 발동
        ABSOLUTE,//절대값 EX) HEALTH_LOW와 같이 사용하면 체력이 20 이하일 때 발동
    }


}