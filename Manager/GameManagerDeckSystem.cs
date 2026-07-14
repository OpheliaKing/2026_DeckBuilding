using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class GameManager
    {
        public void AddCard(UnitInfo unitInfo, string cardTid,Action<UnitInfo> onComplete = null)
        {
            if (unitInfo == null || string.IsNullOrEmpty(cardTid))
            {
                Debug.LogError("UnitInfo or CardData is null");
                return;
            }

            GetSOAsync<CardDataSO>(PublicVariable.Address.CardDataSO, cardDataSO =>
            {
                if (cardDataSO == null)
                {
                    Debug.LogError("CardDataSO is null");
                    return;
                }

                var cardData = cardDataSO.GetCardData(cardTid);
                if (cardData == null)
                {
                    Debug.LogError("CardData is null");
                    return;
                }
                unitInfo.AddCard(cardData);
                onComplete?.Invoke(unitInfo);
            });
        }
    }
}

