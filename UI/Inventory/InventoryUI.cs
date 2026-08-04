using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 런 인벤토리. 보유 카드 / 아이템을 탭으로 확인한다.
    /// 레이아웃은 InventoryUI 프리팹에서 구성한다.
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
        private Color _tabActiveColor = SoftPalette.AccentRoseGold;

        [SerializeField]
        private Color _tabInactiveColor = SoftPalette.TextMuted;

        private InventoryTab _currentTab = InventoryTab.Cards;
        private bool _buttonsBound;
        private bool _fontsApplied;

        private void Awake()
        {
            ApplyFonts();
            BindButtons();
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

            if (_itemDetailRoot != null)
                _itemDetailRoot.SetActive(tab == InventoryTab.Items);

            if (_cardsTabLabel != null)
                _cardsTabLabel.color = tab == InventoryTab.Cards ? _tabActiveColor : _tabInactiveColor;

            if (_itemsTabLabel != null)
                _itemsTabLabel.color = tab == InventoryTab.Items ? _tabActiveColor : _tabInactiveColor;

            ApplyTabButtonVisual(_cardsTabButton, tab == InventoryTab.Cards);
            ApplyTabButtonVisual(_itemsTabButton, tab == InventoryTab.Items);

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

                // Close X는 Cinzel 유지
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
