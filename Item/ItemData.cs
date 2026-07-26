using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [Serializable]
    public class ItemData
    {
        [SerializeField]
        private string _tid;
        public string Tid => _tid;

        [SerializeField]
        private string _itemName;
        public string ItemName => _itemName;

        [SerializeField]
        private string _itemDescription;
        public string ItemDescription => _itemDescription;

        [SerializeField]
        private Sprite _itemIcon;
        public Sprite ItemIcon => _itemIcon;

        [SerializeField]
        private ITEM_GRADE _itemGrade;
        public ITEM_GRADE ItemGrade => _itemGrade;
        [SerializeField]
        private ITEM_REWARD_TYPE _itemRewardType;
        public ITEM_REWARD_TYPE ItemRewardType => _itemRewardType;

        /// <summary>ItemEffectDataSO에서 조회할 효과 tid 목록</summary>
        [SerializeField]
        private List<string> _itemEffectDatas = new();
        public IReadOnlyList<string> ItemEffectDatas => _itemEffectDatas;
    }

    public enum ITEM_GRADE
    {
        NONE,
        COMMON,
        RARE,
    }

    public enum ITEM_REWARD_TYPE
    {
        NONE,
        BOSS,
        EVENT,
    }
}
