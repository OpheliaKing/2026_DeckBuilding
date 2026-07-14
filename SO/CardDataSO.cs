using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "CardDataSO", menuName = "SHIN/Card Data SO")]
    public class CardDataSO : ScriptableObject
    {
        [SerializeField] private List<CardData> _cardDatas = new();

        public IReadOnlyList<CardData> CardDatas => _cardDatas;
        public int Count => _cardDatas.Count;

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
    }
}
