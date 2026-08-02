using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 무기 선택 UI. IconCycleSelectUI로 아이콘을 넘기고, 확정 시 WeaponData만 부모에 전달한다.
    /// </summary>
    public class WeaponSelectUI : UIBase
    {
        [SerializeField]
        private IconCycleSelectUI _iconCycleSelectUI;

        private readonly List<WeaponData> _weapons = new();
        private Action<WeaponData> _onConfirmed;
        private Action _onBack;
        private Action<WeaponData> _onPreviewChanged;
        private int _currentIndex;
        private int _iconLoadVersion;

        public WeaponData SelectedWeapon =>
            _currentIndex >= 0 && _currentIndex < _weapons.Count
                ? _weapons[_currentIndex]
                : null;

        public void Setup(
            IReadOnlyList<WeaponData> weapons,
            Action<WeaponData> onConfirmed,
            Action onBack = null,
            Action<WeaponData> onPreviewChanged = null,
            int startIndex = 0)
        {
            _onConfirmed = onConfirmed;
            _onBack = onBack;
            _onPreviewChanged = onPreviewChanged;
            _weapons.Clear();

            if (weapons != null)
            {
                for (int i = 0; i < weapons.Count; i++)
                {
                    if (weapons[i] != null)
                        _weapons.Add(weapons[i]);
                }
            }

            BindIconCycle();

            if (_weapons.Count == 0)
            {
                _currentIndex = -1;
                _iconCycleSelectUI?.ClearIcon();
                Debug.LogWarning("[WeaponSelectUI] 무기 리스트가 비어 있습니다.");
                return;
            }

            _currentIndex = Mathf.Clamp(startIndex, 0, _weapons.Count - 1);
            RefreshCurrentWeapon();
        }

        /// <summary>
        /// Inspector 확정 버튼에서 연결할 수 있다.
        /// </summary>
        public void OnClickConfirm()
        {
            WeaponData selected = SelectedWeapon;
            if (selected == null)
            {
                Debug.LogWarning("[WeaponSelectUI] 선택된 무기가 없습니다.");
                return;
            }

            _onConfirmed?.Invoke(selected);
        }

        /// <summary>
        /// Inspector 뒤로가기 버튼에서 연결. 캐릭터 선택 단계로 돌아간다.
        /// </summary>
        public void OnClickBack()
        {
            _onBack?.Invoke();
        }

        private void BindIconCycle()
        {
            if (_iconCycleSelectUI == null)
                _iconCycleSelectUI = GetComponentInChildren<IconCycleSelectUI>(true);

            if (_iconCycleSelectUI == null)
            {
                Debug.LogError("[WeaponSelectUI] IconCycleSelectUI가 없습니다.");
                return;
            }

            _iconCycleSelectUI.OnMoveRequested -= HandleMoveRequested;
            _iconCycleSelectUI.OnMoveRequested += HandleMoveRequested;
        }

        private void HandleMoveRequested(int direction)
        {
            if (_weapons.Count == 0)
                return;

            int nextIndex = _currentIndex + direction;
            if (nextIndex < 0)
                nextIndex = _weapons.Count - 1;
            else if (nextIndex >= _weapons.Count)
                nextIndex = 0;

            if (nextIndex == _currentIndex)
                return;

            _currentIndex = nextIndex;
            RefreshCurrentWeapon();
        }

        private void RefreshCurrentWeapon()
        {
            WeaponData weapon = SelectedWeapon;
            if (weapon == null)
            {
                _iconCycleSelectUI?.ClearIcon();
                return;
            }

            UpdateIconAsync(weapon.IconPath);
            _onPreviewChanged?.Invoke(weapon);
        }

        private async void UpdateIconAsync(string iconName)
        {
            if (_iconCycleSelectUI == null)
                return;

            if (string.IsNullOrEmpty(iconName))
            {
                _iconCycleSelectUI.ClearIcon();
                return;
            }

            int version = ++_iconLoadVersion;
            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[WeaponSelectUI] ResourceManager를 찾을 수 없습니다.");
                return;
            }

            Sprite sprite = await resourceManager.GetAtlasSpriteAsync(ATLAS_TYPE.UI, iconName);
            if (version != _iconLoadVersion)
                return;

            _iconCycleSelectUI.SetIcon(sprite);
        }

        private void OnDestroy()
        {
            if (_iconCycleSelectUI != null)
                _iconCycleSelectUI.OnMoveRequested -= HandleMoveRequested;
        }
    }
}
