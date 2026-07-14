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
        private int _cost;
        public int Cost => _cost;

        [SerializeField]
        private CARD_TYPE _cardType;
        public CARD_TYPE CardType => _cardType;

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
        /// 공격대상에게 주는 이벤트 목록
        /// </summary>
        [SerializeField]
        private List<string> _attackEvent;
        public IReadOnlyList<string> AttackEvent => _attackEvent;

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
}
