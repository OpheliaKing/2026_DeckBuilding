using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SHIN
{
    public class StageShopUIObject : MonoBehaviour, IPointerClickHandler
    {
        [Header("Item")]
        [SerializeField]
        private GameObject _itemLayer;
        [SerializeField]
        private Image _itemIcon;
        [SerializeField]
        private TextMeshProUGUI _itemDescText;

        [Header("Card")]
        [SerializeField]
        private GameObject _cardLayer;
        [SerializeField]
        private Transform _cardRoot;

        [Header("Gold")]
        [SerializeField]
        private TextMeshProUGUI _goldText;

        [Header("Other")]
        [SerializeField]
        private GameObject _soldOutLayer;

        [SerializeField]
        private Color _normalGoldColor = Color.white;
        [SerializeField]
        private Color _notEnoughGoldColor = Color.red;

        private StageShopOffer _offer;
        private Action<int> _onBuy;
        private int _offerIndex = -1;
        private int _currentGold;
        private bool _wasSoldOut;

        private InGameCardObject _cardObj;
        private GameObject _cardInstance;
        private bool _isCardLoading;
        private int _bindVersion;

        private void Awake()
        {
            if (_cardRoot == null && _cardLayer != null)
                _cardRoot = _cardLayer.transform;

            EnsureUiClickable();
        }

        public void Bind(StageShopOffer offer, int offerIndex, int currentGold, Action<int> onBuy)
        {
            bool prevSoldOut = _offer != null && _offer.IsSoldOut;

            _offer = offer;
            _offerIndex = offerIndex;
            _currentGold = currentGold;
            _onBuy = onBuy;
            _bindVersion++;

            if (!prevSoldOut && _offer != null && _offer.IsSoldOut)
            {
                Debug.Log(
                    $"[StageShopUIObject] 구매 완료(품절 처리): slot={_offerIndex}, tid={_offer.Reward?.Tid}, price={_offer.Price}");
            }

            _wasSoldOut = _offer != null && _offer.IsSoldOut;
            ApplyVisualAsync(_bindVersion);
        }

        public void RefreshGold(int currentGold)
        {
            _currentGold = currentGold;
            UpdatePriceVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                return;

            HandleBuyClick();
        }

        private async void ApplyVisualAsync(int version)
        {
            StageRewardOffer reward = _offer != null ? _offer.Reward : null;
            bool isItem = reward != null && reward.Kind == STAGE_REWARD_KIND.ITEM;
            bool isCard = reward != null && reward.Kind == STAGE_REWARD_KIND.CARD;

            if (_itemLayer != null)
                _itemLayer.SetActive(isItem);
            if (_cardLayer != null)
                _cardLayer.SetActive(isCard);

            if (isItem && reward.ItemData != null)
            {
                if (_itemIcon != null)
                    _itemIcon.sprite = reward.ItemData.ItemIcon;
                if (_itemDescText != null)
                    _itemDescText.text = reward.ItemData.ItemDescription;
            }
            else if (_itemDescText != null)
            {
                _itemDescText.text = string.Empty;
            }

            if (isCard)
            {
                if (!await EnsureCardObjectAsync())
                    return;

                if (version != _bindVersion)
                    return;

                _cardObj.gameObject.SetActive(true);
                _cardObj.SetData(reward.CardData);
                _cardObj.SetInteractable(false);
            }
            else if (_cardObj != null)
            {
                _cardObj.gameObject.SetActive(false);
            }

            UpdatePriceVisual();
        }

        private async System.Threading.Tasks.Task<bool> EnsureCardObjectAsync()
        {
            if (_cardObj != null)
                return true;

            while (_isCardLoading)
            {
                await System.Threading.Tasks.Task.Yield();
                if (_cardObj != null)
                    return true;
            }

            if (_cardObj != null)
                return true;

            ResourceManager resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[StageShopUIObject] ResourceManager를 찾을 수 없습니다.");
                return false;
            }

            if (_cardRoot == null)
                _cardRoot = transform;

            _isCardLoading = true;
            GameObject instance = await resourceManager.InstantiateAsync(
                PublicVariable.Address.CardObjectPrefab,
                _cardRoot);
            _isCardLoading = false;

            if (_cardObj != null)
            {
                if (instance != null)
                    resourceManager.ReleaseInstance(instance);
                return true;
            }

            if (instance == null)
            {
                Debug.LogError("[StageShopUIObject] CardObject 생성 실패");
                return false;
            }

            _cardInstance = instance;
            _cardObj = instance.GetComponent<InGameCardObject>();
            if (_cardObj == null)
                _cardObj = instance.GetComponentInChildren<InGameCardObject>(true);

            if (_cardObj == null)
            {
                Debug.LogError("[StageShopUIObject] InGameCardObject 컴포넌트가 없습니다.");
                ReleaseCardObject();
                return false;
            }

            return true;
        }

        private void UpdatePriceVisual()
        {
            int price = _offer != null ? Mathf.Max(0, _offer.Price) : 0;
            if (_goldText != null)
            {
                _goldText.text = price.ToString();
                bool canAfford = _offer != null && !_offer.IsSoldOut && _currentGold >= price;
                _goldText.color = canAfford ? _normalGoldColor : _notEnoughGoldColor;
            }

            if (_soldOutLayer != null)
                _soldOutLayer.SetActive(_offer != null && _offer.IsSoldOut);
        }

        private void HandleBuyClick()
        {
            if (_offer == null)
                return;

            if (_offer.IsSoldOut)
            {
                Debug.Log(
                    $"[StageShopUIObject] 구매 불가(품절): slot={_offerIndex}, tid={_offer.Reward?.Tid}");
                return;
            }

            int price = Mathf.Max(0, _offer.Price);
            if (_currentGold < price)
            {
                Debug.Log(
                    $"[StageShopUIObject] 구매 불가(골드 부족): slot={_offerIndex}, need={price}, have={_currentGold}, tid={_offer.Reward?.Tid}");
                return;
            }

            Debug.Log(
                $"[StageShopUIObject] 구매 요청: slot={_offerIndex}, price={price}, tid={_offer.Reward?.Tid}");

            _onBuy?.Invoke(_offerIndex);
        }

        private void EnsureUiClickable()
        {
            Graphic graphic = GetComponent<Graphic>();
            if (graphic == null)
            {
                var image = gameObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = true;
            }
            else
            {
                graphic.raycastTarget = true;
            }
        }

        private void ReleaseCardObject()
        {
            if (_cardInstance == null)
            {
                _cardObj = null;
                return;
            }

            ResourceManager resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager != null)
                resourceManager.ReleaseInstance(_cardInstance);
            else
                Destroy(_cardInstance);

            _cardInstance = null;
            _cardObj = null;
        }

        private void OnDestroy()
        {
            _bindVersion++;
            ReleaseCardObject();
        }
    }
}

