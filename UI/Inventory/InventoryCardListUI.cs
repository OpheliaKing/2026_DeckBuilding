using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 인벤토리 카드 그리드. CardObject를 스폰해 보여주기만 한다(전투 클릭 비활성).
    /// </summary>
    public class InventoryCardListUI : MonoBehaviour
    {
        [SerializeField]
        private Transform _contentRoot;

        [SerializeField]
        private GridLayoutGroup _gridLayout;

        private readonly List<GameObject> _cardInstances = new();
        private int _spawnVersion;

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
                    : CreateChild(transform, "Viewport", out _);
                RectTransform viewportRect = viewportGo.transform as RectTransform;
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = new Vector2(8f, 8f);
                viewportRect.offsetMax = new Vector2(-8f, -8f);

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
                _gridLayout.cellSize = new Vector2(160f, 220f);
                _gridLayout.spacing = new Vector2(12f, 12f);
                _gridLayout.padding = new RectOffset(8, 8, 8, 8);
                _gridLayout.childAlignment = TextAnchor.UpperLeft;

                ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                    fitter = contentGo.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scroll.viewport = viewportRect;
                scroll.content = contentRect;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
            }
        }

        public void Setup(IReadOnlyList<CardData> cards)
        {
            EnsureBuilt(transform.parent);
            RefreshAsync(cards);
        }

        public void Clear()
        {
            _spawnVersion++;
            ReleaseAllCards();
        }

        private async void RefreshAsync(IReadOnlyList<CardData> cards)
        {
            int version = ++_spawnVersion;
            ReleaseAllCards();

            if (cards == null || cards.Count == 0)
                return;

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[InventoryCardListUI] ResourceManager가 없습니다.");
                return;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                if (version != _spawnVersion)
                    return;

                CardData card = cards[i];
                if (card == null)
                    continue;

                GameObject instance = await resourceManager.InstantiateAsync(
                    PublicVariable.Address.CardObjectPrefab,
                    _contentRoot);

                if (version != _spawnVersion)
                {
                    if (instance != null)
                        resourceManager.ReleaseInstance(instance);
                    return;
                }

                if (instance == null)
                {
                    Debug.LogError("[InventoryCardListUI] CardObject 생성 실패");
                    continue;
                }

                InGameCardObject cardView = instance.GetComponent<InGameCardObject>();
                if (cardView == null)
                    cardView = instance.GetComponentInChildren<InGameCardObject>(true);

                if (cardView == null)
                {
                    Debug.LogError("[InventoryCardListUI] InGameCardObject가 없습니다.");
                    resourceManager.ReleaseInstance(instance);
                    continue;
                }

                cardView.SetData(card);
                cardView.SetInteractable(false);

                RectTransform cardRect = instance.transform as RectTransform;
                if (cardRect != null)
                {
                    cardRect.localScale = Vector3.one * 0.85f;
                    cardRect.anchoredPosition = Vector2.zero;
                }

                _cardInstances.Add(instance);
            }
        }

        private void ReleaseAllCards()
        {
            var resourceManager = GameManager.Instance?.ResourceManager;
            for (int i = 0; i < _cardInstances.Count; i++)
            {
                GameObject go = _cardInstances[i];
                if (go == null)
                    continue;

                if (resourceManager != null)
                    resourceManager.ReleaseInstance(go);
                else
                    Destroy(go);
            }

            _cardInstances.Clear();
        }

        private void OnDestroy()
        {
            ReleaseAllCards();
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
