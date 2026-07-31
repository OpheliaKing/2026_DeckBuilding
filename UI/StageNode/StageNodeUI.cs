using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// StageManager가 넘긴 맵 데이터를 받아 노드·연결선 UI를 표시한다.
    /// </summary>
    public class StageNodeUI : UIBase
    {
        [SerializeField]
        private Transform _stageNodeRoot;

        [SerializeField]
        private ScrollRect _scrollRect;

        [SerializeField]
        private Vector2 _nodeSize = new(48f, 48f);

        [SerializeField]
        private float _spacingX = 140f;

        [SerializeField]
        private float _spacingY = 90f;

        [SerializeField]
        private float _contentPadding = 80f;

        [SerializeField]
        private Color _lineColor = new(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField]
        private float _lineThickness = 4f;

        private readonly Dictionary<int, StageNodeObjectUI> _nodeObjects = new();
        private Transform _lineRoot;
        private Action<int> _onNodeClicked;
        private StageMapData _mapData;
        private int _buildVersion;
        private bool _pendingScrollToAvailable;
        private Coroutine _scrollRoutine;

        private void OnEnable()
        {
            if (_mapData != null || _pendingScrollToAvailable)
                RequestScrollToAvailableNodes();
        }

        public void BuildMap(StageMapData mapData, Action<int> onNodeClicked, Action onComplete = null)
        {
            if (mapData == null)
            {
                Debug.LogError("[StageNodeUI] mapData가 null입니다.");
                onComplete?.Invoke();
                return;
            }

            if (_stageNodeRoot == null)
            {
                Debug.LogError("[StageNodeUI] _stageNodeRoot가 없습니다.");
                onComplete?.Invoke();
                return;
            }

            EnsureScrollRect();
            _mapData = mapData;
            _onNodeClicked = onNodeClicked;
            ClearMapVisuals();
            _onNodeClicked = onNodeClicked;
            UpdateContentSize(mapData);
            SpawnConnectionLines(mapData);
            SpawnNodeVisualsAsync(mapData, onComplete);
        }

        /// <summary>
        /// 선택 가능한 노드가 보이도록 스크롤 위치를 갱신한다.
        /// 비활성/레이아웃 미완 시에는 활성화 후 다음 프레임에 적용한다.
        /// </summary>
        public void ScrollToAvailableNodes()
        {
            RequestScrollToAvailableNodes();
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// 맵 진행 상태만 갱신한다. (노드 재생성 없음)
        /// </summary>
        public void ApplyMapProgress(StageMapData mapData)
        {
            if (mapData == null)
                return;

            _mapData = mapData;

            for (int i = 0; i < mapData.Nodes.Count; i++)
            {
                StageNodeData node = mapData.Nodes[i];
                if (!_nodeObjects.TryGetValue(node.NodeId, out StageNodeObjectUI nodeObject))
                    continue;

                nodeObject.Refresh(node);
            }

            RequestScrollToAvailableNodes();
        }

        public void ClearMap()
        {
            ClearMapVisuals();
            _onNodeClicked = null;
            _mapData = null;
            _pendingScrollToAvailable = false;
            StopScrollRoutine();
        }

        private void RequestScrollToAvailableNodes()
        {
            if (_mapData == null)
                return;

            if (!gameObject.activeInHierarchy)
            {
                _pendingScrollToAvailable = true;
                return;
            }

            _pendingScrollToAvailable = false;
            StopScrollRoutine();
            _scrollRoutine = StartCoroutine(ScrollToAvailableNodesNextFrame());
        }

        private IEnumerator ScrollToAvailableNodesNextFrame()
        {
            // ContentSizeFitter / Layout 반영 후 스크롤 적용
            yield return null;
            Canvas.ForceUpdateCanvases();
            ApplyScrollToAvailableNodes();
            // 페이드인·레이아웃 재빌드 후에도 위치가 유지되도록 한 프레임 더
            yield return null;
            Canvas.ForceUpdateCanvases();
            ApplyScrollToAvailableNodes();
            _scrollRoutine = null;
        }

        private void ApplyScrollToAvailableNodes()
        {
            if (_mapData == null)
                return;

            EnsureScrollRect();
            if (_scrollRect == null)
                return;

            float focusFloor = GetFocusFloor(_mapData);
            float maxFloor = Mathf.Max(1, _mapData.GridY - 1);
            // floor 0(아래) → 0, 보스(위) → 1. Unity verticalNormalizedPosition: 0=아래, 1=위
            float normalized = Mathf.Clamp01(focusFloor / maxFloor);

            _scrollRect.StopMovement();
            _scrollRect.verticalNormalizedPosition = normalized;
            _scrollRect.horizontalNormalizedPosition = 0.5f;
        }

        private void StopScrollRoutine()
        {
            if (_scrollRoutine == null)
                return;

            StopCoroutine(_scrollRoutine);
            _scrollRoutine = null;
        }

        private void ClearMapVisuals()
        {
            _buildVersion++;
            _nodeObjects.Clear();
            _lineRoot = null;

            if (_stageNodeRoot == null)
                return;

            for (int i = _stageNodeRoot.childCount - 1; i >= 0; i--)
                DestroyImmediateSafe(_stageNodeRoot.GetChild(i).gameObject);
        }

        private void EnsureScrollRect()
        {
            if (_scrollRect != null)
                return;

            _scrollRect = GetComponentInChildren<ScrollRect>(true);
            if (_scrollRect == null)
                Debug.LogWarning("[StageNodeUI] ScrollRect를 찾을 수 없습니다.");
        }

        private void UpdateContentSize(StageMapData mapData)
        {
            RectTransform content = ResolveContentRect();
            if (content == null)
                return;

            float width = (mapData.GridX - 1) * _spacingX + _nodeSize.x + _contentPadding * 2f;
            float height = (mapData.GridY - 1) * _spacingY + _nodeSize.y + _contentPadding * 2f;
            content.sizeDelta = new Vector2(width, height);
        }

        private RectTransform ResolveContentRect()
        {
            if (_scrollRect != null && _scrollRect.content != null)
                return _scrollRect.content;

            return _stageNodeRoot as RectTransform;
        }

        private static float GetFocusFloor(StageMapData mapData)
        {
            float sum = 0f;
            int count = 0;

            for (int i = 0; i < mapData.Nodes.Count; i++)
            {
                StageNodeData node = mapData.Nodes[i];
                if (!node.IsAvailable)
                    continue;

                sum += node.Floor;
                count++;
            }

            if (count == 0)
            {
                if (mapData.CurrentNodeId >= 0)
                {
                    for (int i = 0; i < mapData.Nodes.Count; i++)
                    {
                        if (mapData.Nodes[i].NodeId == mapData.CurrentNodeId)
                            return mapData.Nodes[i].Floor;
                    }
                }

                return 0f;
            }

            return sum / count;
        }

        private void SpawnConnectionLines(StageMapData mapData)
        {
            Vector2 origin = GetGridOrigin(mapData);
            Transform lineRoot = EnsureLineRoot();
            var nodeLookup = BuildNodeLookup(mapData);

            for (int i = 0; i < mapData.Nodes.Count; i++)
            {
                StageNodeData from = mapData.Nodes[i];
                Vector2 fromPos = GetNodePosition(from, origin);

                for (int n = 0; n < from.NextNodeIds.Count; n++)
                {
                    if (!nodeLookup.TryGetValue(from.NextNodeIds[n], out StageNodeData to))
                        continue;

                    Vector2 toPos = GetNodePosition(to, origin);
                    CreateLine(fromPos, toPos, lineRoot);
                }
            }
        }

        private async void SpawnNodeVisualsAsync(StageMapData mapData, Action onComplete)
        {
            int version = _buildVersion;
            Vector2 origin = GetGridOrigin(mapData);

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[StageNodeUI] ResourceManager를 찾을 수 없습니다.");
                onComplete?.Invoke();
                return;
            }

            for (int i = 0; i < mapData.Nodes.Count; i++)
            {
                if (version != _buildVersion)
                {
                    onComplete?.Invoke();
                    return;
                }

                StageNodeData nodeData = mapData.Nodes[i];
                GameObject go = await resourceManager.InstantiateAsync(
                    PublicVariable.Address.StageNodeObjectUIPrefab,
                    _stageNodeRoot);

                if (version != _buildVersion)
                {
                    if (go != null)
                        resourceManager.ReleaseInstance(go);
                    onComplete?.Invoke();
                    return;
                }

                if (go == null)
                {
                    Debug.LogError(
                        $"[StageNodeUI] StageNodeObjectUI 생성 실패: {PublicVariable.Address.StageNodeObjectUIPrefab}");
                    continue;
                }

                go.name = $"StageNode_F{nodeData.Floor}_S{nodeData.Slot}";
                go.transform.SetAsLastSibling();

                var rect = go.transform as RectTransform;
                if (rect != null)
                {
                    if (_nodeSize.x > 0f && _nodeSize.y > 0f)
                        rect.sizeDelta = _nodeSize;
                    rect.anchoredPosition = GetNodePosition(nodeData, origin);
                }

                var nodeObjectUI = go.GetComponent<StageNodeObjectUI>();
                if (nodeObjectUI == null)
                    nodeObjectUI = go.GetComponentInChildren<StageNodeObjectUI>(true);

                if (nodeObjectUI == null)
                {
                    Debug.LogError("[StageNodeUI] StageNodeObjectUI 컴포넌트가 없습니다.");
                    resourceManager.ReleaseInstance(go);
                    continue;
                }

                nodeObjectUI.Initialize(nodeData, HandleNodeClicked);
                _nodeObjects[nodeData.NodeId] = nodeObjectUI;
            }

            if (version == _buildVersion)
                RequestScrollToAvailableNodes();

            onComplete?.Invoke();
        }

        private void HandleNodeClicked(int nodeId)
        {
            _onNodeClicked?.Invoke(nodeId);
        }

        private static Dictionary<int, StageNodeData> BuildNodeLookup(StageMapData mapData)
        {
            var lookup = new Dictionary<int, StageNodeData>(mapData.Nodes.Count);
            for (int i = 0; i < mapData.Nodes.Count; i++)
                lookup[mapData.Nodes[i].NodeId] = mapData.Nodes[i];
            return lookup;
        }

        private Transform EnsureLineRoot()
        {
            if (_lineRoot != null)
                return _lineRoot;

            var go = new GameObject("StageLineRoot", typeof(RectTransform));
            go.transform.SetParent(_stageNodeRoot, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            _lineRoot = go.transform;
            return _lineRoot;
        }

        private void CreateLine(Vector2 from, Vector2 to, Transform parent)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.01f)
                return;

            Vector2 dir = delta / length;
            float inset = Mathf.Min(_nodeSize.x * 0.35f, length * 0.35f);
            Vector2 start = from + dir * inset;
            Vector2 end = to - dir * inset;
            Vector2 lineDelta = end - start;
            float lineLength = lineDelta.magnitude;
            if (lineLength <= 0.01f)
                return;

            float angle = Mathf.Atan2(lineDelta.y, lineDelta.x) * Mathf.Rad2Deg;

            var go = new GameObject("StageLine");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(lineLength, _lineThickness);
            rect.anchoredPosition = (start + end) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            var image = go.AddComponent<Image>();
            image.color = _lineColor;
            image.raycastTarget = false;
        }

        private Vector2 GetGridOrigin(StageMapData mapData)
        {
            float width = (mapData.GridX - 1) * _spacingX;
            float height = (mapData.GridY - 1) * _spacingY;
            return new Vector2(-width * 0.5f, -height * 0.5f);
        }

        private Vector2 GetNodePosition(StageNodeData node, Vector2 origin)
        {
            return new Vector2(
                origin.x + node.Slot * _spacingX,
                origin.y + node.Floor * _spacingY);
        }

        private static void DestroyImmediateSafe(GameObject go)
        {
            if (go == null)
                return;

            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }
    }
}
