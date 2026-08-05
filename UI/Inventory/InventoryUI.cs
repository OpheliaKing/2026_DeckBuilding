using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 런 인벤토리. 보유 카드 / 아이템을 탭으로 확인한다.
    /// 아이템 클릭 시 슬롯 옆에 설명 패널을 표시한다.
    /// </summary>
    public class InventoryUI : UIBase
    {
        public override UI_TYPE UiType => UI_TYPE.Popup;

        private enum InventoryTab
        {
            Cards = 0,
            Items = 1,
        }

        [SerializeField]
        private InventoryCardListUI _cardListUI;

        [SerializeField]
        private InventoryItemListUI _itemListUI;

        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        private Button _cardsTabButton;

        [SerializeField]
        private Button _itemsTabButton;

        [SerializeField]
        private TextMeshProUGUI _cardsTabLabel;

        [SerializeField]
        private TextMeshProUGUI _itemsTabLabel;

        [SerializeField]
        private TextMeshProUGUI _itemNameText;

        [SerializeField]
        private TextMeshProUGUI _itemDescriptionText;

        [SerializeField]
        private GameObject _itemDetailRoot;

        [SerializeField]
        private GameObject _emptyStateRoot;

        [SerializeField]
        private TextMeshProUGUI _emptyStateText;

        [SerializeField]
        private Vector2 _itemDetailSize = new(280f, 420f);

        [SerializeField]
        [Tooltip("슬롯 중심에서 패널 왼쪽이 얼마나 겹칠지(슬롯 너비 비율). 0.4 ≈ 슬롯 오른쪽 절반 위에 패널 시작")]
        private float _detailOverlapAlongSlot = 0.4f;

        [SerializeField]
        private Color _tabActiveColor = SoftPalette.AccentRoseGold;

        [SerializeField]
        private Color _tabInactiveColor = SoftPalette.TextMuted;

        private InventoryTab _currentTab = InventoryTab.Cards;
        private bool _buttonsBound;
        private bool _fontsApplied;
        private InventoryItemSlotUI _shownDetailSlot;

        private void Awake()
        {
            ApplyFonts();
            BindButtons();
            HideItemDetail();
        }

        private void Update()
        {
            if (_itemDetailRoot == null || !_itemDetailRoot.activeSelf)
                return;

            if (WasDismissKeyPressed())
            {
                DismissItemDetail();
                return;
            }

            if (!WasPrimaryPointerPressed())
                return;

            if (IsPointerOverDetailOrSelectedSlot())
                return;

            DismissItemDetail();
        }

        /// <summary>
        /// 현재 플레이어 런 데이터로 인벤토리를 채운다.
        /// </summary>
        public void Setup()
        {
            ApplyFonts();
            BindButtons();
            ShowTab(InventoryTab.Cards);
            RefreshCurrentTab();
        }

        public void OnClickClose()
        {
            UIManager uiManager = GameManager.Instance?.UIManager;
            if (uiManager != null && uiManager.Current == this)
            {
                uiManager.Close();
                return;
            }

            gameObject.SetActive(false);
        }

        public void OnClickCardsTab()
        {
            ShowTab(InventoryTab.Cards);
            RefreshCurrentTab();
        }

        public void OnClickItemsTab()
        {
            ShowTab(InventoryTab.Items);
            RefreshCurrentTab();
        }

        private void ShowTab(InventoryTab tab)
        {
            _currentTab = tab;

            if (_cardListUI != null)
                _cardListUI.gameObject.SetActive(tab == InventoryTab.Cards);

            if (_itemListUI != null)
                _itemListUI.gameObject.SetActive(tab == InventoryTab.Items);

            ApplyTabButtonVisual(_cardsTabButton, tab == InventoryTab.Cards);
            ApplyTabButtonVisual(_itemsTabButton, tab == InventoryTab.Items);

            if (_cardsTabLabel != null)
                _cardsTabLabel.color = tab == InventoryTab.Cards ? _tabActiveColor : _tabInactiveColor;

            if (_itemsTabLabel != null)
                _itemsTabLabel.color = tab == InventoryTab.Items ? _tabActiveColor : _tabInactiveColor;

            HideItemDetail();
        }

        private void RefreshCurrentTab()
        {
            UnitInfo player = GetPrimaryPlayer();
            if (_currentTab == InventoryTab.Cards)
                RefreshCards(player);
            else
                RefreshItems(player);
        }

        private void RefreshCards(UnitInfo player)
        {
            IReadOnlyList<CardData> cards = player?.DeckCardList;
            int count = cards != null ? cards.Count : 0;

            if (_cardListUI != null)
                _cardListUI.Setup(cards);

            SetEmptyState(count == 0, "보유한 카드가 없습니다.");
        }

        private void RefreshItems(UnitInfo player)
        {
            IReadOnlyList<ItemData> items = player?.Items;
            int count = items != null ? items.Count : 0;

            if (_itemListUI != null)
                _itemListUI.Setup(items, OnItemSelected);

            HideItemDetail();
            SetEmptyState(count == 0, "보유한 아이템이 없습니다.");
        }

        private void OnItemSelected(InventoryItemSlotUI slot)
        {
            if (slot == null || slot.ItemData == null)
            {
                HideItemDetail();
                return;
            }

            // 같은 슬롯 재클릭 시 패널 닫기
            if (_shownDetailSlot == slot && _itemDetailRoot != null && _itemDetailRoot.activeSelf)
            {
                DismissItemDetail();
                return;
            }

            ItemData itemData = slot.ItemData;

            if (_itemNameText != null)
                _itemNameText.text = itemData.ItemName ?? string.Empty;

            if (_itemDescriptionText != null)
                _itemDescriptionText.text = itemData.ItemDescription ?? string.Empty;

            ShowItemDetailNearSlot(slot);
        }

        private void DismissItemDetail()
        {
            InventoryItemSlotUI slot = _shownDetailSlot;
            HideItemDetail();
            if (slot != null)
                slot.SetSelected(false);
            _itemListUI?.ClearSelection();
        }

        private static bool WasPrimaryPointerPressed()
        {
            if (Input.GetMouseButtonDown(0))
                return true;

            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                return true;

            return false;
        }

        private static bool WasDismissKeyPressed()
        {
            return Input.GetKeyDown(KeyCode.Escape);
        }

        private bool IsPointerOverDetailOrSelectedSlot()
        {
            Vector2 screenPos = GetPrimaryPointerScreenPosition();
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;

            if (_itemDetailRoot != null)
            {
                var detailRt = _itemDetailRoot.transform as RectTransform;
                if (detailRt != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(detailRt, screenPos, cam))
                    return true;
            }

            if (_shownDetailSlot != null)
            {
                var slotRt = _shownDetailSlot.transform as RectTransform;
                if (slotRt != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(slotRt, screenPos, cam))
                    return true;
            }

            return false;
        }

        private static Vector2 GetPrimaryPointerScreenPosition()
        {
            if (Input.touchCount > 0)
                return Input.GetTouch(0).position;

            return Input.mousePosition;
        }

        private void ShowItemDetailNearSlot(InventoryItemSlotUI slot)
        {
            if (_itemDetailRoot == null || slot == null)
                return;

            RectTransform detailRt = _itemDetailRoot.transform as RectTransform;
            if (detailRt == null)
                return;

            _shownDetailSlot = slot;
            _itemDetailRoot.SetActive(true);
            detailRt.SetAsLastSibling();
            detailRt.sizeDelta = _itemDetailSize;

            RectTransform slotRect = slot.transform as RectTransform;
            PositionDetailBesideSlot(detailRt, slotRect);
        }

        private void PositionDetailBesideSlot(RectTransform detailRt, RectTransform slotRect)
        {
            if (detailRt == null || slotRect == null)
                return;

            RectTransform parent = detailRt.parent as RectTransform;
            if (parent == null)
                return;

            Canvas canvas = detailRt.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;

            // 슬롯 중심 → 패널 왼쪽(피벗)이 오도록 해서 슬롯 오른쪽에 겹쳐 보이게
            Vector3 worldCenter = slotRect.TransformPoint(slotRect.rect.center);
            Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenCenter, cam, out Vector2 localCenter))
                return;

            float slotWidthInParent = ApproximateSlotWidthInParent(slotRect, parent);
            float overlap = slotWidthInParent * Mathf.Clamp01(_detailOverlapAlongSlot);

            detailRt.pivot = new Vector2(0f, 0.5f);
            detailRt.anchorMin = new Vector2(0.5f, 0.5f);
            detailRt.anchorMax = new Vector2(0.5f, 0.5f);
            detailRt.anchoredPosition = new Vector2(localCenter.x - overlap, localCenter.y);

            ClampDetailInsideParent(detailRt, parent);
        }

        private static float ApproximateSlotWidthInParent(RectTransform slotRect, RectTransform parent)
        {
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;

            Vector3[] corners = new Vector3[4];
            slotRect.GetWorldCorners(corners);
            Vector2 screenL = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 screenR = RectTransformUtility.WorldToScreenPoint(cam, corners[3]);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenL, cam, out Vector2 localL))
                return slotRect.rect.width;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenR, cam, out Vector2 localR))
                return slotRect.rect.width;

            return Mathf.Abs(localR.x - localL.x);
        }

        private static void ClampDetailInsideParent(RectTransform detailRt, RectTransform parent)
        {
            if (detailRt == null || parent == null)
                return;

            Rect parentRect = parent.rect;
            Vector2 pos = detailRt.anchoredPosition;
            Vector2 size = detailRt.sizeDelta;
            Vector2 pivot = detailRt.pivot;

            float minX = parentRect.xMin + size.x * pivot.x;
            float maxX = parentRect.xMax - size.x * (1f - pivot.x);
            float minY = parentRect.yMin + size.y * pivot.y;
            float maxY = parentRect.yMax - size.y * (1f - pivot.y);

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            detailRt.anchoredPosition = pos;
        }

        private void HideItemDetail()
        {
            if (_itemDetailRoot != null)
                _itemDetailRoot.SetActive(false);

            if (_itemNameText != null)
                _itemNameText.text = string.Empty;

            if (_itemDescriptionText != null)
                _itemDescriptionText.text = string.Empty;

            _shownDetailSlot = null;
        }

        private void SetEmptyState(bool empty, string message)
        {
            if (_emptyStateRoot != null)
                _emptyStateRoot.SetActive(empty);

            if (_emptyStateText != null)
                _emptyStateText.text = empty ? message : string.Empty;
        }

        private static UnitInfo GetPrimaryPlayer()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
                return null;

            IReadOnlyList<UnitInfo> players = gameManager.PlayerCharacters;
            if (players == null || players.Count == 0)
                return null;

            return players[0];
        }

        private void BindButtons()
        {
            if (_buttonsBound)
                return;

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(OnClickClose);
                _closeButton.onClick.AddListener(OnClickClose);
            }

            if (_cardsTabButton != null)
            {
                _cardsTabButton.onClick.RemoveListener(OnClickCardsTab);
                _cardsTabButton.onClick.AddListener(OnClickCardsTab);
            }

            if (_itemsTabButton != null)
            {
                _itemsTabButton.onClick.RemoveListener(OnClickItemsTab);
                _itemsTabButton.onClick.AddListener(OnClickItemsTab);
            }

            _buttonsBound = true;
        }

        private void ApplyFonts()
        {
            if (_fontsApplied)
                return;

            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];
                if (text == null)
                    continue;

                string owner = text.gameObject.name;
                string parent = text.transform.parent != null
                    ? text.transform.parent.name
                    : string.Empty;

                if (parent == "CloseButton")
                    continue;

                if (parent == "TitleBanner" || owner == "ItemName")
                    UiFont.ApplyTitle(text);
                else
                    UiFont.ApplyBody(text);
            }

            _fontsApplied = true;
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnClickClose);
            if (_cardsTabButton != null)
                _cardsTabButton.onClick.RemoveListener(OnClickCardsTab);
            if (_itemsTabButton != null)
                _itemsTabButton.onClick.RemoveListener(OnClickItemsTab);

            _cardListUI?.Clear();
            _itemListUI?.Clear();
        }

        private static void ApplyTabButtonVisual(Button button, bool active)
        {
            if (button == null)
                return;

            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = active ? Color.white : SoftPalette.TabInactiveTint;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
