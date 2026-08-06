using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [Serializable]
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

        /// <summary>InGameCombatEventSO에서 조회할 이벤트 tid 목록</summary>
        [SerializeField]
        private List<string> _inGameCombatEvents = new();
        public IReadOnlyList<string> InGameCombatEvents => _inGameCombatEvents;

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
        /// <summary>실제 피격 대상마다 1회. 기존 ON_ATTACK과 동일한 직렬화 값(6).</summary>
        ON_TARGET_HIT,
        ON_DEATH,
        ON_KILL,
        ON_USE_CARD,
        HEALTH_LOW,
        HEALTH_HIGH,
        /// <summary>공격 카드 해석 시작 시 공격당 1회.</summary>
        ON_ATTACK_START,
        /// <summary>애니메이션 히트 판정마다 1회. 범위 공격도 판정당 1회.</summary>
        ON_HIT,
        /// <summary>공격 종료 후 공격당 1회.</summary>
        ON_ATTACK_END,
    }

    public enum ITEM_EFFECT_CONDITION
    {
        NONE,//발동조건 없음(EFFECT_TIMING 조건에 따라 바로 발동)
        COUNT,//횟수 EX) ON_HIT과 같이 사용하면 히트 3번마다, ON_USE_CARD와 사용 시 카드 3장마다 발동
        PERCENTAGE,//퍼센트 EX) HEALTH_LOW와 사용하면 체력 20% 이하, ON_HIT와 사용하면 20% 확률로 발동
        ABSOLUTE,//절대값 EX) HEALTH_LOW와 같이 사용하면 체력이 20 이하일 때 발동
    }


}