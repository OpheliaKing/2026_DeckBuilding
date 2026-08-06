using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 몬스터(NPC) 피격 시 월드 데미지 숫자 표시.
    /// </summary>
    public partial class CharacterBase
    {
        private const float DamageTextDuration = 0.85f;

        private static readonly Queue<DamageTextPopup> DamageTextPool = new();
        private static readonly HashSet<DamageTextPopup> ActiveDamageTexts = new();
        private static Transform _damageTextPoolRoot;

        /// <summary>
        /// 몬스터가 실제 HP 피해를 입으면 데미지 폰트를 띄웁니다.
        /// 위치는 유닛 프리팹의 DamageTextPoint(없으면 HitEffectPoint)를 사용합니다.
        /// </summary>
        public void ShowDamageText(int damage)
        {
            if (damage <= 0)
                return;

            if (_unitInfo == null || _unitInfo.UnitType != UNIT_TYPE.NPC)
                return;

            if (this == null || DamageTextPoint == null)
                return;

            var popup = RentDamageTextPopup();
            if (popup == null)
                return;

            ActiveDamageTexts.Add(popup);
            popup.Play(DamageTextPoint.position, damage, DamageTextDuration, ReturnDamageTextPopup);
        }

        private static DamageTextPopup RentDamageTextPopup()
        {
            EnsureDamageTextPoolRoot();
            if (_damageTextPoolRoot == null)
                return null;

            while (DamageTextPool.Count > 0)
            {
                var pooled = DamageTextPool.Dequeue();
                // Domain reload 없이 플레이모드를 반복하면 파괴된 참조가 남을 수 있음
                if (pooled == null)
                    continue;

                return pooled;
            }

            var go = new GameObject("DamageTextPopup");
            go.transform.SetParent(_damageTextPoolRoot, false);

            // TextMeshPro는 활성 상태에서 AddComponent해야 Awake로 Renderer가 생성됨
            var popup = go.AddComponent<DamageTextPopup>();
            popup.EnsureInitialized();
            go.SetActive(false);
            return popup;
        }

        private static void ReturnDamageTextPopup(DamageTextPopup popup)
        {
            ActiveDamageTexts.Remove(popup);

            if (popup == null)
                return;

            if (_damageTextPoolRoot == null)
            {
                Object.Destroy(popup.gameObject);
                return;
            }

            popup.transform.SetParent(_damageTextPoolRoot, false);
            popup.gameObject.SetActive(false);
            DamageTextPool.Enqueue(popup);
        }

        private static void EnsureDamageTextPoolRoot()
        {
            // 스테이지 교체로 루트가 파괴됐으면 풀 전체 리셋
            if (_damageTextPoolRoot == null)
            {
                ClearStaleDamageTextRefs();

                Transform parent = GameManager.Instance?.InGameManager != null
                    ? GameManager.Instance.InGameManager.transform
                    : null;

                if (parent == null)
                    return;

                var root = new GameObject("DamageTextPool");
                root.transform.SetParent(parent, false);
                // 루트는 활성 유지: 비활성 부모 아래에서는 Update/월드 표시가 막힘
                _damageTextPoolRoot = root.transform;
                return;
            }

            // 부모(InGameManager)가 바뀌었으면 다시 붙임
            var inGame = GameManager.Instance?.InGameManager;
            if (inGame != null && _damageTextPoolRoot.parent != inGame.transform)
                _damageTextPoolRoot.SetParent(inGame.transform, false);
        }

        private static void ClearStaleDamageTextRefs()
        {
            DamageTextPool.Clear();
            ActiveDamageTexts.Clear();
        }

        /// <summary>
        /// 스테이지 종료 등으로 인게임 매니저가 정리될 때 호출합니다.
        /// </summary>
        public static void ReleaseDamageTextPool()
        {
            foreach (var active in ActiveDamageTexts)
            {
                if (active != null)
                    Object.Destroy(active.gameObject);
            }

            ActiveDamageTexts.Clear();

            while (DamageTextPool.Count > 0)
            {
                var popup = DamageTextPool.Dequeue();
                if (popup != null)
                    Object.Destroy(popup.gameObject);
            }

            if (_damageTextPoolRoot != null)
            {
                Object.Destroy(_damageTextPoolRoot.gameObject);
                _damageTextPoolRoot = null;
            }
        }
    }
}
