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
        private Color _lineColor = new(0.86f, 0.62f, 0.72f, 0.85f);

        [SerializeField]
        private float _lineThickness = 4f;

        [SerializeField]
        private StageMapHudUI _hud;

        [SerializeField]
        private Button _inventoryButton;

        [Header("Map Panel (scrolls with content)")]
        [SerializeField]
        private Image _mapPanelImage;

        [SerializeField]
        private float _mapPanelExtraPadding = 56f;

        [SerializeField]
        [Tooltip("상단 보석/장식과 노드가 겹치지 않도록 위쪽만 추가 여백")]
        private float _mapPanelTopOrnamentPadding = 80f;

        [SerializeField]
        [Tooltip("하단 보석/장식과 노드가 겹치지 않도록 아래쪽만 추가 여백")]
        private float _mapPanelBottomOrnamentPadding = 80f;

        private readonly Dictionary<int, StageNodeObjectUI> _nodeObjects = new();
        private Transform _lineRoot;
        private Action<int> _onNodeClicked;
        private StageMapData _mapData;
        private int _buildVersion;
        private bool _pendingScrollToAvailable;
        private Coroutine _scrollRoutine;
        private bool _inventoryButtonBound;

        private const string MapPanelObjectName = "MapPanel";
        private const string MapPanelResourcePath = "UI/scarlet_stage_map_panel";

        private void OnEnable()
        {
            if (_mapData != null || _pendingScrollToAvailable)
                RequestScrollToAvailableNodes();

            EnsureInventoryButton();
            RefreshHud();
        }

        private void OnDisable()
        {
            UnbindInventoryButton();
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
            EnsureHud();
            EnsureInventoryButton();
            _mapData = mapData;
            _onNodeClicked = onNodeClicked;
            ClearMapVisuals();
            _onNodeClicked = onNodeClicked;
            UpdateContentSize(mapData);
            SpawnConnectionLines(mapData);
            SpawnNodeVisualsAsync(mapData, () =>
            {
                RefreshHud();
                onComplete?.Invoke();
            });
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

            RefreshHud();
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

        public void RefreshHud()
        {
            EnsureHud();
            _hud?.Refresh();
        }

        private void EnsureHud()
        {
            if (_hud != null)
            {
                _hud.EnsureBuilt(transform);
                return;
            }

            _hud = GetComponentInChildren<StageMapHudUI>(true);
            if (_hud == null)
            {
                var hudGo = new GameObject("StageMapHud", typeof(RectTransform));
                hudGo.transform.SetParent(transform, false);
                _hud = hudGo.AddComponent<StageMapHudUI>();
            }

            _hud.EnsureBuilt(transform);
        }

        public void OnClickInventory()
        {
            UIManager uiManager = GameManager.Instance?.UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[StageNodeUI] UIManager가 없습니다.");
                return;
            }

            if (uiManager.Current is InventoryUI)
                return;

            uiManager.Show(PublicVariable.Address.InventoryUIPrefab, uiBase =>
            {
                if (uiBase is not InventoryUI inventoryUI)
                {
                    Debug.LogError("[StageNodeUI] InventoryUI 컴포넌트가 없습니다.");
                    return;
                }

                inventoryUI.Setup();
            });
        }

        private void EnsureInventoryButton()
        {
            if (_inventoryButton == null)
            {
                Transform existing = transform.Find("InventoryButton");
                GameObject buttonGo;
                if (existing != null)
                {
                    buttonGo = existing.gameObject;
                }
                else
                {
                    buttonGo = new GameObject("InventoryButton", typeof(RectTransform));
                    buttonGo.transform.SetParent(transform, false);
                    buttonGo.AddComponent<Image>();
                    _inventoryButton = buttonGo.AddComponent<Button>();

                    var labelGo = new GameObject("Label", typeof(RectTransform));
                    labelGo.transform.SetParent(buttonGo.transform, false);
                    var label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
                    label.text = "인벤토리";
                    label.fontSize = 20f;
                    label.alignment = TMPro.TextAlignmentOptions.Center;
                    label.raycastTarget = false;
                    UiFont.ApplyNotoSansRegular(label);
                }

                if (_inventoryButton == null)
                    _inventoryButton = buttonGo.GetComponent<Button>();
            }

            if (_inventoryButton == null)
                return;

            ApplyInventoryButtonLayout(_inventoryButton);
            _inventoryButton.transform.SetAsLastSibling();
            BindInventoryButton();
        }

        private static void ApplyInventoryButtonLayout(Button button)
        {
            if (button == null)
                return;

            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-20f, -110f);
                // 스프라이트 비율에 맞춰 글자와 이미지가 같은 영역에 맞도록 설정
                rect.sizeDelta = new Vector2(220f, 72f);
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                // Preserve Aspect면 Image만 축소되고 Label은 Rect 전체를 써서 글자가 쏠림
                image.preserveAspect = false;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                Sprite buttonSprite = Resources.Load<Sprite>("UI/scarlet_stage_soft_button");
                if (buttonSprite != null)
                    image.sprite = buttonSprite;
                else if (image.sprite == null)
                    image.color = new Color(0.95f, 0.72f, 0.82f, 0.95f);

                button.targetGraphic = image;
            }

            Transform labelTransform = button.transform.Find("Label");
            if (labelTransform != null)
            {
                RectTransform labelRect = labelTransform as RectTransform;
                if (labelRect != null)
                {
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = Vector2.zero;
                    labelRect.offsetMax = Vector2.zero;
                    labelRect.anchoredPosition = Vector2.zero;
                }

                var label = labelTransform.GetComponent<TMPro.TextMeshProUGUI>();
                if (label != null)
                {
                    label.alignment = TMPro.TextAlignmentOptions.Center;
                    label.color = new Color(0.42f, 0.28f, 0.36f, 1f);
                }
            }
        }

        private void BindInventoryButton()
        {
            if (_inventoryButton == null || _inventoryButtonBound)
                return;

            _inventoryButton.onClick.AddListener(OnClickInventory);
            _inventoryButtonBound = true;
        }

        private void UnbindInventoryButton()
        {
            if (_inventoryButton == null || !_inventoryButtonBound)
                return;

            _inventoryButton.onClick.RemoveListener(OnClickInventory);
            _inventoryButtonBound = false;
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
            {
                Transform child = _stageNodeRoot.GetChild(i);
                if (child != null && child.name == MapPanelObjectName)
                    continue;

                DestroyImmediateSafe(child.gameObject);
            }
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
            width += _mapPanelExtraPadding * 2f;
            height += _mapPanelExtraPadding * 2f
                + _mapPanelTopOrnamentPadding
                + _mapPanelBottomOrnamentPadding;

            content.sizeDelta = new Vector2(0f, height);

            RectTransform nodeMain = _stageNodeRoot as RectTransform;
            if (nodeMain != null)
            {
                nodeMain.anchorMin = new Vector2(0.5f, 1f);
                nodeMain.anchorMax = new Vector2(0.5f, 1f);
                nodeMain.pivot = new Vector2(0.5f, 0.5f);
                nodeMain.sizeDelta = new Vector2(width, height);
                nodeMain.anchoredPosition = new Vector2(0f, -height * 0.5f);
            }

            EnsureMapPanel();
        }

        private void EnsureMapPanel()
        {
            if (_stageNodeRoot == null)
                return;

            if (_mapPanelImage == null)
            {
                Transform existing = _stageNodeRoot.Find(MapPanelObjectName);
                if (existing != null)
                    _mapPanelImage = existing.GetComponent<Image>();
            }

            if (_mapPanelImage == null)
            {
                var go = new GameObject(MapPanelObjectName, typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(_stageNodeRoot, false);
                _mapPanelImage = go.AddComponent<Image>();
            }

            RectTransform panelRect = _mapPanelImage.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                panelRect.localScale = Vector3.one;
                panelRect.SetAsFirstSibling();
            }

            if (_mapPanelImage.sprite == null)
            {
                Sprite panelSprite = Resources.Load<Sprite>(MapPanelResourcePath);
                if (panelSprite != null)
                    _mapPanelImage.sprite = panelSprite;
            }

            _mapPanelImage.type = Image.Type.Sliced;
            _mapPanelImage.preserveAspect = false;
            _mapPanelImage.color = Color.white;
            _mapPanelImage.raycastTarget = false;
            _mapPanelImage.enabled = _mapPanelImage.sprite != null;
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

            // MapPanel(0) 바로 위, 노드보다 아래
            EnsureMapPanel();
            int insertIndex = 0;
            if (_mapPanelImage != null && _mapPanelImage.transform.parent == _stageNodeRoot)
                insertIndex = _mapPanelImage.transform.GetSiblingIndex() + 1;
            go.transform.SetSiblingIndex(insertIndex);

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
            // 상단/하단 장식 여백만큼 그리드 중심을 보정
            float yShift = (_mapPanelBottomOrnamentPadding - _mapPanelTopOrnamentPadding) * 0.5f;
            return new Vector2(-width * 0.5f, -height * 0.5f + yShift);
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
