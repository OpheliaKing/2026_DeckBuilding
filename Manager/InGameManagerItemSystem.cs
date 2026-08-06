using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class InGameManager
    {
        private const int MaxItemEffectDepth = 3;

        /// <summary>Timing별 등록된 효과 (Owner + State)</summary>
        private readonly Dictionary<ITEM_EFFECT_TIMING, List<ActiveItemEffectEntry>> _itemEffectsByTiming = new();

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
        public void FireItemEffects(ITEM_EFFECT_TIMING timing, CombatEventContext context = null)
        {
            if (timing == ITEM_EFFECT_TIMING.NONE)
                return;

            if (context != null && context.Origin == COMBAT_EVENT_ORIGIN.ITEM_EFFECT)
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

                    var effectContext = context ?? new CombatEventContext();
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

        private bool TryPassItemEffectCondition(ActiveItemEffectState state, CombatEventContext context)
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

        private void ExecuteItemCombatEvents(
            ItemEffectData effectData,
            CombatEventContext context)
        {
            var eventTids = effectData.InGameCombatEvents;
            if (eventTids == null || eventTids.Count == 0)
                return;

            Debug.Log(
                $"[ItemEffect] 발동: {effectData.Tid} / {effectData.EffectTiming} / " +
                $"Owner={GetCombatName(context.Owner)}");

            var eventContext =
                context.CopyWithOrigin(COMBAT_EVENT_ORIGIN.ITEM_EFFECT);
            ExecuteCombatEventTids(eventTids, eventContext, $"ItemEffect:{effectData.Tid}");
        }

        /// <summary>
        /// 피격/체력 변동 후 HEALTH_LOW / HEALTH_HIGH 타이밍을 검사합니다.
        /// </summary>
        private void FireHealthThresholdItemEffects(
            CharacterBase owner,
            COMBAT_EVENT_ORIGIN origin = COMBAT_EVENT_ORIGIN.NONE)
        {
            if (owner == null || owner.IsDead)
                return;

            var ctx = new CombatEventContext
            {
                Owner = owner,
                Source = owner,
                Origin = origin,
            };

            FireItemEffects(ITEM_EFFECT_TIMING.HEALTH_LOW, ctx);
            FireItemEffects(ITEM_EFFECT_TIMING.HEALTH_HIGH, ctx);
        }
    }
}
