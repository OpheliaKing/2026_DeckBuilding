namespace SHIN
{
    /// <summary>
    /// 공용으로 사용하는 상수/주소 모음 (SO Addressables 경로 등)
    /// </summary>
    public static class PublicVariable
    {
        public static class Address
        {
            public const string StageDataSO = "Assets/Addressables/SO/StageDataSO.asset";
            public const string StageStepDataSO = "Assets/Addressables/SO/StageStepDataSO.asset";
            public const string UnitDataSO = "Assets/Addressables/SO/UnitDataSO.asset";
            public const string CardDataSO = "Assets/Addressables/SO/CardDataSO.asset";
            public const string ItemDataSO = "Assets/Addressables/SO/ItemDataSO.asset";
            public const string StageEventDataSO = "Assets/Addressables/SO/StageEventDataSO.asset";
            public const string EndingSequenceSO = "Assets/Addressables/SO/EndingSequenceSO.asset";
            public const string ItemEffectDataSO = "Assets/Addressables/SO/ItemEffectDataSO.asset";
            public const string InGameCombatEventSO = "Assets/Addressables/SO/InGameCombatEventSO.asset";
            public const string BuffDataSO = "Assets/Addressables/SO/BuffDataSO.asset";
            public const string CharacterSelectDataSO = "Assets/Addressables/SO/CharacterSelectDataSO.asset";
            public const string WeaponDataSO = "Assets/Addressables/SO/WeaponDataSO.asset";
            public const string BGMDataSO = "Assets/Addressables/SO/BGMDataSO.asset";
            public const string CardObjectPrefab = "Assets/Addressables/Prefab/UI/Card/CardObject.prefab";
            public const string CardFrameSprite = "Assets/Art/Image/Card/scarlet_draw_card_frame.png";
            public const string StageSoftButtonSprite =
                "Assets/Art/Image/UI/scarlet_stage_soft_button.png";
            public const string StageMapPanelSprite =
                "Assets/Art/Image/UI/scarlet_stage_map_panel.png";
            public const string PlayerUIPrefab = "Assets/Addressables/Prefab/UI/PlayerUI.prefab";
            public const string StageNodeUIPrefab = "Assets/Addressables/Prefab/UI/Stage/StageNodeUI.prefab";
            public const string StageNodeObjectUIPrefab = "Assets/Addressables/Prefab/UI/Stage/StageNodeObjectUI.prefab";
            public const string StageMapHudPrefab = "Assets/Addressables/Prefab/UI/Stage/StageMapHud.prefab";
            public const string StartUIPrefab =
                "Assets/Addressables/Prefab/UI/StartUI.prefab";
            public const string CharacterSelectObjectPrefab =
                "Assets/Addressables/Prefab/CharacterSelect/CharacterSelectObject.prefab";
            public const string UnitSetupUIPrefab =
                "Assets/Addressables/Prefab/UI/CharacterSetup/UnitSetupUI.prefab";
            public const string CharacterSelectUIPrefab =
                "Assets/Addressables/Prefab/UI/CharacterSetup/CharacterSelectUI.prefab";
            public const string CharacterSelectButtonPrefab =
                "Assets/Addressables/Prefab/UI/CharacterSetup/CharacterSelectButton.prefab";
            public const string WeaponSelectUIPrefab =
                "Assets/Addressables/Prefab/UI/CharacterSetup/WeaponSelectUI.prefab";
            public const string StageRewardUIPrefab =
                "Assets/Addressables/Prefab/UI/StageReward/StageRewardUI.prefab";
            public const string StageRewardObjectPrefab =
                "Assets/Addressables/Prefab/UI/StageReward/StageRewardObject.prefab";
            public const string StageEventUIPrefab =
                "Assets/Addressables/Prefab/UI/StageEvent/StageEventUI.prefab";
            public const string StageEventUIButtonPrefab =
                "Assets/Addressables/Prefab/UI/StageEvent/StageEventUIButton.prefab";
            public const string StageShopUIPrefab =
                "Assets/Addressables/Prefab/UI/StageShop/StageShopUI.prefab";
            public const string StageShopUIObjectPrefab =
                "Assets/Addressables/Prefab/UI/StageShop/StageShopUIObject.prefab";
            public const string InventoryUIPrefab =
                "Assets/Addressables/Prefab/UI/InventoryUI.prefab";
            public const string OptionUIPrefab =
                "Assets/Addressables/Prefab/UI/OptionUI.prefab";
            public const string CharacterStatusUIPrefab =
                "Assets/Addressables/Prefab/UI/CharacterStatusUI.prefab";
            public const string NotoSansKrRegularFont =
                "Assets/Addressables/Font/NotoSansKR-Regular SDF.asset";
            public const string GowunBatangRegularFont =
                "Assets/Addressables/Font/GowunBatang-Regular SDF.asset";
            public const string GowunBatangBoldFont =
                "Assets/Addressables/Font/GowunBatang-Bold SDF.asset";
            public const string GowunDodumRegularFont =
                "Assets/Addressables/Font/GowunDodum-Regular SDF.asset";
            public const string EndingSequenceUIPrefab =
                "Assets/Addressables/Prefab/UI/EndingSequenceUI.prefab";
            public const string FadeUIPrefab =
                "Assets/Addressables/Prefab/UI/FadeUI.prefab";
            public const string UiButtonClickSe =
                "Assets/Addressables/Sound/UI/Button/button_click_001.wav";
            /// <summary>플레이어 카드 드로우 SE</summary>
            public const string SeCardDraw =
                "Assets/Addressables/Sound/SE/Card/se_card_drow_001.ogg";
            /// <summary>타이틀(인트로) BGM. BGMDataSO의 Intro와 동일 경로를 유지한다.</summary>
            public const string BgmIntro =
                "Assets/Addressables/Sound/BGM/bgm_intro_001.ogg";
            /// <summary>캐릭터 보이스 루트. 최종 경로: VoiceRoot + unitTid + "/" + fileName</summary>
            public const string VoiceRoot =
                "Assets/Addressables/Sound/Voice/";
            public const string DefaultHitEffectPrefab =
                "Assets/Addressables/Prefab/Effect/Unit/Sword/effect_sword_slash_hit_001.prefab";
            /// <summary>카드 ResolveSoundPath가 비어 있을 때 사용하는 기본 히트 SE</summary>
            public const string DefaultHitSe =
                "Assets/Addressables/Sound/SE/Weapon/Sword/se_sword_hit_001.mp3";
            public const string UIAtlas = "Assets/Addressables/Atlas/UIAtlas.spriteatlasv2";
            public const string CardIllustAtlas =
                "Assets/Addressables/Atlas/CardIllustAtlas.spriteatlasv2";
            public const string PlayerAnimatorSword =
                "Assets/Addressables/Animator/Player/PlayerAnimator_Sword.controller";
            public const string PlayerAnimatorBow =
                "Assets/Addressables/Animator/Player/PlayerAnimator_Bow.controller";
            public const string PlayerUnitPrefab =
                "Assets/Addressables/Prefab/Unit/Player/PlayerUnit.prefab";
        }
    }
}
