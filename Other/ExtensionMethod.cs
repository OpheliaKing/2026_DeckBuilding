using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public static class ExtensionMethod
    {

        public static string GetSpriteName(this STAGE_TYPE stageType)
        {
            switch (stageType)
            {
                case STAGE_TYPE.NONE:
                    return "None";
                case STAGE_TYPE.BATTLE_NORMAL:
                    return "Pictoicon_Skull";
                case STAGE_TYPE.BATTLE_ELITE:
                    return "Pictoicon_Assassin";
                case STAGE_TYPE.BATTLE_BOSS:
                    return "Pictoicon_Boss";
                case STAGE_TYPE.SHOP:
                    return "Pictoicon_Store";
                case STAGE_TYPE.EVENT:
                    return "Pictoicon_Chest_0";
                default:
                    return "None";
            }
        }
    }

}

