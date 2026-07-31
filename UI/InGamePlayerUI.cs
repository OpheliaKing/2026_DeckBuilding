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

        [Header("Draw Animation")]
        [SerializeField]
        [Tooltip("카드 한 장씩 UI에 올릴 때 간격(초)")]
        private float _drawCardDelay = 0.12f;

        [SerializeField]
        [Tooltip("목표 위치까지 이동하는 시간(초)")]
        private float _cardMoveDuration = 0.22f;

        [SerializeField]
        [Tooltip("드로우 시작 X 오프셋(목표 위치 기준, 오른쪽). Y는 목표와 동일하게 고정")]
        private float _drawSpawnOffsetX = 220f;

        private UnitInfo _currentUnitInfo;
        private readonly List<GameObject> _handCardObjects = new();
        private int _refreshVersion;

        /// <summary>
        /// Addressables 생성 직후 한 프레임 노출을 막기 위한 비활성 부모.
        /// </summary>
        private Transform _cardSpawnRoot;

        public Transform HandCardParent => _handCardParent;
        public UnitInfo CurrentUnitInfo => _currentUnitInfo;

        /// <summary>
        /// 드로우 결과와 현재 손패를 UI에 반영합니다.
        /// 카드는 짧은 딜레이로 한 장씩 등장하며 목표 위치로 이동합니다.
        /// </summary>
        public void OnCardsDrawn(UnitInfo unitInfo, IReadOnlyList<CardData> drawnCards)
        {
            if (unitInfo == null)
            {
                Debug.LogError("[InGamePlayerUI] UnitInfo가 null입니다.");
                return;
            }

            _currentUnitInfo = unitInfo;
            RefreshHandAsync(unitInfo.Hand, sequentialDraw: true);

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

            RefreshHandAsync(hand, sequentialDraw: false);
        }

        private async void RefreshHandAsync(IReadOnlyList<CardData> hand, bool sequentialDraw)
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

            EnsureCardSpawnRoot();

            for (int i = 0; i < hand.Count; i++)
            {
                if (version != _refreshVersion)
                    return;

                if (sequentialDraw && i > 0)
                {
                    if (!await WaitSecondsAsync(_drawCardDelay, version))
                        return;
                }

                CardData cardData = hand[i];
                if (cardData == null)
                {
                    Debug.LogWarning($"[InGamePlayerUI] hand[{i}] CardData가 null입니다.");
                    continue;
                }

                // 비활성 루트 아래 생성 → 생성 완료 프레임에도 화면에 안 보임
                GameObject cardObject = await resourceManager.InstantiateAsync(
                    PublicVariable.Address.CardObjectPrefab,
                    _cardSpawnRoot);

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

                // 핸드로 옮기기 전에 비활성 유지 (부모 활성 전환 시 노출 방지)
                cardObject.SetActive(false);

                InGameCardObject cardView = cardObject.GetComponent<InGameCardObject>();
                if (cardView == null)
                    cardView = cardObject.GetComponentInChildren<InGameCardObject>(true);

                if (cardView == null)
                {
                    Debug.LogError("[InGamePlayerUI] InGameCardObject 컴포넌트가 없습니다.");
                    resourceManager.ReleaseInstance(cardObject);
                    continue;
                }

                cardView.SetData(cardData);

                RectTransform cardRect = cardObject.transform as RectTransform;
                PrepareCardRect(cardRect);
                Canvas.ForceUpdateCanvases();

                _handCardObjects.Add(cardObject);

                int visibleCount = _handCardObjects.Count;
                Vector2 target = GetRightAlignedPosition(cardRect, visibleCount - 1, visibleCount);
                float spawnOffsetX = sequentialDraw ? _drawSpawnOffsetX : _drawSpawnOffsetX * 0.35f;
                Vector2 start = new Vector2(target.x + spawnOffsetX, target.y);

                if (cardRect != null)
                    cardRect.anchoredPosition = start;

                cardObject.SetActive(true);

                if (!await AnimateHandLayoutAsync(visibleCount, version))
                    return;
            }
        }

        private void EnsureCardSpawnRoot()
        {
            if (_cardSpawnRoot != null)
                return;

            var go = new GameObject("CardSpawnRoot", typeof(RectTransform));
            Transform parent = _handCardParent != null ? _handCardParent.parent : transform;
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            _cardSpawnRoot = go.transform;
        }

        private void PrepareCardRect(RectTransform cardRect)
        {
            if (cardRect == null)
                return;

            cardRect.SetParent(_handCardParent, false);
            cardRect.anchorMin = new Vector2(1f, 0.5f);
            cardRect.anchorMax = new Vector2(1f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.localRotation = Quaternion.identity;
            cardRect.localScale = Vector3.one;
        }

        /// <summary>
        /// 부모 오른쪽 기준으로 카드를 왼쪽으로 나열합니다. (마지막 카드가 가장 오른쪽)
        /// </summary>
        private Vector2 GetRightAlignedPosition(RectTransform cardRect, int index, int totalCount)
        {
            if (cardRect == null || totalCount <= 0)
                return Vector2.zero;

            float cardWidth = GetCardWidth(cardRect);
            float rightEdgeOffset = (cardWidth * 0.5f) + _handCardRightPadding;
            float step = Mathf.Max(_handCardSpacing, cardWidth + _handCardRightPadding);
            float x = -rightEdgeOffset - ((totalCount - 1 - index) * step);
            return new Vector2(x, 0f);
        }

        private async Task<bool> AnimateHandLayoutAsync(int totalCount, int version)
        {
            if (totalCount <= 0)
                return version == _refreshVersion;

            var moveTasks = new List<Task>(_handCardObjects.Count);
            for (int i = 0; i < _handCardObjects.Count; i++)
            {
                if (_handCardObjects[i] == null)
                    continue;

                RectTransform cardRect = _handCardObjects[i].transform as RectTransform;
                if (cardRect == null)
                    continue;

                Vector2 target = GetRightAlignedPosition(cardRect, i, totalCount);
                moveTasks.Add(MoveAnchoredPositionAsync(cardRect, target, version));
            }

            if (moveTasks.Count == 0)
                return version == _refreshVersion;

            await Task.WhenAll(moveTasks);
            return version == _refreshVersion;
        }

        private async Task MoveAnchoredPositionAsync(RectTransform rect, Vector2 target, int version)
        {
            if (rect == null)
                return;

            float duration = Mathf.Max(0.01f, _cardMoveDuration);
            Vector2 start = rect.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (version != _refreshVersion || rect == null)
                    return;

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                rect.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
                await Task.Yield();
            }

            if (version == _refreshVersion && rect != null)
                rect.anchoredPosition = target;
        }

        private async Task<bool> WaitSecondsAsync(float seconds, int version)
        {
            float end = Time.unscaledTime + Mathf.Max(0f, seconds);
            while (Time.unscaledTime < end)
            {
                if (version != _refreshVersion)
                    return false;

                await Task.Yield();
            }

            return version == _refreshVersion;
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
                GameObject cardObject = _handCardObjects[i];
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

        /// <summary>
        /// 사용한 카드 UI만 제거하고, 남은 카드가 빈자리를 채우도록 이동합니다.
        /// </summary>
        public void RemoveCardFromHand(InGameCardObject cardObject)
        {
            if (cardObject == null)
                return;

            int index = -1;
            for (int i = 0; i < _handCardObjects.Count; i++)
            {
                if (_handCardObjects[i] == null)
                    continue;

                InGameCardObject view = _handCardObjects[i].GetComponent<InGameCardObject>();
                if (view == null)
                    view = _handCardObjects[i].GetComponentInChildren<InGameCardObject>(true);

                if (view == cardObject || _handCardObjects[i] == cardObject.gameObject)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                Debug.LogWarning("[InGamePlayerUI] 제거할 카드 UI를 찾지 못했습니다.");
                return;
            }

            RemoveCardAtIndexAndCompact(index);
        }

        /// <summary>
        /// CardData 참조로 손패 UI에서 첫 매칭 카드를 제거하고 자리를 채웁니다.
        /// </summary>
        public void RemoveCardFromHand(CardData cardData)
        {
            if (cardData == null)
                return;

            for (int i = 0; i < _handCardObjects.Count; i++)
            {
                if (_handCardObjects[i] == null)
                    continue;

                InGameCardObject view = _handCardObjects[i].GetComponent<InGameCardObject>();
                if (view == null)
                    view = _handCardObjects[i].GetComponentInChildren<InGameCardObject>(true);

                if (view != null && view.CardData == cardData)
                {
                    RemoveCardAtIndexAndCompact(i);
                    return;
                }
            }

            Debug.LogWarning($"[InGamePlayerUI] CardData에 해당하는 UI를 찾지 못했습니다: {cardData.Tid}");
        }

        private void RemoveCardAtIndexAndCompact(int index)
        {
            if (index < 0 || index >= _handCardObjects.Count)
                return;

            GameObject cardObject = _handCardObjects[index];
            _handCardObjects.RemoveAt(index);

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (cardObject != null)
            {
                if (resourceManager != null)
                    resourceManager.ReleaseInstance(cardObject);
                else
                    Destroy(cardObject);
            }

            int version = ++_refreshVersion;
            CompactHandLayoutAsync(version);
        }

        private async void CompactHandLayoutAsync(int version)
        {
            if (_handCardObjects.Count == 0)
                return;

            await AnimateHandLayoutAsync(_handCardObjects.Count, version);
        }

        public void SetInteractable(bool interactable)
        {
            for (int i = 0; i < _handCardObjects.Count; i++)
            {
                if (_handCardObjects[i] == null)
                    continue;

                InGameCardObject cardView = _handCardObjects[i].GetComponent<InGameCardObject>();
                if (cardView == null)
                    cardView = _handCardObjects[i].GetComponentInChildren<InGameCardObject>(true);

                cardView?.SetInteractable(interactable);
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            _refreshVersion++;
            ClearHandObjects();
        }
    }
}
