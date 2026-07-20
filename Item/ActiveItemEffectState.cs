namespace SHIN
{
    /// <summary>
    /// 전투 중 아이템 효과의 런타임 상태.
    /// ItemEffectData는 설계값, 이 클래스는 COUNT 누적 등 전투 중 값만 보관합니다.
    /// </summary>
    public class ActiveItemEffectState
    {
        public ItemData SourceItem { get; }
        public ItemEffectData EffectData { get; }

        /// <summary>COUNT 조건용 누적 횟수</summary>
        public int TriggerCounter { get; set; }

        public ActiveItemEffectState(ItemData sourceItem, ItemEffectData effectData)
        {
            SourceItem = sourceItem;
            EffectData = effectData;
            TriggerCounter = 0;
        }

        public ITEM_EFFECT_TIMING Timing =>
            EffectData != null ? EffectData.EffectTiming : ITEM_EFFECT_TIMING.NONE;
    }
}
