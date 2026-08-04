using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 인벤토리 아이템 그리드. 슬롯 선택 시 ItemDescription을 부모에 전달한다.
    /// </summary>
    public class InventoryItemListUI : MonoBehaviour
    {
        [SerializeField]
        private Transform _contentRoot;

        [SerializeField]
        private GridLayoutGroup _gridLayout;

        private readonly List<InventoryItemSlotUI> _slots = new();
        private Action<ItemData> _onItemSelected;
        private InventoryItemSlotUI _selectedSlot;

        public void EnsureBuilt(Transform parent)
        {
            if (parent != null && transform.parent != parent)
                transform.SetParent(parent, false);

            RectTransform rect = transform as RectTransform;
            if (rect == null)
                rect = gameObject.AddComponent<RectTransform>();

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (_contentRoot == null)
            {
                ScrollRect scroll = GetComponent<ScrollRect>();
                if (scroll == null)
                    scroll = gameObject.AddComponent<ScrollRect>();

                Image rootImage = GetComponent<Image>();
                if (rootImage == null)
                {
                    rootImage = gameObject.AddComponent<Image>();
                    rootImage.color = new Color(0f, 0f, 0f, 0.2f);
                }
                rootImage.raycastTarget = true;

                Transform viewportT = transform.Find("Viewport");
                GameObject viewportGo = viewportT != null
                    ? viewportT.gameObject
                    : CreateChild(transform, "Viewport", out RectTransform viewportRect);
                RectTransform viewportRectTransform = viewportGo.transform as RectTransform;
                viewportRectTransform.anchorMin = Vector2.zero;
                viewportRectTransform.anchorMax = Vector2.one;
                viewportRectTransform.offsetMin = new Vector2(8f, 8f);
                viewportRectTransform.offsetMax = new Vector2(-8f, -8f);

                if (viewportGo.GetComponent<RectMask2D>() == null)
                    viewportGo.AddComponent<RectMask2D>();
                Image viewportImage = viewportGo.GetComponent<Image>();
                if (viewportImage == null)
                {
                    viewportImage = viewportGo.AddComponent<Image>();
                    viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
                }
                viewportImage.raycastTarget = true;

                Transform contentT = viewportGo.transform.Find("Content");
                GameObject contentGo = contentT != null
                    ? contentT.gameObject
                    : CreateChild(viewportGo.transform, "Content", out _);
                _contentRoot = contentGo.transform;

                RectTransform contentRect = _contentRoot as RectTransform;
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(0f, 0f);

                _gridLayout = contentGo.GetComponent<GridLayoutGroup>();
                if (_gridLayout == null)
                    _gridLayout = contentGo.AddComponent<GridLayoutGroup>();
                _gridLayout.cellSize = new Vector2(120f, 140f);
                _gridLayout.spacing = new Vector2(12f, 12f);
                _gridLayout.padding = new RectOffset(8, 8, 8, 8);
                _gridLayout.childAlignment = TextAnchor.UpperLeft;
                _gridLayout.constraint = GridLayoutGroup.Constraint.Flexible;

                ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                    fitter = contentGo.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scroll.viewport = viewportRectTransform;
                scroll.content = contentRect;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
            }
        }

        public void Setup(IReadOnlyList<ItemData> items, Action<ItemData> onItemSelected)
        {
            EnsureBuilt(transform.parent);
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
            var go = new GameObject("ItemSlot", typeof(RectTransform));
            go.transform.SetParent(_contentRoot, false);
            InventoryItemSlotUI slot = go.AddComponent<InventoryItemSlotUI>();
            slot.EnsureBuilt();
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

        private static GameObject CreateChild(Transform parent, string name, out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            rect = go.GetComponent<RectTransform>();
            return go;
        }
    }
}
