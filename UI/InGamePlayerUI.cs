using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SHIN
{
    public class InGamePlayerUI : MonoBehaviour
    {
        [SerializeField]
        private Transform _handCardParent;

        [SerializeField]
        private float _handCardSpacing = 180f;

        [SerializeField]
        private float _handCardRightPadding = 20f;

        private UnitInfo _currentUnitInfo;
        private readonly List<GameObject> _handCardObjects = new();
        private int _refreshVersion;

        public Transform HandCardParent => _handCardParent;
        public UnitInfo CurrentUnitInfo => _currentUnitInfo;

        /// <summary>
        /// 드로우 결과와 현재 손패를 UI에 반영합니다.
        /// </summary>
        public void OnCardsDrawn(UnitInfo unitInfo, IReadOnlyList<CardData> drawnCards)
        {
            if (unitInfo == null)
            {
                Debug.LogError("[InGamePlayerUI] UnitInfo가 null입니다.");
                return;
            }

            _currentUnitInfo = unitInfo;
            RefreshHand(unitInfo.Hand);

            if (drawnCards == null || drawnCards.Count == 0)
            {
                Debug.LogWarning("[InGamePlayerUI] 이번에 뽑은 카드가 없습니다.");
                return;
            }

            Debug.Log($"[InGamePlayerUI] 드로우 {drawnCards.Count}장 / 손패 {unitInfo.Hand.Count}장");
        }

        public void RefreshHand(IReadOnlyList<CardData> hand)
        {
            if (hand == null)
            {
                Debug.LogWarning("[InGamePlayerUI] hand가 null입니다.");
                return;
            }

            if (_handCardParent == null)
            {
                Debug.LogError("[InGamePlayerUI] _handCardParent가 없습니다.");
                return;
            }

            RefreshHandAsync(hand);
        }

        private async void RefreshHandAsync(IReadOnlyList<CardData> hand)
        {
            int version = ++_refreshVersion;
            ClearHandObjects();

            if (hand.Count == 0)
                return;

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[InGamePlayerUI] ResourceManager를 찾을 수 없습니다.");
                return;
            }

            for (int i = 0; i < hand.Count; i++)
            {
                if (version != _refreshVersion)
                    return;

                var cardData = hand[i];
                if (cardData == null)
                {
                    Debug.LogWarning($"[InGamePlayerUI] hand[{i}] CardData가 null입니다.");
                    continue;
                }

                var cardObject = await resourceManager.InstantiateAsync(
                    PublicVariable.Address.CardObjectPrefab,
                    _handCardParent);

                if (version != _refreshVersion)
                {
                    if (cardObject != null)
                        resourceManager.ReleaseInstance(cardObject);
                    return;
                }

                if (cardObject == null)
                {
                    Debug.LogError("[InGamePlayerUI] CardObject 생성 실패");
                    continue;
                }

                var cardView = cardObject.GetComponent<InGameCardObject>();
                if (cardView == null)
                    cardView = cardObject.GetComponentInChildren<InGameCardObject>(true);

                if (cardView == null)
                {
                    Debug.LogError("[InGamePlayerUI] InGameCardObject 컴포넌트가 없습니다.");
                    resourceManager.ReleaseInstance(cardObject);
                    continue;
                }

                cardView.SetData(cardData);
                _handCardObjects.Add(cardObject);
                ApplyRightAlignedPosition(cardObject.transform as RectTransform, i, hand.Count);
            }
        }

        /// <summary>
        /// 부모 오른쪽 기준으로 카드를 왼쪽으로 나열합니다. (마지막 카드가 가장 오른쪽)
        /// 카드 중심이 아니라 카드 오른쪽 끝 + 여백이 부모 오른쪽에 맞도록 오프셋합니다.
        /// </summary>
        private void ApplyRightAlignedPosition(RectTransform cardRect, int index, int totalCount)
        {
            if (cardRect == null)
                return;

            cardRect.SetParent(_handCardParent, false);
            cardRect.anchorMin = new Vector2(1f, 0.5f);
            cardRect.anchorMax = new Vector2(1f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.localRotation = Quaternion.identity;
            cardRect.localScale = Vector3.one;

            float cardWidth = GetCardWidth(cardRect);
            // 피벗이 중앙이므로, 오른쪽 끝이 보이려면 반폭 + 여백만큼 왼쪽으로 이동
            float rightEdgeOffset = (cardWidth * 0.5f) + _handCardRightPadding;
            float step = Mathf.Max(_handCardSpacing, cardWidth + _handCardRightPadding);

            // index 0이 왼쪽, 마지막이 오른쪽
            float x = -rightEdgeOffset - ((totalCount - 1 - index) * step);
            cardRect.anchoredPosition = new Vector2(x, 0f);
        }

        private static float GetCardWidth(RectTransform cardRect)
        {
            float width = Mathf.Abs(cardRect.rect.width);
            if (width > 0.01f)
                return width;

            width = Mathf.Abs(cardRect.sizeDelta.x);
            if (width > 0.01f)
                return width;

            return 100f;
        }

        private void ClearHandObjects()
        {
            var resourceManager = GameManager.Instance?.ResourceManager;

            for (int i = 0; i < _handCardObjects.Count; i++)
            {
                var cardObject = _handCardObjects[i];
                if (cardObject == null)
                    continue;

                if (resourceManager != null)
                    resourceManager.ReleaseInstance(cardObject);
                else
                    Destroy(cardObject);
            }

            _handCardObjects.Clear();
        }

        public void ClearHandUI()
        {
            _refreshVersion++;
            _currentUnitInfo = null;
            ClearHandObjects();
        }

        public void SetInteractable(bool interactable)
        {
            // TODO: 카드 클릭/드래그 가능 여부
        }

        private void OnDestroy()
        {
            _refreshVersion++;
            ClearHandObjects();
        }
    }
}
