using System;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 인게임 전투에서 발생할 수 있는 이벤트.
    /// ItemEffectData에서 조건을 관리하고, InGameCombatEvent에서 결과를 관리합니다.
    /// </summary>
    [Serializable]
    public class InGameCombatEvent
    {
        [SerializeField]
        private string _tid;
        public string Tid => _tid;

        [SerializeField]
        private IN_GAME_COMBAT_EVENT_TYPE _eventType;
        public IN_GAME_COMBAT_EVENT_TYPE EventType => _eventType;

        [SerializeField]
        private IN_GAME_COMBAT_EVENT_TARGET_UNIT _targetUnit;
        public IN_GAME_COMBAT_EVENT_TARGET_UNIT TargetUnit => _targetUnit;

        [SerializeField]
        private float _value;
        public float Value => _value;
        [SerializeField]
        private string _customString;
        public string CustomString => _customString;
    }

    public enum IN_GAME_COMBAT_EVENT_TYPE
    {
        NONE,
        ATTACK,
        ATTACK_PROJECTILE,
        HEAL,
        DRAW_CARD,
        BUFF,
        DEBUFF,
    }

    public enum IN_GAME_COMBAT_EVENT_TARGET_UNIT
    {
        NONE,
        SELF,
        TEAM,
        TEAM_ALL,
        ENEMY,
        ENEMY_ALL,
        ALL,
    }
}
