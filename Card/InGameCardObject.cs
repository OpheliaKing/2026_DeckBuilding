using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SHIN
{
    public class InGameCardObject : ClickEventUI
    {
        [SerializeField]
        private TextMeshProUGUI _cardNameText;
        [SerializeField]
        private TextMeshProUGUI _cardDescriptionText;
        [SerializeField]
        private TextMeshProUGUI _cardCostText;
        [SerializeField]
        private Image _cardIllustrationImage;

        private CardData _cardData;
        private bool _interactable = true;
        private int _illustLoadVersion;

        public CardData CardData => _cardData;
        public bool Interactable => _interactable;

        private void Awake()
        {
            EnsureUiClickable();
        }

        public void SetData(CardData cardData)
        {
            _cardData = cardData;

            if (_cardNameText != null)
                _cardNameText.text = cardData != null ? cardData.Name : string.Empty;

            if (_cardDescriptionText != null)
                _cardDescriptionText.text = cardData != null ? cardData.Description : string.Empty;

            if (_cardCostText != null)
                _cardCostText.text = cardData != null ? cardData.Cost.ToString() : string.Empty;

            RefreshIllustrationAsync(cardData != null ? cardData.IllustrationPath : null);
            EnsureUiClickable();
        }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
        }

        protected override bool CanClick(PointerEventData eventData)
        {
            if (!base.CanClick(eventData))
                return false;

            return _interactable;
        }

        protected override void HandleClick(PointerEventData eventData)
        {
            OnClickCard();
        }

        public void OnClickCard()
        {
            if (!_interactable)
                return;

            if (_cardData == null)
            {
                Debug.LogWarning("[InGameCardObject] CardData가 없습니다.");
                return;
            }

            var inGameManager = GameManager.Instance?.InGameManager;
            if (inGameManager == null)
            {
                Debug.LogError("[InGameCardObject] InGameManager를 찾을 수 없습니다.");
                return;
            }

            inGameManager.OnCardClicked(this);
        }

        private async void RefreshIllustrationAsync(string illustrationPath)
        {
            if (_cardIllustrationImage == null)
                return;

            int version = ++_illustLoadVersion;

            if (string.IsNullOrEmpty(illustrationPath))
            {
                _cardIllustrationImage.sprite = null;
                _cardIllustrationImage.enabled = false;
                return;
            }

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[InGameCardObject] ResourceManager를 찾을 수 없습니다.");
                return;
            }

            Sprite sprite = await resourceManager.LoadAsync<Sprite>(illustrationPath);
            if (version != _illustLoadVersion)
                return;

            if (sprite == null)
            {
                _cardIllustrationImage.sprite = null;
                _cardIllustrationImage.enabled = false;
                return;
            }

            _cardIllustrationImage.enabled = true;
            _cardIllustrationImage.sprite = sprite;
            _cardIllustrationImage.preserveAspect = true;
            _cardIllustrationImage.color = Color.white;
        }

        /// <summary>
        /// UI 레이캐스트로 클릭을 받을 수 있게 Graphic을 보장하고,
        /// 자식 텍스트가 클릭을 가로채지 않게 합니다.
        /// </summary>
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
    }
}
