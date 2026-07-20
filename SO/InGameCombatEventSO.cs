using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "InGameCombatEventSO", menuName = "SHIN/InGame Combat Event SO")]
    public class InGameCombatEventSO : ScriptableObject
    {
        [SerializeField] private List<InGameCombatEvent> _combatEvents = new();

        public IReadOnlyList<InGameCombatEvent> CombatEvents => _combatEvents;
        public int Count => _combatEvents.Count;

        public InGameCombatEvent GetCombatEvent(int index)
        {
            if (index < 0 || index >= _combatEvents.Count)
            {
                Debug.LogError($"[InGameCombatEventSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _combatEvents[index];
        }

        public InGameCombatEvent GetCombatEvent(string eventTid)
        {
            if (string.IsNullOrEmpty(eventTid))
            {
                Debug.LogError("[InGameCombatEventSO] eventTid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _combatEvents.Count; i++)
            {
                if (_combatEvents[i].Tid == eventTid)
                    return _combatEvents[i];
            }

            Debug.LogError($"[InGameCombatEventSO] eventTid를 찾을 수 없습니다: {eventTid}");
            return null;
        }

        public bool TryGetCombatEvent(string eventTid, out InGameCombatEvent combatEvent)
        {
            combatEvent = null;

            if (string.IsNullOrEmpty(eventTid))
                return false;

            for (int i = 0; i < _combatEvents.Count; i++)
            {
                if (_combatEvents[i].Tid == eventTid)
                {
                    combatEvent = _combatEvents[i];
                    return true;
                }
            }

            return false;
        }
    }
}
