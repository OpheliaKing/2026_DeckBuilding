using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 인벤토리 아이템 슬롯. 클릭 시 선택되어 설명을 보여준다.
    /// 비주얼은 InventoryItemSlot 프리팹에서 구성한다.
    /// </summary>
    public class InventoryItemSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        private Image _iconImage;

        [SerializeField]
        private Image _frameImage;

        [SerializeField]
        private TextMeshProUGUI _nameText;

        [SerializeField]
        private Color _normalFrameColor = SoftPalette.SlotNormal;

        [SerializeField]
        private Color _selectedFrameColor = SoftPalette.SlotSelected;

        private ItemData _itemData;
        private Action<InventoryItemSlotUI> _onSelected;
        private bool _selected;
        private bool _fontApplied;

        public ItemData ItemData => _itemData;

        private void Awake()
        {
            ApplyFont();
        }

        public void Bind(ItemData itemData, Action<InventoryItemSlotUI> onSelected)
        {
            ApplyFont();
            _itemData = itemData;
            _onSelected = onSelected;
            SetSelected(false);

            if (_iconImage != null)
            {
                _iconImage.sprite = itemData != null ? itemData.ItemIcon : null;
                _iconImage.enabled = _iconImage.sprite != null;
            }

            if (_nameText != null)
                _nameText.text = itemData != null ? itemData.ItemName : string.Empty;
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_frameImage != null)
                _frameImage.color = selected ? _selectedFrameColor : _normalFrameColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onSelected?.Invoke(this);
        }

        private void ApplyFont()
        {
            if (_fontApplied)
                return;

            UiFont.ApplyBody(_nameText);
            _fontApplied = true;
        }
    }
}
