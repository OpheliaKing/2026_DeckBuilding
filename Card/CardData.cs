using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SHIN
{
    [Serializable]
    public class CardData : ISerializationCallbackReceiver
    {
        [SerializeField]
        private string _tid;
        public string Tid => _tid;

        [SerializeField]
        private string _name;
        public string Name => _name;

        [SerializeField]
        private string _description;
        public string Description => _description;

        [SerializeField]
        private CHARACTER_EQUIP_TYPE _cardWeaponType;
        public CHARACTER_EQUIP_TYPE CardWeaponType => _cardWeaponType;

        [SerializeField]
        private int _cost;
        public int Cost => _cost;

        /// <summary>
        /// CardIllustAtlas 스프라이트 이름. 비우면 플레이스홀더.
        /// 예: card_illust_pink_sword_slash / 권장 해상도 1024×1024.
        /// </summary>
        [SerializeField]
        private string _illustrationPath;
        public string IllustrationPath => _illustrationPath;

        [SerializeField]
        private CARD_TYPE _cardType;
        public CARD_TYPE CardType => _cardType;

        [SerializeField]
        private ITEM_GRADE _cardGrade;
        public ITEM_GRADE CardGrade => _cardGrade;

        [SerializeField]
        private string _animationName;
        public string AnimationName => _animationName;

        /// <summary>
        /// 스킬 Virtual Camera Addressables 경로.
        /// CombatAnimStateBehaviour SkillCameraCue(Play)에서 사용한다. 비면 카메라 없음.
        /// 예: Assets/Addressables/Prefab/Camera/Skill/BuffCamera.prefab
        /// </summary>
        [Tooltip("스킬 Virtual Camera Addressables 경로. 비면 카메라 없음.")]
        [SerializeField]
        private string _skillCameraPath;
        public string SkillCameraPath => _skillCameraPath;

        #region RESOLVE

        /// <summary>
        /// 카드 효과가 적용되는 대상에게 스폰할 Addressables 이펙트 경로.
        /// 공격 피격 대상 / 버프·디버프 적용 대상 공용.
        /// </summary>
        [FormerlySerializedAs("_hitEffectPath")]
        [SerializeField]
        private string _resolveEffectPath;
        public string ResolveEffectPath => _resolveEffectPath;

        /// <summary>
        /// 카드 효과 적용 시 재생할 SE 경로.
        /// 공격은 비우면 DefaultHitSe 사용, 버프·디버프는 설정된 경우만 재생.
        /// </summary>
        [FormerlySerializedAs("_hitSoundPath")]
        [SerializeField]
        private string _resolveSoundPath;
        public string ResolveSoundPath => _resolveSoundPath;

        #endregion

        #region ATTACK

        [SerializeField]
        private float _attackMultiplier;
        public float AttackMultiplier => _attackMultiplier;

        [SerializeField]
        private bool _isRangeAttack;
        public bool IsRangeAttack => _isRangeAttack;

        /// <summary>
        /// 공격 애니 파티클 오버라이드.
        /// 비어 있지 않으면 CombatAnimStateBehaviour ParticleCue.ParticleAddress 대신 사용한다.
        /// </summary>
        [SerializeField]
        private string _attackParticlePath;
        public string AttackParticlePath => _attackParticlePath;

        /// <summary>
        /// 공격 과정에서 실행할 전투 이벤트 목록.
        /// 이벤트별 발동 시점, 대상, 확률, 수치 배율과 반복 여부를 설정합니다.
        /// </summary>
        [SerializeField]
        private List<CardAttackEventData> _attackEvents = new();
        public IReadOnlyList<CardAttackEventData> AttackEvents => _attackEvents;

        /// <summary>
        /// 기존 List&lt;string&gt; _attackEvent 데이터의 자동 마이그레이션용 필드.
        /// </summary>
        [FormerlySerializedAs("_attackEvent")]
        [SerializeField, HideInInspector]
        private List<string> _legacyAttackEvents;

        #endregion

        #region BUFF

        /// <summary>
        /// 카드가 적용할 버프·디버프 목록. 항목마다 대상과 BuffDataSO tid를 지정합니다.
        /// </summary>
        [SerializeField]
        private List<CardBuffEntry> _buffEntries = new();
        public IReadOnlyList<CardBuffEntry> BuffEntries => _buffEntries;

        [FormerlySerializedAs("_buffTargetType")]
        [SerializeField, HideInInspector]
        private CARD_BUFF_TARGET_TYPE _legacyBuffTargetType;

        [FormerlySerializedAs("_buffData")]
        [SerializeField, HideInInspector]
        private BuffData _legacyBuffData;

        /// <summary>
        /// 클릭으로 대상 선택이 필요한 버프가 있는지.
        /// </summary>
        public bool NeedsBuffTargetSelection
        {
            get
            {
                if (_buffEntries == null)
                    return false;

                for (int i = 0; i < _buffEntries.Count; i++)
                {
                    var entry = _buffEntries[i];
                    if (entry == null)
                        continue;

                    switch (entry.TargetType)
                    {
                        case CARD_BUFF_TARGET_TYPE.TEAM:
                        case CARD_BUFF_TARGET_TYPE.ALL:
                        case CARD_BUFF_TARGET_TYPE.ENEMY:
                        case CARD_BUFF_TARGET_TYPE.ENEMY_ALL:
                            return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 아군 대상 선택(팀 카메라)이 필요한지.
        /// </summary>
        public bool NeedsAllyBuffTargetSelection
        {
            get
            {
                if (_buffEntries == null)
                    return false;

                for (int i = 0; i < _buffEntries.Count; i++)
                {
                    var entry = _buffEntries[i];
                    if (entry == null)
                        continue;

                    if (entry.TargetType == CARD_BUFF_TARGET_TYPE.TEAM ||
                        entry.TargetType == CARD_BUFF_TARGET_TYPE.ALL)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 적 대상 선택이 필요한지.
        /// </summary>
        public bool NeedsEnemyBuffTargetSelection
        {
            get
            {
                if (_buffEntries == null)
                    return false;

                for (int i = 0; i < _buffEntries.Count; i++)
                {
                    var entry = _buffEntries[i];
                    if (entry == null)
                        continue;

                    if (entry.TargetType == CARD_BUFF_TARGET_TYPE.ENEMY ||
                        entry.TargetType == CARD_BUFF_TARGET_TYPE.ENEMY_ALL)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        #endregion

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _attackEvents ??= new List<CardAttackEventData>();
            _buffEntries ??= new List<CardBuffEntry>();

            if (_legacyAttackEvents != null && _legacyAttackEvents.Count > 0)
            {
                for (int i = 0; i < _legacyAttackEvents.Count; i++)
                {
                    string eventTid = _legacyAttackEvents[i];
                    if (!string.IsNullOrEmpty(eventTid))
                        _attackEvents.Add(new CardAttackEventData(eventTid));
                }

                _legacyAttackEvents.Clear();
            }

            // 구 CardBuffData 인라인 수치는 tid 변환이 불가합니다.
            // 기존 카드는 CardDataSO에서 BuffEntries + BuffDataSO tid로 재설정합니다.
            _ = _legacyBuffTargetType;
            _ = _legacyBuffData;
        }
    }

    [Serializable]
    public class CardBuffEntry
    {
        [Tooltip("이 버프를 적용할 카드 대상 범위")]
        [SerializeField]
        private CARD_BUFF_TARGET_TYPE _targetType = CARD_BUFF_TARGET_TYPE.SELF;
        public CARD_BUFF_TARGET_TYPE TargetType => _targetType;

        [Tooltip("BuffDataSO에서 조회할 버프 tid")]
        [SerializeField]
        private string _buffTid;
        public string BuffTid => _buffTid;

        public bool IsValid =>
            _targetType != CARD_BUFF_TARGET_TYPE.NONE &&
            !string.IsNullOrEmpty(_buffTid);

        public CardBuffEntry()
        {
        }

        public CardBuffEntry(CARD_BUFF_TARGET_TYPE targetType, string buffTid)
        {
            _targetType = targetType;
            _buffTid = buffTid;
        }
    }

    [Serializable]
    public class CardAttackEventData
    {
        [Tooltip("실행할 InGameCombatEventSO 이벤트 TID")]
        [SerializeField]
        private string _eventTid;
        public string EventTid => _eventTid;

        [Tooltip("NONE이면 InGameCombatEvent의 TargetUnit을 사용합니다.")]
        [SerializeField]
        private IN_GAME_COMBAT_EVENT_TARGET_UNIT _targetOverride;
        public IN_GAME_COMBAT_EVENT_TARGET_UNIT TargetOverride => _targetOverride;

        [Tooltip("이 이벤트가 실행될 공격 시점")]
        [SerializeField]
        private CARD_ATTACK_EVENT_TIMING _timing = CARD_ATTACK_EVENT_TIMING.FINAL_HIT;
        public CARD_ATTACK_EVENT_TIMING Timing => _timing;

        [Tooltip("발동 확률. 0은 발동하지 않고 1은 항상 발동합니다.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float _triggerChance = 1f;
        public float TriggerChance => _triggerChance;

        [Tooltip("InGameCombatEvent의 Value에 곱할 카드 전용 배율")]
        [Min(0f)]
        [SerializeField]
        private float _valueMultiplier = 1f;
        public float ValueMultiplier => _valueMultiplier;

        [Tooltip("같은 공격 중 동일 이벤트가 여러 판정에서 반복 실행될 수 있는지 여부")]
        [SerializeField]
        private bool _allowRepeatedExecution;
        public bool AllowRepeatedExecution => _allowRepeatedExecution;

        public CardAttackEventData(string eventTid)
        {
            _eventTid = eventTid;
            _timing = CARD_ATTACK_EVENT_TIMING.FINAL_HIT;
            _triggerChance = 1f;
            _valueMultiplier = 1f;
        }
    }

    public enum CARD_ATTACK_EVENT_TIMING
    {
        /// <summary>기존 AttackEvent와 동일하게 마지막 Hit 후 한 번 실행.</summary>
        FINAL_HIT = 0,
        ATTACK_START,
        EACH_HIT,
        ON_KILL,
    }

    public enum CARD_TYPE
    {
        NONE = 0,
        ATTACK = 1,
        DEFENSE = 2,
        /// <summary>아군 버프. 선택 시 팀 카메라 사용.</summary>
        BUFF = 3,
        /// <summary>적 디버프. 적 대상 선택(전투 카메라) 사용.</summary>
        DEBUFF = 4,
        SPECIAL = 5,
    }

    public enum CARD_BUFF_TARGET_TYPE
    {
        NONE,
        SELF,
        TEAM,
        ALL,
        ENEMY,
        ENEMY_ALL,
    }

    public static class CardTypeUtility
    {
        /// <summary>버프·디버프 카드 (BuffEntries 사용).</summary>
        public static bool UsesBuffEntries(CARD_TYPE cardType)
        {
            return cardType == CARD_TYPE.BUFF || cardType == CARD_TYPE.DEBUFF;
        }

        public static bool UsesAttackFields(CARD_TYPE cardType)
        {
            return cardType == CARD_TYPE.ATTACK;
        }

        public static bool IsBuff(CARD_TYPE cardType) => cardType == CARD_TYPE.BUFF;

        public static bool IsDebuff(CARD_TYPE cardType) => cardType == CARD_TYPE.DEBUFF;

        /// <summary>
        /// 아군 팀 타겟 카메라 사용 여부.
        /// CardType.BUFF면 항상 켠다 (엔트리 TargetType과 무관).
        /// </summary>
        public static bool ShouldUseAllyTargetCamera(CARD_TYPE cardType)
        {
            return cardType == CARD_TYPE.BUFF;
        }

        /// <summary>레거시 호환. DEBUFF를 BUFF로 합치지 않고 그대로 반환.</summary>
        public static CARD_TYPE Normalize(CARD_TYPE cardType) => cardType;
    }
}
