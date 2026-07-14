using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class GameManager
    {
        public void AddCard(UnitInfo unitInfo, string cardTid, Action<UnitInfo> onComplete = null)
        {
            if (unitInfo == null || string.IsNullOrEmpty(cardTid))
            {
                Debug.LogError("[GameManager] UnitInfo 또는 cardTid가 비어 있습니다.");
                return;
            }

            AddCard(unitInfo, new List<string> { cardTid }, onComplete);
        }

        public void AddCard(UnitInfo unitInfo, List<string> cardTids, Action<UnitInfo> onComplete = null)
        {
            if (unitInfo == null)
            {
                Debug.LogError("[GameManager] UnitInfo가 null입니다.");
                return;
            }

            if (cardTids == null || cardTids.Count == 0)
            {
                Debug.LogError("[GameManager] cardTids가 비어 있습니다.");
                return;
            }

            GetSOAsync<CardDataSO>(PublicVariable.Address.CardDataSO, cardDataSO =>
            {
                if (cardDataSO == null)
                {
                    Debug.LogError("[GameManager] CardDataSO 로드 실패");
                    return;
                }

                for (int i = 0; i < cardTids.Count; i++)
                {
                    var cardTid = cardTids[i];
                    if (string.IsNullOrEmpty(cardTid))
                    {
                        Debug.LogWarning($"[GameManager] cardTids[{i}]가 비어 있어 건너뜁니다.");
                        continue;
                    }

                    var cardData = cardDataSO.GetCardData(cardTid);
                    if (cardData == null)
                    {
                        Debug.LogError($"[GameManager] CardData를 찾을 수 없습니다: {cardTid}");
                        continue;
                    }

                    unitInfo.AddDeckCard(cardData);
                }

                onComplete?.Invoke(unitInfo);
            });
        }
    }
}
