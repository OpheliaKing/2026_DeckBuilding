using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SHIN
{
    [Serializable]
    public class CardData : ISerializationCallbackReceiver
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

        [SerializeField]
        private CHARACTER_EQUIP_TYPE _cardWeaponType;
        public CHARACTER_EQUIP_TYPE CardWeaponType => _cardWeaponType;

        [SerializeField]
        private int _cost;
        public int Cost => _cost;

        /// <summary>
        /// CardIllustAtlas 스프라이트 이름. 비우면 플레이스홀더.
        /// 예: card_illust_pink_sword_slash / 권장 해상도 1024×1024.
        /// </summary>
        [SerializeField]
        private string _illustrationPath;
        public string IllustrationPath => _illustrationPath;

        [SerializeField]
        private CARD_TYPE _cardType;
        public CARD_TYPE CardType => _cardType;

        [SerializeField]
        private ITEM_GRADE _cardGrade;
        public ITEM_GRADE CardGrade => _cardGrade;

        [SerializeField]
        private string _animationName;
        public string AnimationName => _animationName;

        #region ATTACK

        [SerializeField]
        private float _attackMultiplier;
        public float AttackMultiplier => _attackMultiplier;

        [SerializeField]
        private bool _isRangeAttack;
        public bool IsRangeAttack => _isRangeAttack;

        /// <summary>
        /// 피격 대상 HitEffectPoint에 스폰할 Addressables 이펙트 경로.
        /// </summary>
        [SerializeField]
        private string _hitEffectPath;
        public string HitEffectPath => _hitEffectPath;

        /// <summary>
        /// 히트 판정 시 재생할 SE 경로. 비우면 DefaultHitSe 사용.
        /// </summary>
        [SerializeField]
        private string _hitSoundPath;
        public string HitSoundPath => _hitSoundPath;

        /// <summary>
        /// 공격 애니 파티클 오버라이드.
        /// 비어 있지 않으면 CombatAnimStateBehaviour ParticleCue.ParticleAddress 대신 사용한다.
        /// </summary>
        [SerializeField]
        private string _attackParticlePath;
        public string AttackParticlePath => _attackParticlePath;

        /// <summary>
        /// 공격 과정에서 실행할 전투 이벤트 목록.
        /// 이벤트별 발동 시점, 대상, 확률, 수치 배율과 반복 여부를 설정합니다.
        /// </summary>
        [SerializeField]
        private List<CardAttackEventData> _attackEvents = new();
        public IReadOnlyList<CardAttackEventData> AttackEvents => _attackEvents;

        /// <summary>
        /// 기존 List&lt;string&gt; _attackEvent 데이터의 자동 마이그레이션용 필드.
        /// </summary>
        [FormerlySerializedAs("_attackEvent")]
        [SerializeField, HideInInspector]
        private List<string> _legacyAttackEvents;

        #endregion

        #region BUFF

        [SerializeField]
        private CARD_BUFF_TARGET_TYPE _buffTargetType;
        public CARD_BUFF_TARGET_TYPE BuffTargetType => _buffTargetType;

        [SerializeField]
        private CardBuffData _buffData;
        public CardBuffData BuffData => _buffData;


        #endregion

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _attackEvents ??= new List<CardAttackEventData>();
            if (_legacyAttackEvents == null || _legacyAttackEvents.Count == 0)
                return;

            for (int i = 0; i < _legacyAttackEvents.Count; i++)
            {
                string eventTid = _legacyAttackEvents[i];
                if (!string.IsNullOrEmpty(eventTid))
                    _attackEvents.Add(new CardAttackEventData(eventTid));
            }

            _legacyAttackEvents.Clear();
        }
    }

    [Serializable]
    public class CardAttackEventData
    {
        [Tooltip("실행할 InGameCombatEventSO 이벤트 TID")]
        [SerializeField]
        private string _eventTid;
        public string EventTid => _eventTid;

        [Tooltip("NONE이면 InGameCombatEvent의 TargetUnit을 사용합니다.")]
        [SerializeField]
        private IN_GAME_COMBAT_EVENT_TARGET_UNIT _targetOverride;
        public IN_GAME_COMBAT_EVENT_TARGET_UNIT TargetOverride => _targetOverride;

        [Tooltip("이 이벤트가 실행될 공격 시점")]
        [SerializeField]
        private CARD_ATTACK_EVENT_TIMING _timing = CARD_ATTACK_EVENT_TIMING.FINAL_HIT;
        public CARD_ATTACK_EVENT_TIMING Timing => _timing;

        [Tooltip("발동 확률. 0은 발동하지 않고 1은 항상 발동합니다.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float _triggerChance = 1f;
        public float TriggerChance => _triggerChance;

        [Tooltip("InGameCombatEvent의 Value에 곱할 카드 전용 배율")]
        [Min(0f)]
        [SerializeField]
        private float _valueMultiplier = 1f;
        public float ValueMultiplier => _valueMultiplier;

        [Tooltip("같은 공격 중 동일 이벤트가 여러 판정에서 반복 실행될 수 있는지 여부")]
        [SerializeField]
        private bool _allowRepeatedExecution;
        public bool AllowRepeatedExecution => _allowRepeatedExecution;

        public CardAttackEventData(string eventTid)
        {
            _eventTid = eventTid;
            _timing = CARD_ATTACK_EVENT_TIMING.FINAL_HIT;
            _triggerChance = 1f;
            _valueMultiplier = 1f;
        }
    }

    public enum CARD_ATTACK_EVENT_TIMING
    {
        /// <summary>기존 AttackEvent와 동일하게 마지막 Hit 후 한 번 실행.</summary>
        FINAL_HIT = 0,
        ATTACK_START,
        EACH_HIT,
        ON_KILL,
    }

    public enum CARD_TYPE
    {
        NONE,
        ATTACK,
        DEFENSE,
        BUFF,
        DEBUFF,
        SPECIAL,
    }

    public enum CARD_BUFF_TARGET_TYPE
    {
        NONE,
        SELF,
        TEAM,
        ALL,
    }

    public enum CARD_BUFF_EFFECT_TYPE
    {
        NONE,
        ATTACK_UP,
        DEFENSE_UP,
        HP_UP,
        SPEED_UP,
        MAX_COST_UP,
        CUSTOM,
    }

    [Serializable]
    public class CardBuffData
    {
        [SerializeField]
        private CARD_BUFF_EFFECT_TYPE _buffEffectType;
        public CARD_BUFF_EFFECT_TYPE BuffEffectType => _buffEffectType;
        [SerializeField]
        private float _buffEffectValue;
        public float BuffEffectValue => _buffEffectValue;

        [SerializeField]
        private int _buffEffectDuration;
        public int BuffEffectDuration => _buffEffectDuration;
    }
}
