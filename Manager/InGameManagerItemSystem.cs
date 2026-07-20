using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class InGameManager
    {
        private const int MaxItemEffectDepth = 3;

        /// <summary>Timing별 등록된 효과 (Owner + State)</summary>
        private readonly Dictionary<ITEM_EFFECT_TIMING, List<ActiveItemEffectEntry>> _itemEffectsByTiming = new();

        private InGameCombatEventSO _inGameCombatEventSO;
        private int _itemEffectDepth;

        private sealed class ActiveItemEffectEntry
        {
            public CharacterBase Owner;
            public ActiveItemEffectState State;
        }

        /// <summary>
        /// 전투 시작 시 모든 유닛의 아이템 효과를 Timing별로 등록합니다.
        /// </summary>
        private async System.Threading.Tasks.Task RegisterAllItemEffectsAsync()
        {
            _itemEffectsByTiming.Clear();
            _itemEffectDepth = 0;
            _inGameCombatEventSO = null;

            var itemEffectDataSO = await GameManager.Instance.GetSOAsync<ItemEffectDataSO>(
                PublicVariable.Address.ItemEffectDataSO);
            if (itemEffectDataSO == null)
            {
                Debug.LogError("[ItemEffect] ItemEffectDataSO 로드 실패");
                return;
            }

            var itemDataSO = await GameManager.Instance.GetSOAsync<ItemDataSO>(
                PublicVariable.Address.ItemDataSO);
            if (itemDataSO == null)
            {
                Debug.LogError("[ItemEffect] ItemDataSO 로드 실패");
                return;
            }

            _inGameCombatEventSO = await GameManager.Instance.GetSOAsync<InGameCombatEventSO>(
                PublicVariable.Address.InGameCombatEventSO);
            if (_inGameCombatEventSO == null)
            {
                Debug.LogError("[ItemEffect] InGameCombatEventSO 로드 실패");
                return;
            }

            RegisterItemEffectsForList(_playerCharacters, itemDataSO, itemEffectDataSO);
            RegisterItemEffectsForList(_enemyCharacters, itemDataSO, itemEffectDataSO);

            Debug.Log($"[ItemEffect] 등록 완료. Timing 수: {_itemEffectsByTiming.Count}");
        }

        private void RegisterItemEffectsForList(
            IReadOnlyList<CharacterBase> characters,
            ItemDataSO itemDataSO,
            ItemEffectDataSO itemEffectDataSO)
        {
            if (characters == null)
                return;

            for (int i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character?.UnitInfo == null)
                    continue;

                character.UnitInfo.SetItemDataSO(itemDataSO);
                character.UnitInfo.SetItemEffectDataSO(itemEffectDataSO);
                character.UnitInfo.RebuildActiveItemEffects();

                var effects = character.UnitInfo.ActiveItemEffects;
                for (int j = 0; j < effects.Count; j++)
                    RegisterItemEffect(character, effects[j]);
            }
        }

        private void RegisterItemEffect(CharacterBase owner, ActiveItemEffectState state)
        {
            if (owner == null || state?.EffectData == null)
                return;

            var timing = state.Timing;
            if (timing == ITEM_EFFECT_TIMING.NONE)
                return;

            if (!_itemEffectsByTiming.TryGetValue(timing, out var list))
            {
                list = new List<ActiveItemEffectEntry>();
                _itemEffectsByTiming[timing] = list;
            }

            list.Add(new ActiveItemEffectEntry
            {
                Owner = owner,
                State = state,
            });
        }

        /// <summary>
        /// 특정 Timing의 아이템 효과를 발동합니다.
        /// context.Owner가 있으면 해당 유닛 효과만, 없으면 등록된 전체 효과를 검사합니다.
        /// </summary>
        public void FireItemEffects(ITEM_EFFECT_TIMING timing, ItemEffectContext context = null)
        {
            if (timing == ITEM_EFFECT_TIMING.NONE)
                return;

            if (context != null && context.FromItemEffect)
                return;

            if (_itemEffectDepth >= MaxItemEffectDepth)
            {
                Debug.LogWarning($"[ItemEffect] 재진입 깊이 초과({MaxItemEffectDepth}): {timing}");
                return;
            }

            if (!_itemEffectsByTiming.TryGetValue(timing, out var entries) || entries.Count == 0)
                return;

            _itemEffectDepth++;
            try
            {
                // 순회 중 등록 변경 대비 복사
                var snapshot = new List<ActiveItemEffectEntry>(entries);
                for (int i = 0; i < snapshot.Count; i++)
                {
                    var entry = snapshot[i];
                    if (entry?.Owner == null || entry.Owner.IsDead || entry.State?.EffectData == null)
                        continue;

                    if (context?.Owner != null && entry.Owner != context.Owner)
                        continue;

                    var effectContext = context ?? new ItemEffectContext();
                    if (effectContext.Owner == null)
                        effectContext.Owner = entry.Owner;
                    if (effectContext.Source == null)
                        effectContext.Source = entry.Owner;

                    if (!TryPassItemEffectCondition(entry.State, effectContext))
                        continue;

                    ExecuteItemCombatEvents(entry.State.EffectData, effectContext);
                }
            }
            finally
            {
                _itemEffectDepth--;
            }
        }

        private bool TryPassItemEffectCondition(ActiveItemEffectState state, ItemEffectContext context)
        {
            var data = state.EffectData;
            var condition = data.EffectCondition;
            int value = data.EffectConditionValue;
            var timing = data.EffectTiming;

            switch (condition)
            {
                case ITEM_EFFECT_CONDITION.NONE:
                    return true;

                case ITEM_EFFECT_CONDITION.COUNT:
                    {
                        int everyN = Mathf.Max(1, value);
                        state.TriggerCounter++;
                        return state.TriggerCounter % everyN == 0;
                    }

                case ITEM_EFFECT_CONDITION.PERCENTAGE:
                    {
                        // HEALTH_* 타이밍: HP 비율 임계값
                        if (timing == ITEM_EFFECT_TIMING.HEALTH_LOW ||
                            timing == ITEM_EFFECT_TIMING.HEALTH_HIGH)
                            return CheckHealthRatioCondition(context.Owner, timing, value);

                        // 그 외 Timing: 발동 확률 (0~100)
                        int chance = Mathf.Clamp(value, 0, 100);
                        return Random.Range(0, 100) < chance;
                    }

                case ITEM_EFFECT_CONDITION.ABSOLUTE:
                    {
                        if (timing == ITEM_EFFECT_TIMING.HEALTH_LOW ||
                            timing == ITEM_EFFECT_TIMING.HEALTH_HIGH)
                            return CheckHealthAbsoluteCondition(context.Owner, timing, value);

                        Debug.LogWarning(
                            $"[ItemEffect] ABSOLUTE는 HEALTH_LOW/HIGH와 함께 사용하세요: {data.Tid}");
                        return false;
                    }

                default:
                    return false;
            }
        }

        private static bool CheckHealthRatioCondition(
            CharacterBase owner,
            ITEM_EFFECT_TIMING timing,
            int percent)
        {
            if (owner?.UnitInfo == null)
                return false;

            int maxHp = owner.UnitInfo.MaxHp;
            if (maxHp <= 0)
                return false;

            float ratio = owner.UnitInfo.CurrentHp / (float)maxHp * 100f;
            if (timing == ITEM_EFFECT_TIMING.HEALTH_LOW)
                return ratio <= percent;

            return ratio >= percent;
        }

        private static bool CheckHealthAbsoluteCondition(
            CharacterBase owner,
            ITEM_EFFECT_TIMING timing,
            int absoluteHp)
        {
            if (owner?.UnitInfo == null)
                return false;

            int hp = owner.UnitInfo.CurrentHp;
            if (timing == ITEM_EFFECT_TIMING.HEALTH_LOW)
                return hp <= absoluteHp;

            return hp >= absoluteHp;
        }

        private void ExecuteItemCombatEvents(ItemEffectData effectData, ItemEffectContext context)
        {
            var eventTids = effectData.InGameCombatEvents;
            if (eventTids == null || eventTids.Count == 0)
                return;

            if (_inGameCombatEventSO == null)
            {
                Debug.LogError("[ItemEffect] InGameCombatEventSO가 없어 이벤트를 실행할 수 없습니다.");
                return;
            }

            Debug.Log(
                $"[ItemEffect] 발동: {effectData.Tid} / {effectData.EffectTiming} / " +
                $"Owner={GetCombatName(context.Owner)}");

            for (int i = 0; i < eventTids.Count; i++)
            {
                var eventTid = eventTids[i];
                if (string.IsNullOrEmpty(eventTid))
                    continue;

                if (!_inGameCombatEventSO.TryGetCombatEvent(eventTid, out var combatEvent) || combatEvent == null)
                {
                    Debug.LogError(
                        $"[ItemEffect] InGameCombatEvent를 찾을 수 없습니다: effect={effectData.Tid} / event={eventTid}");
                    continue;
                }

                if (combatEvent.EventType == IN_GAME_COMBAT_EVENT_TYPE.NONE)
                    continue;

                ExecuteCombatEvent(combatEvent, context);
            }
        }

        /// <summary>
        /// InGameCombatEvent 실행. 구체 효과는 단계적으로 채웁니다.
        /// 아이템에서 파생된 후속 Fire는 FromItemEffect로 막아 연쇄를 제한합니다.
        /// </summary>
        private void ExecuteCombatEvent(InGameCombatEvent combatEvent, ItemEffectContext context)
        {
            if (combatEvent == null || context == null)
                return;

            var targets = ResolveCombatEventTargets(combatEvent.TargetUnit, context);
            if (targets.Count == 0)
            {
                Debug.LogWarning($"[CombatEvent] 대상 없음: {combatEvent.Tid} / {combatEvent.TargetUnit}");
                return;
            }

            switch (combatEvent.EventType)
            {
                case IN_GAME_COMBAT_EVENT_TYPE.HEAL:
                    ApplyCombatEventHeal(targets, Mathf.FloorToInt(combatEvent.Value));
                    break;

                case IN_GAME_COMBAT_EVENT_TYPE.DRAW_CARD:
                    ApplyCombatEventDraw(targets, Mathf.FloorToInt(combatEvent.Value));
                    break;

                case IN_GAME_COMBAT_EVENT_TYPE.ATTACK:
                case IN_GAME_COMBAT_EVENT_TYPE.ATTACK_PROJECTILE:
                case IN_GAME_COMBAT_EVENT_TYPE.BUFF:
                case IN_GAME_COMBAT_EVENT_TYPE.DEBUFF:
                    // 상세 구현 예정. 지금은 로그로 흐름만 확인.
                    Debug.Log(
                        $"[CombatEvent] {combatEvent.EventType} / value={combatEvent.Value} / " +
                        $"targets={targets.Count} (구현 예정)");
                    break;

                default:
                    break;
            }
        }

        private List<CharacterBase> ResolveCombatEventTargets(
            IN_GAME_COMBAT_EVENT_TARGET_UNIT targetType,
            ItemEffectContext context)
        {
            var result = new List<CharacterBase>();
            var owner = context.Owner;
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
                        result.Add(context.Target);
                    else if (owner.IsAlive)
                        result.Add(owner);
                    break;

                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.TEAM_ALL:
                    AddAliveFromList(ownerIsPlayer ? _playerCharacters : _enemyCharacters, result);
                    break;

                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.ENEMY:
                    if (context.Target != null &&
                        context.Target.IsAlive &&
                        IsPlayerCharacter(context.Target) != ownerIsPlayer)
                        result.Add(context.Target);
                    break;

                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.ENEMY_ALL:
                    AddAliveFromList(ownerIsPlayer ? _enemyCharacters : _playerCharacters, result);
                    break;

                case IN_GAME_COMBAT_EVENT_TARGET_UNIT.ALL:
                    AddAliveFromList(_playerCharacters, result);
                    AddAliveFromList(_enemyCharacters, result);
                    break;
            }

            return result;
        }

        private static void AddAliveFromList(IReadOnlyList<CharacterBase> source, List<CharacterBase> dest)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                var c = source[i];
                if (c != null && c.IsAlive)
                    dest.Add(c);
            }
        }

        private void ApplyCombatEventHeal(List<CharacterBase> targets, int amount)
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
                    FireHealthThresholdItemEffects(target);
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
                    PlayerUI.RefreshHand(unitInfo.Hand);

                Debug.Log($"[CombatEvent][DRAW] {GetCombatName(targets[i])} +{drawn.Count}장");
            }
        }

        /// <summary>
        /// 피격/체력 변동 후 HEALTH_LOW / HEALTH_HIGH 타이밍을 검사합니다.
        /// </summary>
        private void FireHealthThresholdItemEffects(CharacterBase owner)
        {
            if (owner == null || owner.IsDead)
                return;

            var ctx = new ItemEffectContext
            {
                Owner = owner,
                Source = owner,
            };

            FireItemEffects(ITEM_EFFECT_TIMING.HEALTH_LOW, ctx);
            FireItemEffects(ITEM_EFFECT_TIMING.HEALTH_HIGH, ctx);
        }
    }
}
