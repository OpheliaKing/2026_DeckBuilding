using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class CharacterBase
    {
        public void ApplyBuffData(BuffData buffData, string sourceTid = null)
        {
            if (_unitInfo == null || buffData == null || !buffData.IsValid)
                return;

            int oldSpeed = _unitInfo.CurrentSpeed;
            _unitInfo.AddBuff(buffData, sourceTid);
            int newSpeed = _unitInfo.CurrentSpeed;

            Debug.Log(
                $"[Buff] {GetCharacterDisplayName()} ← {sourceTid ?? buffData.Tid} / " +
                $"{buffData.EffectType} +{buffData.Value} / {buffData.Duration}턴");

            if (oldSpeed != newSpeed)
                GameManager.Instance?.InGameManager?.RecalculateAVOnSpeedChanged(this, oldSpeed, newSpeed);
        }

        public void TickBuffsOnTurnStart()
        {
            if (_unitInfo == null)
                return;

            // 재생 → 독 → 지속시간 감소 순
            int regen = Mathf.FloorToInt(_unitInfo.GetBuffValueSum(BUFF_EFFECT_TYPE.REGEN));
            if (regen > 0 && IsAlive)
            {
                int healed = Heal(regen);
                if (healed > 0)
                    Debug.Log($"[Buff][REGEN] {GetCharacterDisplayName()} +{healed}");
            }

            int poison = Mathf.FloorToInt(_unitInfo.GetBuffValueSum(BUFF_EFFECT_TYPE.POISON));
            if (poison > 0 && IsAlive)
            {
                int applied = TakeDamage(poison, null, triggerReactiveEffects: false);
                if (applied > 0)
                    Debug.Log($"[Buff][POISON] {GetCharacterDisplayName()} -{applied}");
            }

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
        public BUFF_EFFECT_TYPE EffectType;
        public float Value;
        public int RemainingTurns;
        public string SourceCardTid;
    }
}
