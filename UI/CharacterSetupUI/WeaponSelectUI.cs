using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 무기 선택 UI. IconCycleSelectUI로 아이콘을 넘기고, 확정 시 WeaponData만 부모에 전달한다.
    /// </summary>
    public class WeaponSelectUI : UIBase
    {
        [SerializeField]
        private IconCycleSelectUI _iconCycleSelectUI;

        [SerializeField]
        private Button _confirmButton;

        private readonly List<WeaponData> _weapons = new();
        private Action<WeaponData> _onConfirmed;
        private int _currentIndex;
        private int _iconLoadVersion;

        public WeaponData SelectedWeapon =>
            _currentIndex >= 0 && _currentIndex < _weapons.Count
                ? _weapons[_currentIndex]
                : null;

        public void Setup(IReadOnlyList<WeaponData> weapons, Action<WeaponData> onConfirmed, int startIndex = 0)
        {
            _onConfirmed = onConfirmed;
            _weapons.Clear();

            if (weapons != null)
            {
                for (int i = 0; i < weapons.Count; i++)
                {
                    if (weapons[i] != null)
                        _weapons.Add(weapons[i]);
                }
            }

            BindConfirmButton();
            BindIconCycle();

            if (_weapons.Count == 0)
            {
                _currentIndex = -1;
                _iconCycleSelectUI?.ClearIcon();
                Debug.LogWarning("[WeaponSelectUI] 무기 리스트가 비어 있습니다.");
                return;
            }

            _currentIndex = Mathf.Clamp(startIndex, 0, _weapons.Count - 1);
            RefreshCurrentIcon();
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

        private void BindConfirmButton()
        {
            if (_confirmButton == null)
                _confirmButton = FindConfirmButton();

            if (_confirmButton == null)
                return;

            _confirmButton.onClick.RemoveListener(OnClickConfirm);
            _confirmButton.onClick.AddListener(OnClickConfirm);
        }

        private Button FindConfirmButton()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.name == "SelectButton")
                    return buttons[i];
            }

            return null;
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
            RefreshCurrentIcon();
        }

        private void RefreshCurrentIcon()
        {
            WeaponData weapon = SelectedWeapon;
            if (weapon == null || _iconCycleSelectUI == null)
            {
                _iconCycleSelectUI?.ClearIcon();
                return;
            }

            UpdateIconAsync(weapon.IconPath);
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
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(OnClickConfirm);

            if (_iconCycleSelectUI != null)
                _iconCycleSelectUI.OnMoveRequested -= HandleMoveRequested;
        }
    }
}
