using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class InGameManager
    {
        private const int MaxCombatEventDepth = 4;

        private InGameCombatEventSO _inGameCombatEventSO;
        private int _combatEventDepth;

        private async System.Threading.Tasks.Task LoadCombatEventDataAsync()
        {
            if (_inGameCombatEventSO != null)
                return;

            _inGameCombatEventSO = await GameManager.Instance.GetSOAsync<InGameCombatEventSO>(
                PublicVariable.Address.InGameCombatEventSO);

            if (_inGameCombatEventSO == null)
                Debug.LogError("[CombatEvent] InGameCombatEventSO 로드 실패");
        }

        private void ExecuteCombatEventTids(
            IReadOnlyList<string> eventTids,
            CombatEventContext context,
            string sourceLabel)
        {
            if (eventTids == null || eventTids.Count == 0 || context == null)
                return;

            if (_inGameCombatEventSO == null)
            {
                Debug.LogError($"[CombatEvent] SO가 없어 실행할 수 없습니다: {sourceLabel}");
                return;
            }

            if (_combatEventDepth >= MaxCombatEventDepth)
            {
                Debug.LogWarning(
                    $"[CombatEvent] 재진입 깊이 초과({MaxCombatEventDepth}): {sourceLabel}");
                return;
            }

            _combatEventDepth++;
            try
            {
                for (int i = 0; i < eventTids.Count; i++)
                {
                    string eventTid = eventTids[i];
                    if (string.IsNullOrEmpty(eventTid))
                        continue;

                    TryExecuteCombatEvent(
                        eventTid,
                        context,
                        sourceLabel,
                        IN_GAME_COMBAT_EVENT_TARGET_UNIT.NONE,
                        1f);
                }
            }
            finally
            {
                _combatEventDepth--;
            }
        }

        private void ExecuteCardAttackEvent(
            CardAttackEventData eventData,
            CombatEventContext context,
            string sourceLabel)
        {
            if (eventData == null || context == null || string.IsNullOrEmpty(eventData.EventTid))
                return;

            if (_inGameCombatEventSO == null)
            {
                Debug.LogError($"[CombatEvent] SO가 없어 실행할 수 없습니다: {sourceLabel}");
                return;
            }

            if (_combatEventDepth >= MaxCombatEventDepth)
            {
                Debug.LogWarning(
                    $"[CombatEvent] 재진입 깊이 초과({MaxCombatEventDepth}): {sourceLabel}");
                return;
            }

            _combatEventDepth++;
            try
            {
                TryExecuteCombatEvent(
                    eventData.EventTid,
                    context,
                    sourceLabel,
                    eventData.TargetOverride,
                    eventData.ValueMultiplier);
            }
            finally
            {
                _combatEventDepth--;
            }
        }

        private void TryExecuteCombatEvent(
            string eventTid,
            CombatEventContext context,
            string sourceLabel,
            IN_GAME_COMBAT_EVENT_TARGET_UNIT targetOverride,
            float valueMultiplier)
        {
            if (!_inGameCombatEventSO.TryGetCombatEvent(eventTid, out var combatEvent) ||
                combatEvent == null)
            {
                Debug.LogError(
                    $"[CombatEvent] 이벤트를 찾을 수 없습니다: source={sourceLabel} / event={eventTid}");
                return;
            }

            if (combatEvent.EventType == IN_GAME_COMBAT_EVENT_TYPE.NONE)
                return;

            var targetType = targetOverride == IN_GAME_COMBAT_EVENT_TARGET_UNIT.NONE
                ? combatEvent.TargetUnit
                : targetOverride;

            ExecuteCombatEvent(
                combatEvent,
                context,
                targetType,
                Mathf.Max(0f, valueMultiplier));
        }

        private void ExecuteCombatEvent(
            InGameCombatEvent combatEvent,
            CombatEventContext context,
            IN_GAME_COMBAT_EVENT_TARGET_UNIT targetType,
            float valueMultiplier)
        {
            if (combatEvent == null || context == null)
                return;

            var targets = ResolveCombatEventTargets(targetType, context);
            if (targets.Count == 0)
            {
                Debug.LogWarning(
                    $"[CombatEvent] 대상 없음: {combatEvent.Tid} / {targetType}");
                return;
            }

            float effectiveValue = combatEvent.Value * valueMultiplier;
            switch (combatEvent.EventType)
            {
                case IN_GAME_COMBAT_EVENT_TYPE.HEAL:
                    ApplyCombatEventHeal(
                        targets,
                        Mathf.FloorToInt(effectiveValue),
                        context.Origin);
                    break;

                case IN_GAME_COMBAT_EVENT_TYPE.DRAW_CARD:
                    ApplyCombatEventDraw(targets, Mathf.FloorToInt(effectiveValue));
                    break;

                case IN_GAME_COMBAT_EVENT_TYPE.BUFF:
                case IN_GAME_COMBAT_EVENT_TYPE.DEBUFF:
                    ApplyCombatEventBuff(targets, combatEvent, effectiveValue);
                    break;

                case IN_GAME_COMBAT_EVENT_TYPE.ATTACK:
                case IN_GAME_COMBAT_EVENT_TYPE.ATTACK_PROJECTILE:
                    Debug.Log(
                        $"[CombatEvent] {combatEvent.EventType} / value={effectiveValue} / " +
                        $"targets={targets.Count} (구현 예정)");
                    break;
            }
        }

        private List<CharacterBase> ResolveCombatEventTargets(
            IN_GAME_COMBAT_EVENT_TARGET_UNIT targetType,
            CombatEventContext context)
        {
            var result = new List<CharacterBase>();
            var owner = context.Owner ?? context.Source;
            if (owner == null)
                return result;

            bool ownerIsPlayer = IsPlayerCharacter(owner);

            switch (targetType)
            {
                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.SELF:
                    if (owner.IsAlive)
                        result.Add(owner);
                    break;

                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.TEAM:
                    if (context.Target != null &&
                        context.Target.IsAlive &&
                        IsPlayerCharacter(context.Target) == ownerIsPlayer)
                    {
                        result.Add(context.Target);
                    }
                    else if (owner.IsAlive)
                    {
                        result.Add(owner);
                    }
                    break;

                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.TEAM_ALL:
                    AddAliveFromList(
                        ownerIsPlayer ? _playerCharacters : _enemyCharacters,
                        result);
                    break;

                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.ENEMY:
                    if (context.Target != null &&
                        context.Target.IsAlive &&
                        IsPlayerCharacter(context.Target) != ownerIsPlayer)
                    {
                        result.Add(context.Target);
                    }
                    break;

                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.ENEMY_ALL:
                    AddAliveFromList(
                        ownerIsPlayer ? _enemyCharacters : _playerCharacters,
                        result);
                    break;

                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.ALL:
                    AddAliveFromList(_playerCharacters, result);
                    AddAliveFromList(_enemyCharacters, result);
                    break;

                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.AFFECTED_TARGETS:
                    AddAliveFromList(context.AffectedTargets, result);
                    break;
            }

            return result;
        }

        private static void AddAliveFromList(
            IReadOnlyList<CharacterBase> source,
            List<CharacterBase> destination)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                var character = source[i];
                if (character != null && character.IsAlive && !destination.Contains(character))
                    destination.Add(character);
            }
        }

        private void ApplyCombatEventHeal(
            List<CharacterBase> targets,
            int amount,
            COMBAT_EVENT_ORIGIN origin)
        {
            if (amount <= 0)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || target.IsDead)
                    continue;

                int healed = target.Heal(amount);
                Debug.Log(
                    $"[CombatEvent][HEAL] {GetCombatName(target)} +{healed} " +
                    $"(요청:{amount}) / HP:{target.UnitInfo.CurrentHp}/{target.UnitInfo.MaxHp}");

                if (healed > 0)
                    FireHealthThresholdItemEffects(target, origin);
            }
        }

        private void ApplyCombatEventDraw(List<CharacterBase> targets, int count)
        {
            if (count <= 0)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                var unitInfo = targets[i]?.UnitInfo;
                if (unitInfo == null)
                    continue;

                var drawn = unitInfo.DrawCards(count);
                if (IsPlayerCharacter(targets[i]) && PlayerUI != null)
                {
                    for (int d = 0; d < drawn.Count; d++)
                        GameManager.Instance?.SoundManager?.PlaySe(PublicVariable.Address.SeCardDraw);

                    PlayerUI.RefreshHand(unitInfo.Hand);
                }

                Debug.Log(
                    $"[CombatEvent][DRAW] {GetCombatName(targets[i])} +{drawn.Count}장");
            }
        }

        private void ApplyCombatEventBuff(
            List<CharacterBase> targets,
            InGameCombatEvent combatEvent,
            float effectiveValue)
        {
            if (combatEvent.BuffEffectType == CARD_BUFF_EFFECT_TYPE.NONE ||
                combatEvent.Duration <= 0)
            {
                Debug.LogWarning(
                    $"[CombatEvent] 버프 데이터 없음: {combatEvent.Tid} / " +
                    $"{combatEvent.BuffEffectType} / {combatEvent.Duration}턴");
                return;
            }

            bool isDebuff = combatEvent.EventType == IN_GAME_COMBAT_EVENT_TYPE.DEBUFF;
            float value = isDebuff
                ? -Mathf.Abs(effectiveValue)
                : Mathf.Abs(effectiveValue);

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target?.UnitInfo == null || target.IsDead)
                    continue;

                int oldSpeed = target.UnitInfo.CurrentSpeed;
                target.UnitInfo.AddBuff(
                    combatEvent.BuffEffectType,
                    value,
                    combatEvent.Duration,
                    combatEvent.Tid);
                int newSpeed = target.UnitInfo.CurrentSpeed;

                if (oldSpeed != newSpeed)
                    RecalculateAVOnSpeedChanged(target, oldSpeed, newSpeed);

                Debug.Log(
                    $"[CombatEvent][{combatEvent.EventType}] {GetCombatName(target)} / " +
                    $"{combatEvent.BuffEffectType} {value:+0.##;-0.##;0} / " +
                    $"{combatEvent.Duration}턴");
            }
        }
    }
}
