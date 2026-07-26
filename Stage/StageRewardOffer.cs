using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public enum STAGE_REWARD_KIND
    {
        CARD,
        ITEM,
    }

    /// <summary>
    /// 스테이지 클리어 후 3택1에 제시되는 보상 1개.
    /// </summary>
    [Serializable]
    public class StageRewardOffer
    {
        public STAGE_REWARD_KIND Kind;
        public string Tid;
        public CardData CardData;
        public ItemData ItemData;
        public ITEM_GRADE Grade;

        public string DisplayName
        {
            get
            {
                if (Kind == STAGE_REWARD_KIND.CARD)
                    return CardData != null ? CardData.Name : Tid;
                return ItemData != null ? ItemData.ItemName : Tid;
            }
        }

        public string DisplayDescription
        {
            get
            {
                if (Kind == STAGE_REWARD_KIND.CARD)
                    return CardData != null ? CardData.Description : string.Empty;
                return ItemData != null ? ItemData.ItemDescription : string.Empty;
            }
        }
    }

    /// <summary>
    /// 등급별 가중치. Inspector에서 확률 튜닝용.
    /// </summary>
    [Serializable]
    public struct RewardGradeWeight
    {
        public ITEM_GRADE Grade;
        [Tooltip("상대 가중치. 합이 100일 필요는 없음")]
        public float Weight;
    }

    /// <summary>
    /// 진행도 구간별 보상 확률 테이블.
    /// </summary>
    [Serializable]
    public struct StageRewardProgressTable
    {
        [Tooltip("포함 최소 진행도(클리어한 전투 수 등)")]
        public int MinProgressStep;

        [Tooltip("포함 최대 진행도. -1이면 상한 없음")]
        public int MaxProgressStep;

        [Tooltip("카드가 나올 확률(0~1). 나머지는 아이템")]
        [Range(0f, 1f)]
        public float CardChance;

        public RewardGradeWeight[] GradeWeights;
    }
}
