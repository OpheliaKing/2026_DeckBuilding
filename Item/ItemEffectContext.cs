namespace SHIN
{
    /// <summary>
    /// 아이템 효과 발동/전투 이벤트 실행에 필요한 컨텍스트.
    /// </summary>
    public class ItemEffectContext
    {
        /// <summary>아이템을 보유한 유닛 (효과 검사 대상)</summary>
        public CharacterBase Owner;

        /// <summary>행동의 주체 (공격자, 카드 사용자 등)</summary>
        public CharacterBase Source;

        /// <summary>행동의 대상 (피격자 등)</summary>
        public CharacterBase Target;

        public CardData Card;
        public int Damage;
        public int HealAmount;

        /// <summary>아이템 발동으로 생긴 후속 이벤트면 true (재진입 방지용)</summary>
        public bool FromItemEffect;
    }
}
