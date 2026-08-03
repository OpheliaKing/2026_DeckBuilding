using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SHIN
{
    public class StageRewardObjectUI : ClickEventUI
    {
        [SerializeField]
        private GameObject _selectedMark;

        [SerializeField]
        private Transform _itemLayer;

        [SerializeField]
        private Image _itemIcon;

        [SerializeField]
        private TextMeshProUGUI _itemDescText;

        [SerializeField]
        private Transform _cardRoot;

        private InGameCardObject _cardObj;
        private GameObject _cardInstance;
        private bool _isCardLoading;
        private int _bindVersion;

        private StageRewardOffer _offer;
        private Action<StageRewardObjectUI> _onClicked;

        public StageRewardOffer Offer => _offer;

        private void Awake()
        {
            if (_cardRoot == null)
            {
                Transform cardLayer = transform.Find("CardLayer");
                if (cardLayer != null)
                    _cardRoot = cardLayer;
            }

            EnsureUiClickable();
        }

        public void Bind(StageRewardOffer offer, Action<StageRewardObjectUI> onClicked)
        {
            _offer = offer;
            _onClicked = onClicked;
            _bindVersion++;

            EnsureUiClickable();
            ApplyOfferVisualAsync(_bindVersion);
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (_selectedMark != null)
                _selectedMark.SetActive(selected);
        }

        protected override bool CanClick(PointerEventData eventData)
        {
            if (!base.CanClick(eventData))
                return false;

            return _offer != null;
        }

        protected override void HandleClick(PointerEventData eventData)
        {
            _onClicked?.Invoke(this);
        }

        private async void ApplyOfferVisualAsync(int version)
        {
            bool isCard = _offer != null && _offer.Kind == STAGE_REWARD_KIND.CARD;
            bool isItem = _offer != null && _offer.Kind == STAGE_REWARD_KIND.ITEM;

            if (_itemLayer != null)
                _itemLayer.gameObject.SetActive(isItem);

            // 카드/아이템 레이어를 배타적으로 노출한다.
            // _cardRoot가 루트(transform)로 폴백된 경우 오브젝트 전체 비활성화를 피한다.
            if (_cardRoot != null)
                _cardRoot.gameObject.SetActive(isCard);

            if (isItem && _offer.ItemData != null)
            {
                if (_itemIcon != null)
                    _itemIcon.sprite = _offer.ItemData.ItemIcon;

                if (_itemDescText != null)
                    _itemDescText.text = _offer.ItemData.ItemDescription;
            }
            else if (_itemDescText != null)
            {
                _itemDescText.text = string.Empty;
            }

            if (!isCard)
            {
                if (_cardObj != null)
                    _cardObj.gameObject.SetActive(false);
                return;
            }

            if (!await EnsureCardObjectAsync())
                return;

            if (version != _bindVersion)
                return;

            _cardObj.gameObject.SetActive(true);
            _cardObj.SetData(_offer.CardData);
            _cardObj.SetInteractable(false);
            EnsureUiClickable();
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

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[StageRewardObjectUI] ResourceManager를 찾을 수 없습니다.");
                return false;
            }

            Transform spawnRoot = _cardRoot != null ? _cardRoot : transform;

            _isCardLoading = true;
            GameObject instance = await resourceManager.InstantiateAsync(
                PublicVariable.Address.CardObjectPrefab,
                spawnRoot);
            _isCardLoading = false;

            if (_cardObj != null)
            {
                if (instance != null)
                    resourceManager.ReleaseInstance(instance);
                return true;
            }

            if (instance == null)
            {
                Debug.LogError(
                    $"[StageRewardObjectUI] CardObject 생성 실패: {PublicVariable.Address.CardObjectPrefab}");
                return false;
            }

            _cardInstance = instance;
            _cardObj = instance.GetComponent<InGameCardObject>();
            if (_cardObj == null)
                _cardObj = instance.GetComponentInChildren<InGameCardObject>(true);

            if (_cardObj == null)
            {
                Debug.LogError("[StageRewardObjectUI] InGameCardObject 컴포넌트가 없습니다.");
                ReleaseCardObject();
                return false;
            }

            return true;
        }

        private void ReleaseCardObject()
        {
            if (_cardInstance == null)
            {
                _cardObj = null;
                return;
            }

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager != null)
                resourceManager.ReleaseInstance(_cardInstance);
            else
                Destroy(_cardInstance);

            _cardInstance = null;
            _cardObj = null;
        }

        private void EnsureUiClickable()
        {
            var graphic = GetComponent<Graphic>();
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

            var childGraphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < childGraphics.Length; i++)
            {
                if (childGraphics[i].gameObject == gameObject)
                    continue;

                childGraphics[i].raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            _bindVersion++;
            ReleaseCardObject();
        }
    }
}
