using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 스테이지 맵 데이터 생성·보관·세이브/로드 및 UI 갱신 요청.
    /// </summary>
    public class StageManager : ManagerBase
    {
        #region Constants

        private const int GridX = 5;
        private const int GridY = 15;
        private const int MaxNodesPerFloor = 3;
        private const int MinNodesPerFloor = 3;
        private const int MinStartNodes = 2;
        private const int MinOutgoing = 1;
        private const int MaxOutgoing = 2;

        #endregion

        #region Serialized Fields

        [SerializeField]
        private StageNodeUI _stageNodeUI;

        [SerializeField]
        private bool _initializeOnStart = true;

        #endregion

        #region Properties

        public StageMapData MapData => _mapData;

        #endregion

        #region Fields

        private StageMapData _mapData;
        private readonly List<MapNode> _allNodes = new();
        private readonly List<MapNode>[] _nodesByFloor = new List<MapNode>[GridY];

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (_initializeOnStart)
                InitializeStageRun();
        }

        #endregion

        #region Public API

        [ContextMenu("Initialize Stage Run")]
        public void InitializeStageRun()
        {
            if (TryLoadSave(out StageMapSaveData saveData))
            {
                _mapData = saveData.MapData;
                Debug.Log("[StageManager] 세이브 데이터를 불러왔습니다.");
            }
            else
            {
                GenerateMap();
                SaveMapData();
            }

            RefreshStageNodeUI();
        }

        public void RefreshStageNodeUI()
        {
            if (_mapData == null)
            {
                Debug.LogError("[StageManager] 표시할 맵 데이터가 없습니다.");
                return;
            }

            StageNodeUI ui = ResolveStageNodeUI();
            if (ui == null)
                return;

            ui.BuildMap(_mapData, OnNodeClicked);
        }

        public void OnNodeClicked(int nodeId)
        {
            if (_mapData == null)
                return;

            StageNodeData node = FindNode(nodeId);
            if (node == null || !node.IsAvailable)
                return;

            Debug.Log($"[StageManager] 노드 클릭: id={nodeId}, tid={node.StageTid}, type={node.StageType}");
            // TODO: 이동 처리 및 스테이지 진입
        }

        #endregion

        #region Save / Load

        private bool TryLoadSave(out StageMapSaveData saveData)
        {
            saveData = null;

            // TODO: 로컬 세이브 존재 여부 확인 및 역직렬화
            return false;
        }

        private void SaveMapData()
        {
            if (_mapData == null)
                return;

            // TODO: _mapData를 로컬 세이브 파일로 저장
            Debug.Log($"[StageManager] 맵 데이터 저장 예정: 노드 {_mapData.Nodes.Count}개");
        }

        #endregion

        #region Map Generation

        private void GenerateMap()
        {
            ClearInternalMap();
            PlaceNodes();
            ConnectNodes();
            PruneUnreachableNodes();
            _mapData = BuildMapData();
        }

        private void ClearInternalMap()
        {
            _allNodes.Clear();
            for (int y = 0; y < GridY; y++)
                _nodesByFloor[y] = new List<MapNode>();
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
                    Debug.LogError($"[StageManager] {floor}층 배치 가능 슬롯이 없습니다.");
                    break;
                }

                List<int> slots;
                if (floor == 0)
                {
                    int startCount = GetNodeCountForFloor(0, GridX);
                    slots = PickDistinctSlots(startCount, GetAllSlots(), preferCenter: startCount == 1);
                }
                else if (isEndFloor)
                {
                    slots = new List<int> { PickEndSlot(_nodesByFloor[floor - 1], allowedSlots) };
                }
                else
                {
                    int nodeCount = GetNodeCountForFloor(floor, allowedSlots.Count);
                    slots = PickSlotsCoveringPrevious(_nodesByFloor[floor - 1], allowedSlots, nodeCount);
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    var node = new MapNode
                    {
                        Index = _allNodes.Count,
                        Floor = floor,
                        Slot = slots[i]
                    };
                    _allNodes.Add(node);
                    _nodesByFloor[floor].Add(node);
                }

                _nodesByFloor[floor].Sort((a, b) => a.Slot.CompareTo(b.Slot));
            }
        }

        private StageMapData BuildMapData()
        {
            var mapData = new StageMapData
            {
                GridX = GridX,
                GridY = GridY,
                CurrentNodeId = -1
            };

            for (int i = 0; i < _allNodes.Count; i++)
            {
                MapNode node = _allNodes[i];
                var nodeData = new StageNodeData
                {
                    NodeId = node.Index,
                    Floor = node.Floor,
                    Slot = node.Slot,
                    StageTid = string.Empty,
                    StageType = ResolveStageType(node),
                    IsVisited = false,
                    IsAvailable = node.Floor == 0,
                    IsCurrent = false
                };

                for (int n = 0; n < node.NextNodes.Count; n++)
                    nodeData.NextNodeIds.Add(node.NextNodes[n].Index);

                mapData.Nodes.Add(nodeData);
            }

            return mapData;
        }

        private static STAGE_TYPE ResolveStageType(MapNode node)
        {
            if (node.Floor == 0)
                return STAGE_TYPE.NONE;

            if (node.Floor == GridY - 1)
                return STAGE_TYPE.BATTLE_BOSS;

            return STAGE_TYPE.BATTLE_NORMAL;
        }

        #endregion

        #region Connection

        private void ConnectNodes()
        {
            for (int floor = 0; floor < GridY - 1; floor++)
            {
                List<MapNode> current = _nodesByFloor[floor];
                List<MapNode> next = _nodesByFloor[floor + 1];
                bool toEndFloor = floor + 1 == GridY - 1;
                ConnectFloors(current, next, toEndFloor);
            }
        }

        private static void ConnectFloors(List<MapNode> current, List<MapNode> next, bool toEndFloor)
        {
            if (toEndFloor)
            {
                MapNode end = next[0];
                for (int i = 0; i < current.Count; i++)
                    AddConnection(current[i], end);
                return;
            }

            for (int i = 0; i < current.Count; i++)
            {
                MapNode from = current[i];
                MapNode nearest = FindNearestInRange(from, next, exclude: null);
                AddConnection(from, nearest);
            }

            for (int i = 0; i < next.Count; i++)
            {
                MapNode to = next[i];
                if (HasIncoming(to, current))
                    continue;

                MapNode nearestFrom = FindNearestInRange(to, current, exclude: null);
                if (nearestFrom == null)
                    nearestFrom = FindNearestAny(to, current, exclude: null);

                AddConnection(nearestFrom, to, force: true);
            }

            for (int i = 0; i < current.Count; i++)
            {
                MapNode from = current[i];
                if (from.NextNodes.Count >= MaxOutgoing)
                    continue;

                if (Random.value > 0.55f)
                    continue;

                MapNode extra = FindNearestInRange(from, next, exclude: from.NextNodes);
                if (extra != null)
                    AddConnection(from, extra);
            }

            for (int i = 0; i < current.Count; i++)
            {
                MapNode from = current[i];
                if (from.NextNodes.Count >= MinOutgoing)
                    continue;

                MapNode nearest = FindNearestInRange(from, next, exclude: from.NextNodes);
                if (nearest == null)
                    nearest = FindNearestAny(from, next, exclude: from.NextNodes);

                AddConnection(from, nearest, force: true);
            }
        }

        private void PruneUnreachableNodes()
        {
            var reachable = new HashSet<MapNode>();
            var queue = new Queue<MapNode>();

            List<MapNode> starts = _nodesByFloor[0];
            for (int i = 0; i < starts.Count; i++)
            {
                MapNode start = starts[i];
                if (reachable.Add(start))
                    queue.Enqueue(start);
            }

            while (queue.Count > 0)
            {
                MapNode node = queue.Dequeue();
                for (int i = 0; i < node.NextNodes.Count; i++)
                {
                    MapNode next = node.NextNodes[i];
                    if (reachable.Add(next))
                        queue.Enqueue(next);
                }
            }

            for (int i = _allNodes.Count - 1; i >= 0; i--)
            {
                MapNode node = _allNodes[i];
                if (!reachable.Contains(node))
                    RemoveNode(node);
            }
        }

        private void RemoveNode(MapNode node)
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

        private static void AddConnection(MapNode from, MapNode to, bool force = false)
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

        #endregion

        #region Placement Helpers

        private static List<int> PickSlotsCoveringPrevious(
            List<MapNode> previousFloor,
            List<int> allowedSlots,
            int desiredCount)
        {
            List<int> covering = FindCoveringSlots(previousFloor, allowedSlots, MaxNodesPerFloor);
            if (covering.Count == 0)
            {
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

        private static List<int> FindCoveringSlots(List<MapNode> previousFloor, List<int> allowedSlots, int maxCount)
        {
            int limit = Mathf.Min(maxCount, allowedSlots.Count);

            for (int size = 1; size <= limit; size++)
            {
                var combo = new int[size];
                if (TryFindCoverCombo(previousFloor, allowedSlots, size, 0, 0, combo))
                    return new List<int>(combo);
            }

            return new List<int>();
        }

        private static bool TryFindCoverCombo(
            List<MapNode> previousFloor,
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

        private static bool CoversAllPrevious(List<MapNode> previousFloor, IList<int> slots)
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

        private static int PickEndSlot(List<MapNode> previousFloor, List<int> allowedSlots)
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
            if (floor == GridY - 1)
                return 1;

            int maxCount = Mathf.Min(MaxNodesPerFloor, allowedSlotCount);
            int minCount = floor == 0
                ? Mathf.Min(MinStartNodes, maxCount)
                : Mathf.Min(MinNodesPerFloor, maxCount);

            return Random.Range(minCount, maxCount + 1);
        }

        private static List<int> BuildAllowedSlotsFromPreviousFloor(List<MapNode> previousFloor)
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

        #endregion

        #region Search Helpers

        private static bool IsWithinAdjacentSlot(MapNode a, MapNode b)
        {
            return Mathf.Abs(a.Slot - b.Slot) <= 1;
        }

        private static MapNode FindNearestInRange(MapNode origin, List<MapNode> candidates, List<MapNode> exclude)
        {
            MapNode best = null;
            int bestDist = int.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                MapNode candidate = candidates[i];
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

        private static MapNode FindNearestAny(MapNode origin, List<MapNode> candidates, List<MapNode> exclude)
        {
            MapNode best = null;
            int bestDist = int.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                MapNode candidate = candidates[i];
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

        private static bool HasIncoming(MapNode target, List<MapNode> previousFloor)
        {
            for (int i = 0; i < previousFloor.Count; i++)
            {
                if (previousFloor[i].NextNodes.Contains(target))
                    return true;
            }

            return false;
        }

        private StageNodeData FindNode(int nodeId)
        {
            if (_mapData?.Nodes == null)
                return null;

            for (int i = 0; i < _mapData.Nodes.Count; i++)
            {
                if (_mapData.Nodes[i].NodeId == nodeId)
                    return _mapData.Nodes[i];
            }

            return null;
        }

        private StageNodeUI ResolveStageNodeUI()
        {
            if (_stageNodeUI == null)
                _stageNodeUI = FindObjectOfType<StageNodeUI>(true);

            if (_stageNodeUI == null)
                Debug.LogError("[StageManager] StageNodeUI를 찾을 수 없습니다.");

            return _stageNodeUI;
        }

        #endregion

        #region Internal Types

        private class MapNode
        {
            public int Index;
            public int Floor;
            public int Slot;
            public readonly List<MapNode> NextNodes = new();
            public readonly List<MapNode> PrevNodes = new();
        }

        #endregion
    }
}
