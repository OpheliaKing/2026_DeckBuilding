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

        private UNIT_TYPE _unitType = UNIT_TYPE.NONE;
        public UNIT_TYPE UnitType => _unitType;

        /// <summary>
        /// 전투용 복사본이 가리키는 런 영속 UnitInfo. 런 원본이면 null.
        /// </summary>
        private UnitInfo _runSourceUnitInfo;
        public UnitInfo RunSourceUnitInfo => _runSourceUnitInfo;
        public bool IsCombatClone => _runSourceUnitInfo != null;

        public void SetUnitType(UNIT_TYPE unitType)
        {
            _unitType = unitType;
        }

        private int _maxHp;
        public int MaxHp => CalculateMaxHp();

        private int _currentHp;
        public int CurrentHp => _currentHp;
        public bool IsDead => _currentHp <= 0;

        public int CurrentAttack => CalculateCurrentAttack();

        public int CurrentDefense => CalculateCurrentDefense();

        public int CurrentSpeed => CalculateCurrentSpeed();

        #region Card Cost

        /// <summary>버프/아이템 반영된 최대 카드 코스트</summary>
        public int MaxCardCost => CalculateMaxCardCost();

        private int _currentCardCost;
        /// <summary>이번 턴 남은 카드 코스트</summary>
        public int CurrentCardCost => _currentCardCost;

        /// <summary>
        /// 카드 사용에 필요한 코스트를 지불할 수 있는지 확인합니다.
        /// </summary>
        public bool CanAffordCard(CardData card)
        {
            if (card == null)
                return false;

            return CanAffordCardCost(card.Cost);
        }

        public bool CanAffordCardCost(int cost)
        {
            return cost <= _currentCardCost;
        }

        /// <summary>
        /// 카드 코스트를 소모합니다. 부족하면 false.
        /// </summary>
        public bool TrySpendCardCost(int cost)
        {
            if (cost < 0)
                cost = 0;

            if (!CanAffordCardCost(cost))
                return false;

            _currentCardCost -= cost;
            return true;
        }

        public bool TrySpendCardCost(CardData card)
        {
            if (card == null)
                return false;

            return TrySpendCardCost(card.Cost);
        }

        /// <summary>
        /// 턴 시작 시 현재 코스트를 최대치로 회복합니다.
        /// </summary>
        public void RefillCardCost()
        {
            _currentCardCost = MaxCardCost;
        }

        private int CalculateMaxCardCost()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }

            int baseCost = Mathf.Max(0, _unitData.unitBaseMaxCardCost);
            // 아이템/버프로 최대 코스트 증가
            int costBonus = Mathf.FloorToInt(GetBuffValueSum(BUFF_EFFECT_TYPE.MAX_COST_UP));
            return Mathf.Max(0, baseCost + costBonus);
        }

        #endregion

        #region Item

        private readonly List<ItemData> _items = new();
        public IReadOnlyList<ItemData> Items => _items;

        private readonly List<ActiveItemEffectState> _activeItemEffects = new();
        public IReadOnlyList<ActiveItemEffectState> ActiveItemEffects => _activeItemEffects;

        private ItemDataSO _itemDataSO;
        private ItemEffectDataSO _itemEffectDataSO;

        public void SetItemDataSO(ItemDataSO itemDataSO)
        {
            _itemDataSO = itemDataSO;
        }

        public void SetItemEffectDataSO(ItemEffectDataSO itemEffectDataSO)
        {
            _itemEffectDataSO = itemEffectDataSO;
        }

        public void AddItem(ItemData itemData)
        {
            if (itemData == null)
            {
                Debug.LogError("[UnitInfo] ItemData is null");
                return;
            }

            _items.Add(itemData);
            RebuildActiveItemEffects();
        }

        /// <summary>
        /// ItemDataSO에서 tid로 ItemData를 조회해 추가합니다.
        /// 로컬/캐시에 SO가 없으면 ResourceManager로 로드한 뒤 추가합니다.
        /// </summary>
        /// <returns>즉시 추가되었거나 비동기 로드 요청이 시작되면 true</returns>
        public bool AddItem(string itemTid)
        {
            if (string.IsNullOrEmpty(itemTid))
            {
                Debug.LogError("[UnitInfo] itemTid가 비어 있습니다.");
                return false;
            }

            if (TryResolveItemDataSO())
                return TryAddItemByTid(itemTid);

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[UnitInfo] GameManager가 없어 ItemDataSO를 로드할 수 없습니다.");
                return false;
            }

            gameManager.GetSOAsync<ItemDataSO>(PublicVariable.Address.ItemDataSO, itemDataSO =>
            {
                if (itemDataSO == null)
                {
                    Debug.LogError("[UnitInfo] ItemDataSO 로드 실패");
                    return;
                }

                _itemDataSO = itemDataSO;
                TryAddItemByTid(itemTid);
            });

            return true;
        }

        private bool TryResolveItemDataSO()
        {
            if (_itemDataSO != null)
                return true;

            var gameManager = GameManager.Instance;
            if (gameManager != null &&
                gameManager.TryGetSO(PublicVariable.Address.ItemDataSO, out ItemDataSO cached))
            {
                _itemDataSO = cached;
                return true;
            }

            return false;
        }

        private bool TryAddItemByTid(string itemTid)
        {
            if (_itemDataSO == null)
            {
                Debug.LogError("[UnitInfo] ItemDataSO가 없습니다.");
                return false;
            }

            if (!_itemDataSO.TryGetItemData(itemTid, out var itemData) || itemData == null)
            {
                Debug.LogError($"[UnitInfo] ItemData를 찾을 수 없습니다: {itemTid}");
                return false;
            }

            AddItem(itemData);
            return true;
        }

        public bool RemoveItem(ItemData itemData)
        {
            if (itemData == null || !_items.Remove(itemData))
                return false;

            RebuildActiveItemEffects();
            return true;
        }

        public void ClearItems()
        {
            _items.Clear();
            _activeItemEffects.Clear();
        }

        /// <summary>
        /// 보유 아이템의 effect tid를 ItemEffectDataSO에서 조회해 런타임 효과 상태를 재구성합니다.
        /// </summary>
        public void RebuildActiveItemEffects()
        {
            _activeItemEffects.Clear();

            if (_itemEffectDataSO == null)
            {
                if (_items.Count > 0)
                    Debug.LogWarning("[UnitInfo] ItemEffectDataSO가 없어 아이템 효과를 구성할 수 없습니다.");
                return;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item?.ItemEffectDatas == null)
                    continue;

                for (int j = 0; j < item.ItemEffectDatas.Count; j++)
                {
                    var effectTid = item.ItemEffectDatas[j];
                    if (string.IsNullOrEmpty(effectTid))
                        continue;

                    if (!_itemEffectDataSO.TryGetItemEffectData(effectTid, out var effect) || effect == null)
                    {
                        Debug.LogError(
                            $"[UnitInfo] ItemEffectData를 찾을 수 없습니다: item={item.Tid} / effect={effectTid}");
                        continue;
                    }

                    if (effect.EffectTiming == ITEM_EFFECT_TIMING.NONE)
                        continue;

                    _activeItemEffects.Add(new ActiveItemEffectState(item, effect));
                }
            }
        }

        #endregion

        #region Equip

        private CHARACTER_EQUIP_TYPE _equipType = CHARACTER_EQUIP_TYPE.NONE;
        public CHARACTER_EQUIP_TYPE EquipType => _equipType;

        public void SetEquipType(CHARACTER_EQUIP_TYPE equipType)
        {
            _equipType = equipType;
        }

        #endregion

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

        public void AddBuff(BuffData buffData, string sourceTid = null)
        {
            if (buffData == null || !buffData.IsValid)
                return;

            string source = !string.IsNullOrEmpty(sourceTid)
                ? sourceTid
                : buffData.Tid;
            AddBuff(buffData.EffectType, buffData.Value, buffData.Duration, source);
        }

        public void AddBuff(BUFF_EFFECT_TYPE effectType, float value, int duration, string sourceTid = null)
        {
            if (effectType == BUFF_EFFECT_TYPE.NONE || duration <= 0)
                return;

            // 방어도는 같은 타입끼리 합산해 관리
            if (effectType == BUFF_EFFECT_TYPE.BLOCK)
            {
                for (int i = 0; i < _activeBuffs.Count; i++)
                {
                    if (_activeBuffs[i].EffectType != BUFF_EFFECT_TYPE.BLOCK)
                        continue;

                    _activeBuffs[i].Value += Mathf.Max(0f, value);
                    _activeBuffs[i].RemainingTurns = Mathf.Max(_activeBuffs[i].RemainingTurns, duration);
                    if (string.IsNullOrEmpty(_activeBuffs[i].SourceCardTid))
                        _activeBuffs[i].SourceCardTid = sourceTid;
                    return;
                }
            }

            _activeBuffs.Add(new ActiveBuff
            {
                EffectType = effectType,
                Value = Mathf.Max(0f, value),
                RemainingTurns = duration,
                SourceCardTid = sourceTid,
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

        public void ClearBuffs()
        {
            _activeBuffs.Clear();
        }

        public float GetBuffValueSum(BUFF_EFFECT_TYPE effectType)
        {
            float sum = 0f;
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].EffectType == effectType)
                    sum += _activeBuffs[i].Value;
            }

            return sum;
        }

        public int CurrentBlock => Mathf.FloorToInt(GetBuffValueSum(BUFF_EFFECT_TYPE.BLOCK));

        /// <summary>
        /// 데미지를 적용하고 실제 HP 감소량을 반환합니다.
        /// BLOCK 방어도가 있으면 먼저 흡수합니다.
        /// </summary>
        public int ApplyDamage(int damage)
        {
            if (damage <= 0 || IsDead)
                return 0;

            int remaining = AbsorbDamageWithBlock(damage);
            if (remaining <= 0)
                return 0;

            int before = _currentHp;
            _currentHp = Mathf.Max(0, _currentHp - remaining);
            return before - _currentHp;
        }

        private int AbsorbDamageWithBlock(int damage)
        {
            int remaining = damage;
            for (int i = 0; i < _activeBuffs.Count && remaining > 0; i++)
            {
                var buff = _activeBuffs[i];
                if (buff.EffectType != BUFF_EFFECT_TYPE.BLOCK || buff.Value <= 0f)
                    continue;

                int blockValue = Mathf.FloorToInt(buff.Value);
                if (blockValue <= 0)
                    continue;

                int absorbed = Mathf.Min(blockValue, remaining);
                buff.Value -= absorbed;
                remaining -= absorbed;
            }

            CleanupDepletedBlockBuffs();
            return remaining;
        }

        private void CleanupDepletedBlockBuffs()
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                if (_activeBuffs[i].EffectType == BUFF_EFFECT_TYPE.BLOCK &&
                    _activeBuffs[i].Value <= 0f)
                {
                    _activeBuffs.RemoveAt(i);
                }
            }
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
            RefillCardCost();
        }

        /// <summary>
        /// 런 영속 데이터를 전투용으로 복사한다.
        /// 마스터 덱/아이템/장비/체력은 복사하고, 버프·손패 등 전투 전용 상태는 비운다.
        /// </summary>
        public UnitInfo CloneForCombat()
        {
            if (_unitData == null)
            {
                Debug.LogError("[UnitInfo] CloneForCombat: UnitData가 null입니다.");
                return null;
            }

            var clone = new UnitInfo(_unitData);
            clone._runSourceUnitInfo = _runSourceUnitInfo != null ? _runSourceUnitInfo : this;
            clone._unitType = _unitType;
            clone._equipType = _equipType;
            clone._drawCardCount = _drawCardCount;
            clone._itemDataSO = _itemDataSO;
            clone._itemEffectDataSO = _itemEffectDataSO;

            clone._items.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null)
                    clone._items.Add(_items[i]);
            }

            clone.RebuildActiveItemEffects();

            clone._deckCardList.Clear();
            for (int i = 0; i < _deckCardList.Count; i++)
            {
                if (_deckCardList[i] != null)
                    clone._deckCardList.Add(_deckCardList[i]);
            }

            clone.ClearCombatDeck();
            clone.ClearBuffs();
            clone.SetCurrentHp(_currentHp);
            clone.RefillCardCost();
            return clone;
        }

        /// <summary>
        /// 전투 종료 시 복사본 체력을 런 영속 UnitInfo에 반영한다.
        /// </summary>
        public void SyncHpToRunSource()
        {
            if (_runSourceUnitInfo == null)
                return;

            _runSourceUnitInfo.SetCurrentHp(_currentHp);
        }

        public void SetCurrentHp(int hp)
        {
            int maxHp = MaxHp;
            _currentHp = Mathf.Clamp(hp, 0, Mathf.Max(0, maxHp));
        }

        /// <summary>
        /// 회복을 적용하고 실제 회복량을 반환합니다. 최대 체력을 넘지 않습니다.
        /// </summary>
        public int ApplyHeal(int amount)
        {
            if (amount <= 0 || IsDead)
                return 0;

            int before = _currentHp;
            int maxHp = MaxHp;
            _currentHp = Mathf.Min(maxHp, _currentHp + amount);
            return _currentHp - before;
        }

        private int CalculateMaxHp()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }

            int baseHp = _unitData.unitBaseHp;
            int hpBonus = Mathf.FloorToInt(GetBuffValueSum(BUFF_EFFECT_TYPE.HP_UP));
            return Mathf.Max(1, baseHp + hpBonus);
        }

        private int CalculateCurrentAttack()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }

            int baseAttack = _unitData.unitBaseAttack;
            int attackBonus = Mathf.FloorToInt(
                GetBuffValueSum(BUFF_EFFECT_TYPE.ATTACK_UP) +
                GetBuffValueSum(BUFF_EFFECT_TYPE.STRENGTH));
            return Mathf.Max(0, baseAttack + attackBonus);
        }

        private int CalculateCurrentDefense()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }

            int baseDefense = _unitData.unitBaseDefense;
            int defenseBonus = Mathf.FloorToInt(GetBuffValueSum(BUFF_EFFECT_TYPE.DEFENSE_UP));
            return Mathf.Max(0, baseDefense + defenseBonus);
        }

        private int CalculateCurrentSpeed()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }

            int baseSpeed = _unitData.unitBaseSpeed;
            int speedBonus = Mathf.FloorToInt(GetBuffValueSum(BUFF_EFFECT_TYPE.SPEED_UP));
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

    public enum UNIT_TYPE
    {
        NONE,
        PLAYER,
        NPC,
    }
}
