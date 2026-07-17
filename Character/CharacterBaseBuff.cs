using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class CharacterBase
    {
        public void ApplyBuffFromCard(CardData card)
        {
            if (_unitInfo == null || card == null || card.CardType != CARD_TYPE.BUFF)
                return;

            var buffData = card.BuffData;
            if (buffData == null || buffData.BuffEffectType == CARD_BUFF_EFFECT_TYPE.NONE)
            {
                Debug.LogWarning($"[Buff] 버프 데이터 없음: {card.Name}");
                return;
            }

            int oldSpeed = _unitInfo.CurrentSpeed;
            _unitInfo.AddBuff(buffData.BuffEffectType, buffData.BuffEffectValue, buffData.BuffEffectDuration, card.Tid);
            int newSpeed = _unitInfo.CurrentSpeed;

            Debug.Log(
                $"[Buff] {GetCharacterDisplayName()} ← {card.Name} / " +
                $"{buffData.BuffEffectType} +{buffData.BuffEffectValue} / {buffData.BuffEffectDuration}턴");

            if (oldSpeed != newSpeed)
                GameManager.Instance?.InGameManager?.RecalculateAVOnSpeedChanged(this, oldSpeed, newSpeed);
        }

        public void TickBuffsOnTurnStart()
        {
            if (_unitInfo == null)
                return;

            int oldSpeed = _unitInfo.CurrentSpeed;
            _unitInfo.TickBuffsOnTurnStart();
            int newSpeed = _unitInfo.CurrentSpeed;

            if (oldSpeed != newSpeed)
                GameManager.Instance?.InGameManager?.RecalculateAVOnSpeedChanged(this, oldSpeed, newSpeed);
        }

        public IReadOnlyList<ActiveBuff> ActiveBuffs => _unitInfo?.ActiveBuffs;

        private static string GetCharacterDisplayName(CharacterBase character)
        {
            if (character?.UnitInfo?.UnitData == null)
                return character != null ? character.name : "Unknown";

            return string.IsNullOrEmpty(character.UnitInfo.UnitData.unitName)
                ? character.UnitInfo.UnitData.unitTid
                : character.UnitInfo.UnitData.unitName;
        }

        private string GetCharacterDisplayName() => GetCharacterDisplayName(this);
    }

    [System.Serializable]
    public class ActiveBuff
    {
        public CARD_BUFF_EFFECT_TYPE EffectType;
        public float Value;
        public int RemainingTurns;
        public string SourceCardTid;
    }
}
