using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public class UnitInfo
    {
        public UnitInfo(UnitData unitData)
        {
            _unitData = unitData;
            InitUnitInfo();
        }

        private UnitData _unitData;
        public UnitData UnitData => _unitData;

        private int _maxHp;
        public int MaxHp => CalculateMaxHp();

        private int _currentHp;
        public int CurrentHp => _currentHp;
        public bool IsDead => _currentHp <= 0;

        public int CurrentAttack => CalculateCurrentAttack();

        public int CurrentDefense => CalculateCurrentDefense();

        public int CurrentSpeed => CalculateCurrentSpeed();

        private List<string> _itemList = new List<string>();

        #region Card

        /// <summary>마스터 덱 (영구 보유 카드)</summary>
        private List<CardData> _deckCardList = new List<CardData>();
        public IReadOnlyList<CardData> DeckCardList => _deckCardList;

        /// <summary>전투용 드로우 더미</summary>
        private List<CardData> _drawPile = new List<CardData>();
        public IReadOnlyList<CardData> DrawPile => _drawPile;

        /// <summary>손패</summary>
        private List<CardData> _hand = new List<CardData>();
        public IReadOnlyList<CardData> Hand => _hand;

        /// <summary>버린 패</summary>
        private List<CardData> _discardPile = new List<CardData>();
        public IReadOnlyList<CardData> DiscardPile => _discardPile;

        private int _drawCardCount = 3;
        public int DrawCardCount => _drawCardCount;

        #endregion

        #region Buff

        private readonly List<ActiveBuff> _activeBuffs = new();
        public IReadOnlyList<ActiveBuff> ActiveBuffs => _activeBuffs;

        public void AddBuff(CARD_BUFF_EFFECT_TYPE effectType, float value, int duration, string sourceCardTid = null)
        {
            if (effectType == CARD_BUFF_EFFECT_TYPE.NONE || duration <= 0)
                return;

            _activeBuffs.Add(new ActiveBuff
            {
                EffectType = effectType,
                Value = value,
                RemainingTurns = duration,
                SourceCardTid = sourceCardTid,
            });
        }

        /// <summary>
        /// 턴 시작 시 남은 지속시간을 1 감소하고 0이면 제거합니다.
        /// </summary>
        public void TickBuffsOnTurnStart()
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                _activeBuffs[i].RemainingTurns--;
                if (_activeBuffs[i].RemainingTurns <= 0)
                    _activeBuffs.RemoveAt(i);
            }
        }

        private float GetBuffValueSum(CARD_BUFF_EFFECT_TYPE effectType)
        {
            float sum = 0f;
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].EffectType == effectType)
                    sum += _activeBuffs[i].Value;
            }

            return sum;
        }

        #endregion

        public void InitUnitInfo(UnitData unitData)
        {
            _unitData = unitData;
            InitUnitInfo();
        }

        public void InitUnitInfo()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return;
            }

            _currentHp = MaxHp;
        }

        /// <summary>
        /// 데미지를 적용하고 실제 감소량을 반환합니다.
        /// </summary>
        public int ApplyDamage(int damage)
        {
            if (damage <= 0 || IsDead)
                return 0;

            int before = _currentHp;
            _currentHp = Mathf.Max(0, _currentHp - damage);
            return before - _currentHp;
        }

        private int CalculateMaxHp()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 최대 체력 반환하도록 수정
            int baseHp = _unitData.unitBaseHp;
            int hpBonus = Mathf.FloorToInt(GetBuffValueSum(CARD_BUFF_EFFECT_TYPE.HP_UP));
            return Mathf.Max(1, baseHp + hpBonus);
        }

        private int CalculateCurrentAttack()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 데미지 반환하도록 수정
            int baseAttack = _unitData.unitBaseAttack;
            int attackBonus = Mathf.FloorToInt(GetBuffValueSum(CARD_BUFF_EFFECT_TYPE.ATTACK_UP));
            return Mathf.Max(0, baseAttack + attackBonus);
        }

        private int CalculateCurrentDefense()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 방어력 반환하도록 수정
            int baseDefense = _unitData.unitBaseDefense;
            int defenseBonus = Mathf.FloorToInt(GetBuffValueSum(CARD_BUFF_EFFECT_TYPE.DEFENSE_UP));
            return Mathf.Max(0, baseDefense + defenseBonus);
        }

        private int CalculateCurrentSpeed()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 속도 반환하도록 수정
            int baseSpeed = _unitData.unitBaseSpeed;
            int speedBonus = Mathf.FloorToInt(GetBuffValueSum(CARD_BUFF_EFFECT_TYPE.SPEED_UP));
            return Mathf.Max(1, baseSpeed + speedBonus);
        }

        public void AddDeckCard(CardData cardData)
        {
            if (cardData == null)
            {
                Debug.LogError("CardData is null");
                return;
            }

            _deckCardList.Add(cardData);
        }

        /// <summary>
        /// 전투 시작 시 마스터 덱을 복사해 드로우 더미를 만들고 섞습니다.
        /// </summary>
        public void InitCombatDeck()
        {
            ClearCombatDeck();

            _drawPile.AddRange(_deckCardList);
            Shuffle(_drawPile);
        }

        /// <summary>
        /// 전투용 더미(드로우/손패/버린 패)만 비웁니다. 마스터 덱은 유지됩니다.
        /// </summary>
        public void ClearCombatDeck()
        {
            _drawPile.Clear();
            _hand.Clear();
            _discardPile.Clear();
        }

        /// <summary>
        /// 드로우 더미에서 손패로 카드를 뽑습니다. 더미가 비면 버린 패를 섞어 보충합니다.
        /// </summary>
        public List<CardData> DrawCards(int count = -1)
        {
            if (count < 0)
                count = _drawCardCount;

            var drawnCards = new List<CardData>();

            for (int i = 0; i < count; i++)
            {
                if (_drawPile.Count == 0)
                {
                    if (_discardPile.Count == 0)
                        break;

                    RefillDrawPileFromDiscard();
                }

                if (_drawPile.Count == 0)
                    break;

                var card = _drawPile[_drawPile.Count - 1];
                _drawPile.RemoveAt(_drawPile.Count - 1);
                _hand.Add(card);
                drawnCards.Add(card);
            }

            return drawnCards;
        }

        /// <summary>
        /// 손패의 카드를 버린 패로 보냅니다.
        /// </summary>
        public bool DiscardFromHand(CardData cardData)
        {
            if (cardData == null || !_hand.Remove(cardData))
                return false;

            _discardPile.Add(cardData);
            return true;
        }

        /// <summary>
        /// 손패 전체를 버린 패로 보냅니다.
        /// </summary>
        public void DiscardAllHand()
        {
            if (_hand.Count == 0)
                return;

            _discardPile.AddRange(_hand);
            _hand.Clear();
        }

        private void RefillDrawPileFromDiscard()
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_drawPile);
        }

        private static void Shuffle(List<CardData> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (cards[i], cards[randomIndex]) = (cards[randomIndex], cards[i]);
            }
        }
    }
}
