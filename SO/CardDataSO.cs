using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "CardDataSO", menuName = "SHIN/Card Data SO")]
    public class CardDataSO : ScriptableObject
    {
        [SerializeField] private List<CardData> _cardDatas = new();

        /// <summary>
        /// CHARACTER_EQUIP_TYPE별 카드 캐시. NONE = 공용 카드.
        /// 런타임 전용(직렬화하지 않음).
        /// </summary>
        [NonSerialized]
        private Dictionary<CHARACTER_EQUIP_TYPE, List<CardData>> _cardsByEquipType;

        /// <summary>
        /// 리워드용: (무기타입, 등급)별 카드 캐시. 공용(NONE)은 무기 키 NONE으로 보관.
        /// </summary>
        [NonSerialized]
        private Dictionary<CHARACTER_EQUIP_TYPE, Dictionary<ITEM_GRADE, List<CardData>>> _rewardCardsByEquipAndGrade;

        [NonSerialized]
        private bool _isIndexBuilt;

        public IReadOnlyList<CardData> CardDatas => _cardDatas;
        public int Count => _cardDatas.Count;
        public bool IsIndexBuilt => _isIndexBuilt;

        /// <summary>
        /// _cardDatas를 CHARACTER_EQUIP_TYPE별로 분류한다. NONE은 공용 카드.
        /// BootFlow 등에서 명시적으로 호출한다.
        /// </summary>
        public void BuildIndex()
        {
            if (_cardsByEquipType == null)
                _cardsByEquipType = new Dictionary<CHARACTER_EQUIP_TYPE, List<CardData>>();
            else
                _cardsByEquipType.Clear();

            if (_rewardCardsByEquipAndGrade == null)
                _rewardCardsByEquipAndGrade =
                    new Dictionary<CHARACTER_EQUIP_TYPE, Dictionary<ITEM_GRADE, List<CardData>>>();
            else
                _rewardCardsByEquipAndGrade.Clear();

            if (_cardDatas == null)
            {
                _isIndexBuilt = true;
                return;
            }

            for (int i = 0; i < _cardDatas.Count; i++)
            {
                CardData card = _cardDatas[i];
                if (card == null)
                    continue;

                CHARACTER_EQUIP_TYPE equipType = card.CardWeaponType;
                if (!_cardsByEquipType.TryGetValue(equipType, out List<CardData> list))
                {
                    list = new List<CardData>();
                    _cardsByEquipType[equipType] = list;
                }

                list.Add(card);
                AddRewardCardIndex(card);
            }

            _isIndexBuilt = true;
            Debug.Log($"[CardDataSO] 인덱스 빌드 완료: 카드 {_cardDatas.Count}장, 타입 {_cardsByEquipType.Count}종");
        }

        /// <summary>
        /// 공용(NONE) + 지정 무기에서 해당 등급 카드를 랜덤으로 하나 뽑는다.
        /// </summary>
        public bool TryGetRandomRewardCard(
            CHARACTER_EQUIP_TYPE weaponType,
            ITEM_GRADE grade,
            out CardData cardData)
        {
            cardData = null;
            EnsureIndex();

            var pool = new List<CardData>();
            AppendRewardCardsByGrade(pool, CHARACTER_EQUIP_TYPE.NONE, grade);
            if (weaponType != CHARACTER_EQUIP_TYPE.NONE)
                AppendRewardCardsByGrade(pool, weaponType, grade);

            if (pool.Count == 0)
                return false;

            cardData = pool[UnityEngine.Random.Range(0, pool.Count)];
            return cardData != null;
        }

        private void AddRewardCardIndex(CardData card)
        {
            CHARACTER_EQUIP_TYPE equipType = card.CardWeaponType;
            ITEM_GRADE grade = card.CardGrade;

            if (!_rewardCardsByEquipAndGrade.TryGetValue(
                    equipType,
                    out Dictionary<ITEM_GRADE, List<CardData>> byGrade))
            {
                byGrade = new Dictionary<ITEM_GRADE, List<CardData>>();
                _rewardCardsByEquipAndGrade[equipType] = byGrade;
            }

            if (!byGrade.TryGetValue(grade, out List<CardData> list))
            {
                list = new List<CardData>();
                byGrade[grade] = list;
            }

            list.Add(card);
        }

        private void AppendRewardCardsByGrade(
            List<CardData> result,
            CHARACTER_EQUIP_TYPE equipType,
            ITEM_GRADE grade)
        {
            if (_rewardCardsByEquipAndGrade == null)
                return;

            if (!_rewardCardsByEquipAndGrade.TryGetValue(equipType, out Dictionary<ITEM_GRADE, List<CardData>> byGrade))
                return;

            if (!byGrade.TryGetValue(grade, out List<CardData> list) || list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    result.Add(list[i]);
            }
        }

        /// <summary>
        /// 인덱스가 없으면 빌드 후 해당 타입 카드 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<CardData> GetCardsByEquipType(CHARACTER_EQUIP_TYPE equipType)
        {
            EnsureIndex();

            if (_cardsByEquipType.TryGetValue(equipType, out List<CardData> list))
                return list;

            return Array.Empty<CardData>();
        }

        /// <summary>
        /// 공용(NONE) + 지정 무기 타입 카드를 합쳐 반환한다.
        /// </summary>
        public List<CardData> GetCardsForWeapon(CHARACTER_EQUIP_TYPE weaponType)
        {
            EnsureIndex();

            var result = new List<CardData>();
            AppendCards(result, CHARACTER_EQUIP_TYPE.NONE);

            if (weaponType != CHARACTER_EQUIP_TYPE.NONE)
                AppendCards(result, weaponType);

            return result;
        }

        public CardData GetCardData(int index)
        {
            if (index < 0 || index >= _cardDatas.Count)
            {
                Debug.LogError($"[CardDataSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _cardDatas[index];
        }

        public CardData GetCardData(string cardTid)
        {
            if (string.IsNullOrEmpty(cardTid))
            {
                Debug.LogError("[CardDataSO] cardTid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _cardDatas.Count; i++)
            {
                if (_cardDatas[i].Tid == cardTid)
                    return _cardDatas[i];
            }

            Debug.LogError($"[CardDataSO] cardTid를 찾을 수 없습니다: {cardTid}");
            return null;
        }

        public bool TryGetCardData(string cardTid, out CardData cardData)
        {
            cardData = null;

            if (string.IsNullOrEmpty(cardTid))
                return false;

            for (int i = 0; i < _cardDatas.Count; i++)
            {
                if (_cardDatas[i].Tid == cardTid)
                {
                    cardData = _cardDatas[i];
                    return true;
                }
            }

            return false;
        }

        private void EnsureIndex()
        {
            if (_isIndexBuilt && _cardsByEquipType != null)
                return;

            BuildIndex();
        }

        private void AppendCards(List<CardData> result, CHARACTER_EQUIP_TYPE equipType)
        {
            if (!_cardsByEquipType.TryGetValue(equipType, out List<CardData> list) || list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    result.Add(list[i]);
            }
        }
    }
}
