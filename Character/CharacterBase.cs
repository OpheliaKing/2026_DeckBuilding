using UnityEngine;

namespace SHIN
{
    public class CharacterBase : MonoBehaviour
    {
        private UnitInfo _unitInfo;
        public UnitInfo UnitInfo => _unitInfo;

        public bool IsDead => _unitInfo == null || _unitInfo.IsDead;
        public bool IsAlive => !IsDead;

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
            if (_unitInfo == null || IsDead)
                return 0;

            return _unitInfo.ApplyDamage(damage);
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
