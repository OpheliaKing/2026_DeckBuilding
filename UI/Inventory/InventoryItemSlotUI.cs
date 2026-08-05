using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 인벤토리 아이템 슬롯. 아이콘만 표시하며, 클릭 시 옆에 설명을 보여준다.
    /// </summary>
    public class InventoryItemSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        private Image _iconImage;

        [SerializeField]
        private Image _frameImage;

        [SerializeField]
        private Color _normalFrameColor = SoftPalette.SlotNormal;

        [SerializeField]
        private Color _selectedFrameColor = SoftPalette.SlotSelected;

        private ItemData _itemData;
        private Action<InventoryItemSlotUI> _onSelected;

        public ItemData ItemData => _itemData;

        public void Bind(ItemData itemData, Action<InventoryItemSlotUI> onSelected)
        {
            _itemData = itemData;
            _onSelected = onSelected;
            SetSelected(false);

            if (_iconImage != null)
            {
                _iconImage.sprite = itemData != null ? itemData.ItemIcon : null;
                _iconImage.enabled = _iconImage.sprite != null;
            }
        }

        public void SetSelected(bool selected)
        {
            if (_frameImage != null)
                _frameImage.color = selected ? _selectedFrameColor : _normalFrameColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onSelected?.Invoke(this);
        }
    }
}
