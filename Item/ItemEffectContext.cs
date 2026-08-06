namespace SHIN
{
    /// <summary>
    /// 기존 코드 호환용 아이템 컨텍스트.
    /// 신규 전투 이벤트 코드는 CombatEventContext를 사용합니다.
    /// </summary>
    public class ItemEffectContext : CombatEventContext
    {
        public bool FromItemEffect
        {
            get => Origin == COMBAT_EVENT_ORIGIN.ITEM_EFFECT;
            set
            {
                if (value)
                    Origin = COMBAT_EVENT_ORIGIN.ITEM_EFFECT;
                else if (Origin == COMBAT_EVENT_ORIGIN.ITEM_EFFECT)
                    Origin = COMBAT_EVENT_ORIGIN.NONE;
            }
        }
    }
}
