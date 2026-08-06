using Michsky.UI.Heat;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 적(NPC) 머리 위 월드 HP바. Heat ProgressBar 프리팹을 사용합니다.
    /// </summary>
    public partial class CharacterBase
    {
        private const float HealthBarWorldScale = 0.00425f;

        private GameObject _healthBarRoot;
        private ProgressBar _healthBar;
        private bool _healthBarEnabled;

        /// <summary>HP바 부착 위치. 미지정 시 DamageTextPoint.</summary>
        public Transform HealthBarPoint =>
            _healthBarPoint != null ? _healthBarPoint : DamageTextPoint;

        /// <summary>
        /// 적 유닛용 HP바를 생성합니다. UNIT_TYPE.NPC일 때만 동작합니다.
        /// </summary>
        public void SetupEnemyHealthBar()
        {
            if (_unitInfo == null || _unitInfo.UnitType != UNIT_TYPE.NPC)
                return;

            if (_healthBarEnabled && _healthBar != null)
            {
                RefreshHealthBar();
                return;
            }

            if (_enemyHealthBarPrefab == null)
            {
                Debug.LogWarning(
                    $"[HealthBar] Enemy Health Bar 프리팹이 비어 있습니다: {name}");
                return;
            }

            Transform anchor = HealthBarPoint;
            if (anchor == null)
                return;

            _healthBarRoot = new GameObject("EnemyHealthBarCanvas");
            _healthBarRoot.transform.SetParent(anchor, false);
            _healthBarRoot.transform.localPosition = Vector3.zero;
            _healthBarRoot.transform.localRotation = Quaternion.identity;
            _healthBarRoot.transform.localScale = Vector3.one * HealthBarWorldScale;

            var canvas = _healthBarRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 80;

            var scaler = _healthBarRoot.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            // 클릭이 캐릭터로 가도록 GraphicRaycaster는 넣지 않음
            var rect = _healthBarRoot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 40f);

            var instance = Instantiate(_enemyHealthBarPrefab, _healthBarRoot.transform, false);
            instance.name = "HealthBar";
            var instanceRect = instance.GetComponent<RectTransform>();
            if (instanceRect != null)
            {
                instanceRect.anchoredPosition = Vector2.zero;
                instanceRect.localScale = Vector3.one;
            }

            DisableRaycasts(instance);
            HideOptionalIcon(instance);

            _healthBar = instance.GetComponent<ProgressBar>();
            if (_healthBar == null)
                _healthBar = instance.GetComponentInChildren<ProgressBar>(true);

            if (_healthBar == null)
            {
                Debug.LogError("[HealthBar] ProgressBar 컴포넌트를 찾을 수 없습니다.");
                Destroy(_healthBarRoot);
                _healthBarRoot = null;
                return;
            }

            _healthBar.addPrefix = false;
            _healthBar.addSuffix = false;
            _healthBar.decimals = 0;
            _healthBar.minValue = 0f;
            _healthBar.Initialize();

            _healthBarEnabled = true;
            RefreshHealthBar();
            FaceHealthBarToCamera();
        }

        public void RefreshHealthBar()
        {
            if (!_healthBarEnabled || _healthBar == null || _unitInfo == null)
                return;

            int maxHp = Mathf.Max(1, _unitInfo.MaxHp);
            int currentHp = Mathf.Clamp(_unitInfo.CurrentHp, 0, maxHp);

            _healthBar.maxValue = maxHp;
            _healthBar.maxValueLimit = maxHp;
            _healthBar.SetValue(currentHp);

            if (_healthBarRoot != null)
                _healthBarRoot.SetActive(currentHp > 0);
        }

        public void SetHealthBarVisible(bool visible)
        {
            if (_healthBarRoot == null)
                return;

            _healthBarRoot.SetActive(visible && IsAlive);
        }

        public void ReleaseHealthBar()
        {
            _healthBarEnabled = false;
            _healthBar = null;

            if (_healthBarRoot != null)
            {
                Destroy(_healthBarRoot);
                _healthBarRoot = null;
            }
        }

        private void UpdateHealthBarBillboard()
        {
            if (_healthBarEnabled && _healthBarRoot != null && _healthBarRoot.activeSelf)
                FaceHealthBarToCamera();
        }

        private void FaceHealthBarToCamera()
        {
            if (_healthBarRoot == null)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            _healthBarRoot.transform.rotation = Quaternion.LookRotation(
                _healthBarRoot.transform.position - cam.transform.position,
                Vector3.up);
        }

        private static void DisableRaycasts(GameObject root)
        {
            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        private static void HideOptionalIcon(GameObject root)
        {
            // Heat Health Bar의 아이콘은 전투 HUD에선 숨김
            Transform icon = root.transform.Find("Icon Background");
            if (icon != null)
                icon.gameObject.SetActive(false);

            Transform iconAlt = root.transform.Find("BG/Icon Background");
            if (iconAlt != null)
                iconAlt.gameObject.SetActive(false);

            // 하위에서 Icon 이름 포함 오브젝트 비활성
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                string n = transforms[i].name;
                if (n.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    transforms[i].gameObject.SetActive(false);
            }
        }
    }
}
