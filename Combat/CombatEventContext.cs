using System.Collections.Generic;

namespace SHIN
{
    public enum COMBAT_EVENT_ORIGIN
    {
        NONE,
        CARD_ATTACK,
        ITEM_EFFECT,
    }

    /// <summary>
    /// 카드와 아이템이 공용으로 사용하는 전투 이벤트 실행 컨텍스트.
    /// </summary>
    public class CombatEventContext
    {
        public CharacterBase Owner;
        public CharacterBase Source;
        public CharacterBase Target;
        public IReadOnlyList<CharacterBase> AffectedTargets;
        public CardData Card;
        public int Damage;
        public int HealAmount;
        public COMBAT_EVENT_ORIGIN Origin;

        public CombatEventContext CopyWithOrigin(COMBAT_EVENT_ORIGIN origin)
        {
            return new CombatEventContext
            {
                Owner = Owner,
                Source = Source,
                Target = Target,
                AffectedTargets = AffectedTargets,
                Card = Card,
                Damage = Damage,
                HealAmount = HealAmount,
                Origin = origin,
            };
        }
    }
}
