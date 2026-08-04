using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 런 인벤토리. 보유 카드 / 아이템을 탭으로 확인한다.
    /// 아이템은 클릭 선택 시 ItemDescription을 상세 패널에 표시한다.
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
        private Color _tabActiveColor = new Color(1f, 0.85f, 0.4f, 1f);

        [SerializeField]
        private Color _tabInactiveColor = new Color(0.75f, 0.75f, 0.75f, 1f);

        private InventoryTab _currentTab = InventoryTab.Cards;
        private bool _built;
        private bool _buttonsBound;

        private void Awake()
        {
            EnsureBuilt();
            BindButtons();
        }

        /// <summary>
        /// 현재 플레이어 런 데이터로 인벤토리를 채운다.
        /// </summary>
        public void Setup()
        {
            EnsureBuilt();
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

            if (_itemDetailRoot != null)
                _itemDetailRoot.SetActive(tab == InventoryTab.Items);

            if (_cardsTabLabel != null)
                _cardsTabLabel.color = tab == InventoryTab.Cards ? _tabActiveColor : _tabInactiveColor;

            if (_itemsTabLabel != null)
                _itemsTabLabel.color = tab == InventoryTab.Items ? _tabActiveColor : _tabInactiveColor;

            if (tab != InventoryTab.Items)
                ClearItemDetail();
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

            ClearItemDetail();
            SetEmptyState(count == 0, "보유한 아이템이 없습니다.");
        }

        private void OnItemSelected(ItemData itemData)
        {
            if (_itemNameText != null)
                _itemNameText.text = itemData != null ? itemData.ItemName : string.Empty;

            if (_itemDescriptionText != null)
                _itemDescriptionText.text = itemData != null ? itemData.ItemDescription : string.Empty;

            if (_itemDetailRoot != null)
                _itemDetailRoot.SetActive(true);
        }

        private void ClearItemDetail()
        {
            if (_itemNameText != null)
                _itemNameText.text = "아이템을 선택하세요";

            if (_itemDescriptionText != null)
                _itemDescriptionText.text = string.Empty;
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

        /// <summary>
        /// 빈 프리팹이어도 동작하도록 런타임 레이아웃을 구성한다.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_built)
                return;

            RectTransform root = transform as RectTransform;
            if (root == null)
                root = gameObject.AddComponent<RectTransform>();

            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            Image dim = GetComponent<Image>();
            if (dim == null)
            {
                dim = gameObject.AddComponent<Image>();
                dim.color = new Color(0f, 0f, 0f, 0.65f);
            }
            dim.raycastTarget = true;

            Transform panelT = transform.Find("Panel");
            GameObject panelGo = panelT != null
                ? panelT.gameObject
                : CreateChild(transform, "Panel", out RectTransform panelRect);

            RectTransform panelRectTransform = panelGo.transform as RectTransform;
            panelRectTransform.anchorMin = new Vector2(0.08f, 0.08f);
            panelRectTransform.anchorMax = new Vector2(0.92f, 0.92f);
            panelRectTransform.offsetMin = Vector2.zero;
            panelRectTransform.offsetMax = Vector2.zero;

            Image panelImage = panelGo.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panelGo.AddComponent<Image>();
                panelImage.color = new Color(0.1f, 0.1f, 0.14f, 0.96f);
            }

            // Header
            Transform headerT = panelGo.transform.Find("Header");
            GameObject headerGo = headerT != null
                ? headerT.gameObject
                : CreateChild(panelGo.transform, "Header", out _);
            RectTransform headerRect = headerGo.transform as RectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 64f);

            TextMeshProUGUI title = EnsureText(headerGo.transform, "Title", "인벤토리", 28f, TextAlignmentOptions.MidlineLeft);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(24f, 0f);
            titleRect.offsetMax = new Vector2(-120f, 0f);

            if (_closeButton == null)
            {
                Transform closeT = headerGo.transform.Find("CloseButton");
                GameObject closeGo = closeT != null
                    ? closeT.gameObject
                    : CreateChild(headerGo.transform, "CloseButton", out _);
                RectTransform closeRect = closeGo.transform as RectTransform;
                closeRect.anchorMin = new Vector2(1f, 0.5f);
                closeRect.anchorMax = new Vector2(1f, 0.5f);
                closeRect.pivot = new Vector2(1f, 0.5f);
                closeRect.anchoredPosition = new Vector2(-16f, 0f);
                closeRect.sizeDelta = new Vector2(100f, 40f);

                Image closeImage = closeGo.GetComponent<Image>();
                if (closeImage == null)
                {
                    closeImage = closeGo.AddComponent<Image>();
                    closeImage.color = new Color(0.35f, 0.2f, 0.2f, 1f);
                }

                _closeButton = closeGo.GetComponent<Button>();
                if (_closeButton == null)
                    _closeButton = closeGo.AddComponent<Button>();
                _closeButton.targetGraphic = closeImage;

                TextMeshProUGUI closeLabel = EnsureText(closeGo.transform, "Label", "닫기", 20f, TextAlignmentOptions.Center);
                RectTransform closeLabelRect = closeLabel.rectTransform;
                closeLabelRect.anchorMin = Vector2.zero;
                closeLabelRect.anchorMax = Vector2.one;
                closeLabelRect.offsetMin = Vector2.zero;
                closeLabelRect.offsetMax = Vector2.zero;
            }

            // Tabs
            Transform tabsT = panelGo.transform.Find("Tabs");
            GameObject tabsGo = tabsT != null
                ? tabsT.gameObject
                : CreateChild(panelGo.transform, "Tabs", out _);
            RectTransform tabsRect = tabsGo.transform as RectTransform;
            tabsRect.anchorMin = new Vector2(0f, 1f);
            tabsRect.anchorMax = new Vector2(1f, 1f);
            tabsRect.pivot = new Vector2(0.5f, 1f);
            tabsRect.anchoredPosition = new Vector2(0f, -64f);
            tabsRect.sizeDelta = new Vector2(0f, 48f);

            HorizontalLayoutGroup tabsLayout = tabsGo.GetComponent<HorizontalLayoutGroup>();
            if (tabsLayout == null)
            {
                tabsLayout = tabsGo.AddComponent<HorizontalLayoutGroup>();
                tabsLayout.padding = new RectOffset(20, 20, 4, 4);
                tabsLayout.spacing = 12f;
                tabsLayout.childAlignment = TextAnchor.MiddleLeft;
                tabsLayout.childControlWidth = false;
                tabsLayout.childControlHeight = true;
                tabsLayout.childForceExpandHeight = true;
            }

            if (_cardsTabButton == null)
            {
                _cardsTabButton = CreateTabButton(tabsGo.transform, "CardsTab", "카드", out _cardsTabLabel);
            }

            if (_itemsTabButton == null)
            {
                _itemsTabButton = CreateTabButton(tabsGo.transform, "ItemsTab", "아이템", out _itemsTabLabel);
            }

            // Content area
            Transform contentT = panelGo.transform.Find("Content");
            GameObject contentGo = contentT != null
                ? contentT.gameObject
                : CreateChild(panelGo.transform, "Content", out _);
            RectTransform contentRect = contentGo.transform as RectTransform;
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(16f, 140f);
            contentRect.offsetMax = new Vector2(-16f, -120f);

            if (_cardListUI == null)
            {
                Transform cardListT = contentGo.transform.Find("CardList");
                GameObject cardListGo = cardListT != null
                    ? cardListT.gameObject
                    : CreateChild(contentGo.transform, "CardList", out _);
                _cardListUI = cardListGo.GetComponent<InventoryCardListUI>();
                if (_cardListUI == null)
                    _cardListUI = cardListGo.AddComponent<InventoryCardListUI>();
            }

            _cardListUI.EnsureBuilt(contentGo.transform);

            if (_itemListUI == null)
            {
                Transform itemListT = contentGo.transform.Find("ItemList");
                GameObject itemListGo = itemListT != null
                    ? itemListT.gameObject
                    : CreateChild(contentGo.transform, "ItemList", out _);
                _itemListUI = itemListGo.GetComponent<InventoryItemListUI>();
                if (_itemListUI == null)
                    _itemListUI = itemListGo.AddComponent<InventoryItemListUI>();
            }

            _itemListUI.EnsureBuilt(contentGo.transform);

            // Item detail
            if (_itemDetailRoot == null)
            {
                Transform detailT = panelGo.transform.Find("ItemDetail");
                GameObject detailGo = detailT != null
                    ? detailT.gameObject
                    : CreateChild(panelGo.transform, "ItemDetail", out _);
                _itemDetailRoot = detailGo;

                RectTransform detailRect = detailGo.transform as RectTransform;
                detailRect.anchorMin = new Vector2(0f, 0f);
                detailRect.anchorMax = new Vector2(1f, 0f);
                detailRect.pivot = new Vector2(0.5f, 0f);
                detailRect.anchoredPosition = Vector2.zero;
                detailRect.sizeDelta = new Vector2(0f, 120f);

                Image detailImage = detailGo.GetComponent<Image>();
                if (detailImage == null)
                {
                    detailImage = detailGo.AddComponent<Image>();
                    detailImage.color = new Color(0.06f, 0.06f, 0.09f, 0.95f);
                }

                _itemNameText = EnsureText(detailGo.transform, "ItemName", "아이템을 선택하세요", 22f, TextAlignmentOptions.TopLeft);
                RectTransform nameRect = _itemNameText.rectTransform;
                nameRect.anchorMin = new Vector2(0f, 1f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.pivot = new Vector2(0.5f, 1f);
                nameRect.anchoredPosition = new Vector2(0f, -10f);
                nameRect.sizeDelta = new Vector2(-32f, 28f);

                _itemDescriptionText = EnsureText(detailGo.transform, "ItemDescription", string.Empty, 18f, TextAlignmentOptions.TopLeft);
                RectTransform descRect = _itemDescriptionText.rectTransform;
                descRect.anchorMin = new Vector2(0f, 0f);
                descRect.anchorMax = new Vector2(1f, 1f);
                descRect.offsetMin = new Vector2(16f, 10f);
                descRect.offsetMax = new Vector2(-16f, -40f);
                _itemDescriptionText.enableWordWrapping = true;
            }

            if (_emptyStateRoot == null)
            {
                Transform emptyT = contentGo.transform.Find("EmptyState");
                GameObject emptyGo = emptyT != null
                    ? emptyT.gameObject
                    : CreateChild(contentGo.transform, "EmptyState", out _);
                _emptyStateRoot = emptyGo;
                RectTransform emptyRect = emptyGo.transform as RectTransform;
                emptyRect.anchorMin = Vector2.zero;
                emptyRect.anchorMax = Vector2.one;
                emptyRect.offsetMin = Vector2.zero;
                emptyRect.offsetMax = Vector2.zero;

                _emptyStateText = EnsureText(emptyGo.transform, "Label", string.Empty, 24f, TextAlignmentOptions.Center);
                RectTransform emptyLabelRect = _emptyStateText.rectTransform;
                emptyLabelRect.anchorMin = Vector2.zero;
                emptyLabelRect.anchorMax = Vector2.one;
                emptyLabelRect.offsetMin = Vector2.zero;
                emptyLabelRect.offsetMax = Vector2.zero;
                _emptyStateRoot.SetActive(false);
            }

            _built = true;
        }

        private static Button CreateTabButton(
            Transform parent,
            string name,
            string label,
            out TextMeshProUGUI labelText)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null
                ? existing.gameObject
                : CreateChild(parent, name, out _);

            RectTransform rect = go.transform as RectTransform;
            rect.sizeDelta = new Vector2(140f, 40f);

            LayoutElement layout = go.GetComponent<LayoutElement>();
            if (layout == null)
                layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 140f;
            layout.minWidth = 120f;

            Image image = go.GetComponent<Image>();
            if (image == null)
            {
                image = go.AddComponent<Image>();
                image.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            }

            Button button = go.GetComponent<Button>();
            if (button == null)
                button = go.AddComponent<Button>();
            button.targetGraphic = image;

            labelText = EnsureText(go.transform, "Label", label, 20f, TextAlignmentOptions.Center);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return button;
        }

        private static TextMeshProUGUI EnsureText(
            Transform parent,
            string name,
            string value,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null
                ? existing.gameObject
                : CreateChild(parent, name, out _);

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            if (text == null)
                text = go.AddComponent<TextMeshProUGUI>();

            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            UiFont.ApplyNotoSansRegular(text);
            return text;
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
