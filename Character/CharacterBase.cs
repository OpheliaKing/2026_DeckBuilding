using UnityEngine;

namespace SHIN
{
    public partial class CharacterBase : MonoBehaviour
    {
        [Header("Combat FX")]
        [SerializeField]
        [Tooltip("피격 이펙트 스폰 위치. 비우면 캐릭터 루트 사용")]
        private Transform _hitEffectPoint;

        [SerializeField]
        [Tooltip("데미지 숫자 스폰 위치. 비우면 HitEffectPoint 사용")]
        private Transform _damageTextPoint;

        [SerializeField]
        [Tooltip("적 HP바 위치. 비우면 DamageTextPoint 사용 (머리 위 권장)")]
        private Transform _healthBarPoint;

        [SerializeField]
        [Tooltip("적 HP바 프리팹 (Heat Health Bar)")]
        private GameObject _enemyHealthBarPrefab;

        /// <summary>피격 이펙트 기준 Transform. 미지정 시 자신.</summary>
        public Transform HitEffectPoint => _hitEffectPoint != null ? _hitEffectPoint : transform;

        /// <summary>데미지 숫자 기준 Transform. 미지정 시 HitEffectPoint.</summary>
        public Transform DamageTextPoint =>
            _damageTextPoint != null ? _damageTextPoint : HitEffectPoint;

        private UnitInfo _unitInfo;
        public UnitInfo UnitInfo => _unitInfo;

        public bool IsDead => _unitInfo == null || _unitInfo.IsDead;
        public bool IsAlive => !IsDead;

        /// <summary>보이스/유닛 공통 TID. UnitData.unitTid.</summary>
        public string UnitTid => _unitInfo?.UnitData?.unitTid;

        private void Awake()
        {
            EnsureClickCollider();
        }

        public void InitCharacter(UnitData unitData)
        {
            _unitInfo = new UnitInfo(unitData);
        }

        public void InitCharacter(UnitInfo unitInfo)
        {
            _unitInfo = unitInfo;
        }

        public int TakeDamage(int damage)
        {
            return TakeDamage(damage, null, triggerReactiveEffects: true);
        }

        /// <summary>
        /// 피해를 적용합니다. BLOCK 흡수 후 HP 감소.
        /// triggerReactiveEffects가 true이면 가시 반사와 공격자 흡혈을 처리합니다.
        /// </summary>
        public int TakeDamage(
            int damage,
            CharacterBase source,
            bool triggerReactiveEffects = true)
        {
            if (_unitInfo == null || IsDead)
                return 0;

            int incoming = Mathf.Max(0, damage);
            int applied = _unitInfo.ApplyDamage(incoming);

            if (applied > 0)
                ShowDamageText(applied);

            RefreshHealthBar();
            RefreshPlayerHudHp();
            if (IsDead)
                SetHealthBarVisible(false);

            if (triggerReactiveEffects && incoming > 0)
                ApplyOnHitReactiveEffects(source, applied);

            return applied;
        }

        private void ApplyOnHitReactiveEffects(CharacterBase source, int appliedHpDamage)
        {
            if (_unitInfo == null)
                return;

            // 가시: 피격 시 공격자에게 고정 피해 (연쇄 반응 없음)
            int thorns = Mathf.FloorToInt(_unitInfo.GetBuffValueSum(BUFF_EFFECT_TYPE.THORNS));
            if (thorns > 0 && source != null && source.IsAlive && source != this)
            {
                source.TakeDamage(thorns, null, triggerReactiveEffects: false);
            }

            // 흡혈: 실제 HP 피해량 기준
            if (source?.UnitInfo == null || appliedHpDamage <= 0)
                return;

            float lifestealPercent = source.UnitInfo.GetBuffValueSum(BUFF_EFFECT_TYPE.LIFESTEAL);
            if (lifestealPercent <= 0f)
                return;

            int healAmount = Mathf.FloorToInt(appliedHpDamage * (lifestealPercent / 100f));
            if (healAmount > 0)
                source.Heal(healAmount);
        }

        /// <summary>
        /// 회복을 적용하고 실제 회복량을 반환합니다. 최대 체력을 넘지 않습니다.
        /// </summary>
        public int Heal(int amount)
        {
            if (_unitInfo == null || IsDead)
                return 0;

            int healed = _unitInfo.ApplyHeal(Mathf.Max(0, amount));
            if (healed > 0)
            {
                RefreshHealthBar();
                RefreshPlayerHudHp();
            }
            return healed;
        }

        private void RefreshPlayerHudHp()
        {
            if (_unitInfo == null || _unitInfo.UnitType != UNIT_TYPE.PLAYER)
                return;

            GameManager.Instance?.InGameManager?.PlayerUI?.RefreshHpUI();
        }

        /// <summary>
        /// PhysicsRaycaster 등 다른 경로에서 직접 호출할 때 사용합니다.
        /// 기본 대상 선택은 InGameManager 카메라 레이캐스트로 처리됩니다.
        /// </summary>
        public void OnClickCharacter()
        {
            var inGameManager = GameManager.Instance?.InGameManager;
            if (inGameManager == null)
                return;

            inGameManager.OnCombatTargetSelected(this);
        }

        /// <summary>
        /// 카메라 클릭 선택을 위해 Collider가 없으면 기본 BoxCollider를 추가합니다.
        /// </summary>
        private void EnsureClickCollider()
        {
            if (GetComponentInChildren<Collider>() != null)
                return;

            if (GetComponentInChildren<Collider2D>() != null)
                return;

            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(1.5f, 2f, 1.5f);
            box.center = new Vector3(0f, 1f, 0f);
        }
    }
}
