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

        private int CalculateMaxHp()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 최대 체력 반환하도록 수정
            return _unitData.unitBaseHp;
        }

        private int CalculateCurrentAttack()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 데미지 반환하도록 수정
            return _unitData.unitBaseAttack;
        }

        private int CalculateCurrentDefense()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 방어력 반환하도록 수정
            return _unitData.unitBaseDefense;
        }

        private int CalculateCurrentSpeed()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 속도 반환하도록 수정
            return _unitData.unitBaseSpeed;
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
