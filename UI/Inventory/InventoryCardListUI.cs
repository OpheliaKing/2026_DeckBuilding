using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 인벤토리 카드 그리드. CardObject를 스폰해 보여주기만 한다(전투 클릭 비활성).
    /// Scroll/Grid는 프리팹에서 구성한다.
    /// </summary>
    public class InventoryCardListUI : MonoBehaviour
    {
        [SerializeField]
        private Transform _contentRoot;

        [SerializeField]
        private GridLayoutGroup _gridLayout;

        private readonly List<GameObject> _cardInstances = new();
        private int _spawnVersion;

        public void Setup(IReadOnlyList<CardData> cards)
        {
            if (_contentRoot == null)
            {
                Debug.LogError("[InventoryCardListUI] _contentRoot가 프리팹에 연결되지 않았습니다.");
                return;
            }

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
                    // Grid cellSize가 CardObject 원본 비율(2:3)을 유지하므로 추가 scale은 쓰지 않는다.
                    // (scale + 다른 비율 cell이면 CardFrame PreserveAspect와 Cost 위치가 어긋남)
                    cardRect.localScale = Vector3.one;
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
    }
}
