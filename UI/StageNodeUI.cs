using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 스테이지 선택용 축소 노드맵 생성.
    /// 그리드: X 5슬롯 × Y 15층. 시작 2~3 / 층당 최대 3노드, 종료 1노드. 다음 층으로 1~2개 연결.
    /// </summary>
    public class StageNodeUI : UIBase
    {
        private const int GridX = 5;
        private const int GridY = 15;
        private const int MaxNodesPerFloor = 3;
        private const int MinStartNodes = 2;
        private const int MinOutgoing = 1;
        private const int MaxOutgoing = 2;

        [SerializeField]
        private Transform _stageNodeRoot;

        [SerializeField]
        private Vector2 _nodeSize = new(48f, 48f);

        [SerializeField]
        private float _spacingX = 140f;

        [SerializeField]
        private float _spacingY = 90f;

        [SerializeField]
        private Color _nodeColor = new(0.85f, 0.85f, 0.85f, 1f);

        [SerializeField]
        private Color _startNodeColor = new(0.45f, 0.85f, 0.55f, 1f);

        [SerializeField]
        private Color _endNodeColor = new(0.9f, 0.4f, 0.4f, 1f);

        [SerializeField]
        private Color _lineColor = new(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField]
        private float _lineThickness = 4f;

        [SerializeField]
        private bool _generateOnStart = true;

        private readonly List<StageNode> _allNodes = new();
        private readonly List<StageNode>[] _nodesByFloor = new List<StageNode>[GridY];
        private Transform _lineRoot;

        public IReadOnlyList<StageNode> AllNodes => _allNodes;

        private void Start()
        {
            if (_generateOnStart)
                GenerateMap();
        }

        [ContextMenu("Generate Map")]
        public void GenerateMap()
        {
            if (_stageNodeRoot == null)
            {
                Debug.LogError("[StageNodeUI] _stageNodeRoot가 없습니다.");
                return;
            }

            ClearMap();
            PlaceNodes();
            ConnectNodes();
            PruneUnreachableNodes();
            SpawnConnectionLines();
            SpawnNodeVisuals();
        }

        public void ClearMap()
        {
            _allNodes.Clear();
            for (int y = 0; y < GridY; y++)
                _nodesByFloor[y] = new List<StageNode>();

            if (_stageNodeRoot == null)
                return;

            _lineRoot = null;

            for (int i = _stageNodeRoot.childCount - 1; i >= 0; i--)
                DestroyImmediateSafe(_stageNodeRoot.GetChild(i).gameObject);
        }

        private void PlaceNodes()
        {
            for (int floor = 0; floor < GridY; floor++)
            {
                bool isEndFloor = floor == GridY - 1;
                List<int> allowedSlots = floor == 0
                    ? GetAllSlots()
                    : BuildAllowedSlotsFromPreviousFloor(_nodesByFloor[floor - 1]);

                if (allowedSlots.Count == 0)
                {
                    Debug.LogError($"[StageNodeUI] {floor}층 배치 가능 슬롯이 없습니다.");
                    break;
                }

                List<int> slots;
                if (floor == 0)
                {
                    // 시작 지점은 1~3개
                    int startCount = GetNodeCountForFloor(0, GridX);
                    slots = PickDistinctSlots(startCount, GetAllSlots(), preferCenter: startCount == 1);
                }
                else if (isEndFloor)
                {
                    // 종료 지점은 항상 1개
                    slots = new List<int> { PickEndSlot(_nodesByFloor[floor - 1], allowedSlots) };
                }
                else
                {
                    int nodeCount = GetNodeCountForFloor(floor, allowedSlots.Count);
                    slots = PickSlotsCoveringPrevious(
                        _nodesByFloor[floor - 1],
                        allowedSlots,
                        nodeCount);
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    var node = new StageNode
                    {
                        Floor = floor,
                        Slot = slots[i],
                        Index = _allNodes.Count
                    };
                    _allNodes.Add(node);
                    _nodesByFloor[floor].Add(node);
                }

                _nodesByFloor[floor].Sort((a, b) => a.Slot.CompareTo(b.Slot));
            }
        }

        /// <summary>
        /// 이전 층 모든 노드가 x±1로 이어질 수 있는 슬롯을 우선 확보한 뒤, 원하는 개수까지 채움.
        /// </summary>
        private static List<int> PickSlotsCoveringPrevious(
            List<StageNode> previousFloor,
            List<int> allowedSlots,
            int desiredCount)
        {
            List<int> covering = FindCoveringSlots(previousFloor, allowedSlots, MaxNodesPerFloor);
            if (covering.Count == 0)
            {
                // 최후: 이전 노드 본인 슬롯 기준으로 강제
                covering = new List<int>();
                for (int i = 0; i < previousFloor.Count && covering.Count < MaxNodesPerFloor; i++)
                {
                    int slot = Mathf.Clamp(previousFloor[i].Slot, 0, GridX - 1);
                    if (!covering.Contains(slot))
                        covering.Add(slot);
                }
            }

            int targetCount = Mathf.Clamp(desiredCount, covering.Count, Mathf.Min(MaxNodesPerFloor, allowedSlots.Count));
            var pool = new List<int>(allowedSlots);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            for (int i = 0; i < pool.Count && covering.Count < targetCount; i++)
            {
                if (!covering.Contains(pool[i]))
                    covering.Add(pool[i]);
            }

            covering.Sort();
            return covering;
        }

        /// <summary>
        /// 이전 층 전체를 커버하는 슬롯 조합을 찾는다 (최대 maxCount개).
        /// </summary>
        private static List<int> FindCoveringSlots(
            List<StageNode> previousFloor,
            List<int> allowedSlots,
            int maxCount)
        {
            int n = allowedSlots.Count;
            int limit = Mathf.Min(maxCount, n);

            for (int size = 1; size <= limit; size++)
            {
                var combo = new int[size];
                if (TryFindCoverCombo(previousFloor, allowedSlots, size, 0, 0, combo))
                    return new List<int>(combo);
            }

            return new List<int>();
        }

        private static bool TryFindCoverCombo(
            List<StageNode> previousFloor,
            List<int> allowedSlots,
            int size,
            int start,
            int depth,
            int[] combo)
        {
            if (depth == size)
                return CoversAllPrevious(previousFloor, combo);

            for (int i = start; i < allowedSlots.Count; i++)
            {
                combo[depth] = allowedSlots[i];
                if (TryFindCoverCombo(previousFloor, allowedSlots, size, i + 1, depth + 1, combo))
                    return true;
            }

            return false;
        }

        private static bool CoversAllPrevious(List<StageNode> previousFloor, IList<int> slots)
        {
            for (int p = 0; p < previousFloor.Count; p++)
            {
                int prevSlot = previousFloor[p].Slot;
                bool covered = false;
                for (int s = 0; s < slots.Count; s++)
                {
                    if (Mathf.Abs(slots[s] - prevSlot) <= 1)
                    {
                        covered = true;
                        break;
                    }
                }

                if (!covered)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 종료 노드 슬롯: 이전 층 슬롯들의 중앙값에 가장 가까운 허용 슬롯.
        /// </summary>
        private static int PickEndSlot(List<StageNode> previousFloor, List<int> allowedSlots)
        {
            if (allowedSlots.Count == 1)
                return allowedSlots[0];

            var prevSlots = new List<int>(previousFloor.Count);
            for (int i = 0; i < previousFloor.Count; i++)
                prevSlots.Add(previousFloor[i].Slot);
            prevSlots.Sort();

            int median = prevSlots[prevSlots.Count / 2];

            int best = allowedSlots[0];
            int bestDist = Mathf.Abs(best - median);
            for (int i = 1; i < allowedSlots.Count; i++)
            {
                int dist = Mathf.Abs(allowedSlots[i] - median);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = allowedSlots[i];
                }
            }

            return best;
        }

        private static int GetNodeCountForFloor(int floor, int allowedSlotCount)
        {
            // 종료 층만 단일 노드. 시작은 MinStartNodes~Max, 중간은 1~3개
            if (floor == GridY - 1)
                return 1;

            int maxCount = Mathf.Min(MaxNodesPerFloor, allowedSlotCount);
            int minCount = floor == 0
                ? Mathf.Min(MinStartNodes, maxCount)
                : 1;

            return Random.Range(minCount, maxCount + 1);
        }

        /// <summary>
        /// 이전 층 각 노드의 X 기준 x-1, x, x+1 합집합.
        /// </summary>
        private static List<int> BuildAllowedSlotsFromPreviousFloor(List<StageNode> previousFloor)
        {
            var allowed = new HashSet<int>();
            for (int i = 0; i < previousFloor.Count; i++)
            {
                int x = previousFloor[i].Slot;
                for (int offset = -1; offset <= 1; offset++)
                {
                    int slot = x + offset;
                    if (slot >= 0 && slot < GridX)
                        allowed.Add(slot);
                }
            }

            var result = new List<int>(allowed);
            result.Sort();
            return result;
        }

        private static List<int> GetAllSlots()
        {
            var slots = new List<int>(GridX);
            for (int x = 0; x < GridX; x++)
                slots.Add(x);
            return slots;
        }

        private static List<int> PickDistinctSlots(int count, List<int> allowedSlots, bool preferCenter)
        {
            count = Mathf.Clamp(count, 1, allowedSlots.Count);

            if (count == 1 && preferCenter)
                return new List<int> { GridX / 2 };

            if (count == 1)
                return new List<int> { allowedSlots[Random.Range(0, allowedSlots.Count)] };

            var pool = new List<int>(allowedSlots);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            var result = pool.GetRange(0, count);
            result.Sort();
            return result;
        }

        private void ConnectNodes()
        {
            for (int floor = 0; floor < GridY - 1; floor++)
            {
                List<StageNode> current = _nodesByFloor[floor];
                List<StageNode> next = _nodesByFloor[floor + 1];
                bool toEndFloor = floor + 1 == GridY - 1;
                ConnectFloors(current, next, toEndFloor);
            }
        }

        private static void ConnectFloors(List<StageNode> current, List<StageNode> next, bool toEndFloor)
        {
            // 종료 층은 노드 1개이므로 모든 이전 노드를 그 한 지점으로 연결
            if (toEndFloor)
            {
                StageNode end = next[0];
                for (int i = 0; i < current.Count; i++)
                    AddConnection(current[i], end);
                return;
            }

            // 1) 각 현재 노드에서 x±1 범위 내 가장 가까운 다음 노드로 최소 1연결
            for (int i = 0; i < current.Count; i++)
            {
                StageNode from = current[i];
                StageNode nearest = FindNearestInRange(from, next, exclude: null);
                AddConnection(from, nearest);
            }

            // 2) 고아(진입 없음) 다음 노드를 이전 층에 연결 (x±1 우선, 필요 시 강제)
            for (int i = 0; i < next.Count; i++)
            {
                StageNode to = next[i];
                if (HasIncoming(to, current))
                    continue;

                StageNode nearestFrom = FindNearestInRange(to, current, exclude: null);
                if (nearestFrom == null)
                    nearestFrom = FindNearestAny(to, current, exclude: null);

                AddConnection(nearestFrom, to, force: true);
            }

            // 3) 일부 노드에 두 번째 연결 추가 (최대 2개, x±1만)
            for (int i = 0; i < current.Count; i++)
            {
                StageNode from = current[i];
                if (from.NextNodes.Count >= MaxOutgoing)
                    continue;

                if (Random.value > 0.55f)
                    continue;

                StageNode extra = FindNearestInRange(from, next, exclude: from.NextNodes);
                if (extra != null)
                    AddConnection(from, extra);
            }

            // 4) 연결이 0개인 노드는 없어야 함 (x±1 우선, 없으면 최단 연결)
            for (int i = 0; i < current.Count; i++)
            {
                StageNode from = current[i];
                if (from.NextNodes.Count >= MinOutgoing)
                    continue;

                StageNode nearest = FindNearestInRange(from, next, exclude: from.NextNodes);
                if (nearest == null)
                    nearest = FindNearestAny(from, next, exclude: from.NextNodes);

                AddConnection(from, nearest, force: true);
            }
        }

        /// <summary>
        /// 시작 층에서 도달할 수 없는 노드(진입 경로 없음)를 제거한다.
        /// </summary>
        private void PruneUnreachableNodes()
        {
            var reachable = new HashSet<StageNode>();
            var queue = new Queue<StageNode>();

            List<StageNode> starts = _nodesByFloor[0];
            for (int i = 0; i < starts.Count; i++)
            {
                StageNode start = starts[i];
                if (reachable.Add(start))
                    queue.Enqueue(start);
            }

            while (queue.Count > 0)
            {
                StageNode node = queue.Dequeue();
                for (int i = 0; i < node.NextNodes.Count; i++)
                {
                    StageNode next = node.NextNodes[i];
                    if (reachable.Add(next))
                        queue.Enqueue(next);
                }
            }

            for (int i = _allNodes.Count - 1; i >= 0; i--)
            {
                StageNode node = _allNodes[i];
                if (!reachable.Contains(node))
                    RemoveNode(node);
            }
        }

        private void RemoveNode(StageNode node)
        {
            if (node == null)
                return;

            for (int i = node.PrevNodes.Count - 1; i >= 0; i--)
                node.PrevNodes[i].NextNodes.Remove(node);

            for (int i = node.NextNodes.Count - 1; i >= 0; i--)
                node.NextNodes[i].PrevNodes.Remove(node);

            node.PrevNodes.Clear();
            node.NextNodes.Clear();

            _allNodes.Remove(node);
            if (node.Floor >= 0 && node.Floor < GridY)
                _nodesByFloor[node.Floor].Remove(node);
        }

        private static StageNode FindNearestAny(StageNode origin, List<StageNode> candidates, List<StageNode> exclude)
        {
            StageNode best = null;
            int bestDist = int.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                StageNode candidate = candidates[i];
                if (exclude != null && exclude.Contains(candidate))
                    continue;

                int dist = Mathf.Abs(origin.Slot - candidate.Slot);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
                else if (dist == bestDist && best != null && Random.value < 0.5f)
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static bool IsWithinAdjacentSlot(StageNode a, StageNode b)
        {
            return Mathf.Abs(a.Slot - b.Slot) <= 1;
        }

        private static StageNode FindNearestInRange(StageNode origin, List<StageNode> candidates, List<StageNode> exclude)
        {
            StageNode best = null;
            int bestDist = int.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                StageNode candidate = candidates[i];
                if (exclude != null && exclude.Contains(candidate))
                    continue;

                if (!IsWithinAdjacentSlot(origin, candidate))
                    continue;

                int dist = Mathf.Abs(origin.Slot - candidate.Slot);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
                else if (dist == bestDist && best != null && Random.value < 0.5f)
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static bool HasIncoming(StageNode target, List<StageNode> previousFloor)
        {
            for (int i = 0; i < previousFloor.Count; i++)
            {
                if (previousFloor[i].NextNodes.Contains(target))
                    return true;
            }

            return false;
        }

        private static void AddConnection(StageNode from, StageNode to, bool force = false)
        {
            if (from == null || to == null)
                return;

            if (from.NextNodes.Contains(to))
                return;

            if (!force && from.NextNodes.Count >= MaxOutgoing)
                return;

            from.NextNodes.Add(to);
            to.PrevNodes.Add(from);
        }

        private void SpawnConnectionLines()
        {
            Vector2 origin = GetGridOrigin();
            Transform lineRoot = EnsureLineRoot();

            for (int i = 0; i < _allNodes.Count; i++)
            {
                StageNode from = _allNodes[i];
                Vector2 fromPos = GetNodePosition(from, origin);

                for (int n = 0; n < from.NextNodes.Count; n++)
                {
                    StageNode to = from.NextNodes[n];
                    Vector2 toPos = GetNodePosition(to, origin);
                    CreateLine(fromPos, toPos, lineRoot);
                }
            }
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

            // 노드 중심을 조금 비워 선이 노드에 가려지지 않게 함
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

        private void SpawnNodeVisuals()
        {
            Vector2 origin = GetGridOrigin();

            for (int i = 0; i < _allNodes.Count; i++)
            {
                StageNode node = _allNodes[i];
                GameObject go = new($"StageNode_F{node.Floor}_S{node.Slot}");
                go.transform.SetParent(_stageNodeRoot, false);
                go.transform.SetAsLastSibling();

                var rect = go.AddComponent<RectTransform>();
                rect.sizeDelta = _nodeSize;
                rect.anchoredPosition = GetNodePosition(node, origin);

                var image = go.AddComponent<Image>();
                image.color = GetNodeColor(node);
                image.raycastTarget = true;

                node.Rect = rect;
                node.Image = image;
            }
        }

        private Color GetNodeColor(StageNode node)
        {
            if (node.Floor == 0)
                return _startNodeColor;
            if (node.Floor == GridY - 1)
                return _endNodeColor;
            return _nodeColor;
        }

        private Vector2 GetGridOrigin()
        {
            float width = (GridX - 1) * _spacingX;
            float height = (GridY - 1) * _spacingY;
            return new Vector2(-width * 0.5f, -height * 0.5f);
        }

        private Vector2 GetNodePosition(StageNode node, Vector2 origin)
        {
            // Y층이 진행 방향(아래→위), X슬롯이 가로 분기
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

    public class StageNode
    {
        public int Index;
        public int Floor;
        public int Slot;
        public readonly List<StageNode> NextNodes = new();
        public readonly List<StageNode> PrevNodes = new();
        public RectTransform Rect;
        public Image Image;
    }
}
