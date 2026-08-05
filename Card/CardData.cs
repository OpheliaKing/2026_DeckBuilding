using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [Serializable]
    public class CardData
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
        /// 공격대상에게 주는 이벤트 목록
        /// </summary>
        [SerializeField]
        private List<string> _attackEvent;
        public IReadOnlyList<string> AttackEvent => _attackEvent;

        #endregion

        #region BUFF

        [SerializeField]
        private CARD_BUFF_TARGET_TYPE _buffTargetType;
        public CARD_BUFF_TARGET_TYPE BuffTargetType => _buffTargetType;

        [SerializeField]
        private CardBuffData _buffData;
        public CardBuffData BuffData => _buffData;


        #endregion
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
