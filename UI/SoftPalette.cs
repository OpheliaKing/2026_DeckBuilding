using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 아웃게임(스테이지 맵/인벤토리) 공통 소프트 핑크·로즈골드 팔레트.
    /// </summary>
    public static class SoftPalette
    {
        public static readonly Color DimOverlay = new(0.28f, 0.12f, 0.2f, 0.55f);
        public static readonly Color TextPrimary = new(0.42f, 0.28f, 0.36f, 1f);
        public static readonly Color TextSecondary = new(0.52f, 0.36f, 0.44f, 1f);
        public static readonly Color TextMuted = new(0.62f, 0.48f, 0.55f, 1f);
        public static readonly Color AccentRoseGold = new(0.78f, 0.52f, 0.42f, 1f);
        public static readonly Color TabInactiveTint = new(1f, 1f, 1f, 0.72f);
        public static readonly Color SlotNormal = Color.white;
        public static readonly Color SlotSelected = new(1f, 0.9f, 0.78f, 1f);

        // StageReward (라벤더·로즈골드 프레임)
        public static readonly Color RewardLavender = new(0.86f, 0.82f, 0.92f, 1f);
        public static readonly Color RewardPanelFill = new(0.78f, 0.74f, 0.88f, 1f);
        public static readonly Color RewardText = new(0.36f, 0.24f, 0.42f, 1f);
        public static readonly Color RewardButtonTint = new(1f, 0.92f, 0.94f, 1f);
    }
}
