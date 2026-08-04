using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 인벤토리 아이템 그리드. 슬롯 선택 시 ItemDescription을 부모에 전달한다.
    /// Scroll/Grid/슬롯 프리팹은 인스펙터에서 연결한다.
    /// </summary>
    public class InventoryItemListUI : MonoBehaviour
    {
        [SerializeField]
        private Transform _contentRoot;

        [SerializeField]
        private GridLayoutGroup _gridLayout;

        [SerializeField]
        private InventoryItemSlotUI _slotPrefab;

        private readonly List<InventoryItemSlotUI> _slots = new();
        private Action<ItemData> _onItemSelected;
        private InventoryItemSlotUI _selectedSlot;

        public void Setup(IReadOnlyList<ItemData> items, Action<ItemData> onItemSelected)
        {
            if (_contentRoot == null)
            {
                Debug.LogError("[InventoryItemListUI] _contentRoot가 프리팹에 연결되지 않았습니다.");
                return;
            }

            _onItemSelected = onItemSelected;
            _selectedSlot = null;
            ClearSlots();

            if (items == null || items.Count == 0)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                ItemData item = items[i];
                if (item == null)
                    continue;

                InventoryItemSlotUI slot = CreateSlot();
                if (slot == null)
                    continue;

                slot.Bind(item, OnSlotSelected);
                _slots.Add(slot);
            }
        }

        public void Clear()
        {
            ClearSlots();
            _onItemSelected = null;
            _selectedSlot = null;
        }

        private void OnSlotSelected(InventoryItemSlotUI slot)
        {
            if (_selectedSlot != null)
                _selectedSlot.SetSelected(false);

            _selectedSlot = slot;
            if (_selectedSlot != null)
                _selectedSlot.SetSelected(true);

            _onItemSelected?.Invoke(slot != null ? slot.ItemData : null);
        }

        private InventoryItemSlotUI CreateSlot()
        {
            if (_slotPrefab == null)
            {
                Debug.LogError("[InventoryItemListUI] _slotPrefab이 프리팹에 연결되지 않았습니다.");
                return null;
            }

            InventoryItemSlotUI slot = Instantiate(_slotPrefab, _contentRoot);
            slot.gameObject.name = "ItemSlot";
            slot.gameObject.SetActive(true);
            return slot;
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null)
                    Destroy(_slots[i].gameObject);
            }

            _slots.Clear();

            if (_contentRoot == null)
                return;

            for (int i = _contentRoot.childCount - 1; i >= 0; i--)
                Destroy(_contentRoot.GetChild(i).gameObject);
        }
    }
}
