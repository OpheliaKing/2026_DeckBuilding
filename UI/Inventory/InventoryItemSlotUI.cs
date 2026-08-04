using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 인벤토리 아이템 슬롯. 클릭 시 선택되어 설명을 보여준다.
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
        private Color _normalFrameColor = new Color(1f, 1f, 1f, 0.15f);

        [SerializeField]
        private Color _selectedFrameColor = new Color(1f, 0.85f, 0.35f, 0.55f);

        private ItemData _itemData;
        private Action<InventoryItemSlotUI> _onSelected;
        private bool _selected;

        public ItemData ItemData => _itemData;

        public void EnsureBuilt()
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null)
                rect = gameObject.AddComponent<RectTransform>();

            rect.sizeDelta = new Vector2(120f, 140f);

            if (_frameImage == null)
            {
                _frameImage = GetComponent<Image>();
                if (_frameImage == null)
                    _frameImage = gameObject.AddComponent<Image>();
                _frameImage.color = _normalFrameColor;
                _frameImage.raycastTarget = true;
            }

            if (_iconImage == null)
            {
                Transform iconT = transform.Find("Icon");
                GameObject iconGo = iconT != null
                    ? iconT.gameObject
                    : CreateChild("Icon", out _);
                _iconImage = iconGo.GetComponent<Image>();
                if (_iconImage == null)
                    _iconImage = iconGo.AddComponent<Image>();
                _iconImage.preserveAspect = true;
                _iconImage.raycastTarget = false;

                RectTransform iconRect = iconGo.transform as RectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.55f);
                iconRect.anchorMax = new Vector2(0.5f, 0.55f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(72f, 72f);
                iconRect.anchoredPosition = Vector2.zero;
            }

            if (_nameText == null)
            {
                Transform nameT = transform.Find("Name");
                GameObject nameGo = nameT != null
                    ? nameT.gameObject
                    : CreateChild("Name", out _);
                _nameText = nameGo.GetComponent<TextMeshProUGUI>();
                if (_nameText == null)
                    _nameText = nameGo.AddComponent<TextMeshProUGUI>();
                _nameText.fontSize = 16f;
                _nameText.alignment = TextAlignmentOptions.Center;
                _nameText.color = Color.white;
                _nameText.raycastTarget = false;
                _nameText.enableWordWrapping = true;
                _nameText.overflowMode = TextOverflowModes.Ellipsis;
                UiFont.ApplyNotoSansRegular(_nameText);

                RectTransform nameRect = nameGo.transform as RectTransform;
                nameRect.anchorMin = new Vector2(0f, 0f);
                nameRect.anchorMax = new Vector2(1f, 0.28f);
                nameRect.offsetMin = new Vector2(4f, 4f);
                nameRect.offsetMax = new Vector2(-4f, 0f);
            }
        }

        public void Bind(ItemData itemData, Action<InventoryItemSlotUI> onSelected)
        {
            EnsureBuilt();
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

        private GameObject CreateChild(string name, out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            rect = go.GetComponent<RectTransform>();
            return go;
        }
    }
}
