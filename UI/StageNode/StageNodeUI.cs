using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private Vector2 _nodeSize = new(48f, 48f);

        [SerializeField]
        private float _spacingX = 140f;

        [SerializeField]
        private float _spacingY = 90f;

        [SerializeField]
        private Color _lineColor = new(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField]
        private float _lineThickness = 4f;

        private readonly Dictionary<int, StageNodeObjectUI> _nodeObjects = new();
        private Transform _lineRoot;
        private Action<int> _onNodeClicked;
        private int _buildVersion;

        public void BuildMap(StageMapData mapData, Action<int> onNodeClicked)
        {
            if (mapData == null)
            {
                Debug.LogError("[StageNodeUI] mapData가 null입니다.");
                return;
            }

            if (_stageNodeRoot == null)
            {
                Debug.LogError("[StageNodeUI] _stageNodeRoot가 없습니다.");
                return;
            }

            _onNodeClicked = onNodeClicked;
            ClearMapVisuals();
            _onNodeClicked = onNodeClicked;
            SpawnConnectionLines(mapData);
            SpawnNodeVisualsAsync(mapData);
        }

        public void ClearMap()
        {
            ClearMapVisuals();
            _onNodeClicked = null;
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

        private async void SpawnNodeVisualsAsync(StageMapData mapData)
        {
            int version = _buildVersion;
            Vector2 origin = GetGridOrigin(mapData);

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[StageNodeUI] ResourceManager를 찾을 수 없습니다.");
                return;
            }

            for (int i = 0; i < mapData.Nodes.Count; i++)
            {
                if (version != _buildVersion)
                    return;

                StageNodeData nodeData = mapData.Nodes[i];
                GameObject go = await resourceManager.InstantiateAsync(
                    PublicVariable.Address.StageNodeObjectUIPrefab,
                    _stageNodeRoot);

                if (version != _buildVersion)
                {
                    if (go != null)
                        resourceManager.ReleaseInstance(go);
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
