using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SHIN
{
    /// <summary>
    /// 카드·아이템·전투이벤트가 공용으로 참조하는 버프 적용 데이터.
    /// </summary>
    [Serializable]
    public class BuffData
    {
        [SerializeField]
        private string _tid;
        public string Tid => _tid;

        [SerializeField]
        private string _name;
        public string Name => _name;

        [SerializeField]
        private string _description;
        public string Description => _description;

        [FormerlySerializedAs("_buffEffectType")]
        [SerializeField]
        private BUFF_EFFECT_TYPE _effectType;
        public BUFF_EFFECT_TYPE EffectType => _effectType;

        [FormerlySerializedAs("_buffEffectValue")]
        [SerializeField]
        private float _value;
        public float Value => _value;

        [FormerlySerializedAs("_buffEffectDuration")]
        [SerializeField]
        private int _duration;
        public int Duration => _duration;

        public bool IsValid =>
            _effectType != BUFF_EFFECT_TYPE.NONE && _duration > 0;
    }

    public enum BUFF_EFFECT_TYPE
    {
        NONE,
        ATTACK_UP,
        DEFENSE_UP,
        HP_UP,
        SPEED_UP,
        MAX_COST_UP,
        CUSTOM,

        /// <summary>공격 피해 고정 증가</summary>
        STRENGTH,
        /// <summary>방어도(턴 지속 실드)</summary>
        BLOCK,
        /// <summary>받는 피해 증가</summary>
        VULNERABLE,
        /// <summary>주는 피해 감소</summary>
        WEAK,
        /// <summary>입힌 피해 비율 흡혈</summary>
        LIFESTEAL,
        /// <summary>피격 시 반사 피해</summary>
        THORNS,
        /// <summary>턴 시작마다 체력 회복</summary>
        REGEN,
        /// <summary>턴 시작 DoT</summary>
        POISON,
    }
}
