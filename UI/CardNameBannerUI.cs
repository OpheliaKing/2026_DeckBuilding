using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 카드 사용 시 화면 상단에 카드 이름을 표시하는 배너.
    /// </summary>
    public class CardNameBannerUI : MonoBehaviour
    {
        private static readonly Color NameCream = new Color(1f, 0.95f, 0.86f, 1f);
        private static readonly Color OutlineScarlet = new Color(0.42f, 0.08f, 0.14f, 0.92f);

        [SerializeField]
        private Image _bannerImage;

        [SerializeField]
        private TextMeshProUGUI _nameText;

        private CanvasGroup _canvasGroup;
        private bool _spriteLoadStarted;

        public static CardNameBannerUI Create(Transform parent)
        {
            var root = new GameObject("CardNameBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(CardNameBannerUI));
            root.layer = 5;
            var ui = root.GetComponent<CardNameBannerUI>();
            ui.Build(parent);
            return ui;
        }

        private void Build(Transform parent)
        {
            transform.SetParent(parent, false);

            var rootRect = (RectTransform)transform;
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -20f);
            // 배너 원본 2176x650 ≈ 3.35:1, 표시 크기는 기준의 약 2/3
            rootRect.sizeDelta = new Vector2(507f, 151f);

            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            var bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bannerGo.layer = 5;
            bannerGo.transform.SetParent(transform, false);
            var bannerRect = (RectTransform)bannerGo.transform;
            bannerRect.anchorMin = Vector2.zero;
            bannerRect.anchorMax = Vector2.one;
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;

            _bannerImage = bannerGo.GetComponent<Image>();
            _bannerImage.raycastTarget = false;
            _bannerImage.preserveAspect = true;
            _bannerImage.color = Color.white;

            var textGo = new GameObject("NameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.layer = 5;
            textGo.transform.SetParent(transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(48f, 24f);
            textRect.offsetMax = new Vector2(-48f, -24f);

            _nameText = textGo.GetComponent<TextMeshProUGUI>();
            _nameText.raycastTarget = false;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.enableAutoSizing = true;
            _nameText.fontSizeMin = 15f;
            _nameText.fontSizeMax = 30f;
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode = TextOverflowModes.Ellipsis;
            _nameText.color = NameCream;
            _nameText.outlineWidth = 0.18f;
            _nameText.outlineColor = OutlineScarlet;
            _nameText.text = string.Empty;

            UiFont.ApplyTitle(_nameText);
            gameObject.SetActive(false);
            EnsureBannerSprite();
        }

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            if (_nameText != null)
            {
                _nameText.color = NameCream;
                _nameText.outlineWidth = 0.18f;
                _nameText.outlineColor = OutlineScarlet;
                UiFont.ApplyTitle(_nameText);
            }

            EnsureBannerSprite();
        }

        public void Show(string cardName)
        {
            if (string.IsNullOrEmpty(cardName))
            {
                Hide();
                return;
            }

            EnsureBannerSprite();

            if (_nameText != null)
            {
                _nameText.text = cardName;
                UiFont.ApplyTitle(_nameText);
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (_nameText != null)
                _nameText.text = string.Empty;

            gameObject.SetActive(false);
        }

        private async void EnsureBannerSprite()
        {
            if (_bannerImage == null || _bannerImage.sprite != null || _spriteLoadStarted)
                return;

            _spriteLoadStarted = true;
            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                _spriteLoadStarted = false;
                return;
            }

            Sprite sprite = await resourceManager.LoadAsync<Sprite>(PublicVariable.Address.CardNameBannerSprite);
            if (_bannerImage == null)
                return;

            if (sprite != null)
                _bannerImage.sprite = sprite;
            else
                _spriteLoadStarted = false;
        }
    }
}
