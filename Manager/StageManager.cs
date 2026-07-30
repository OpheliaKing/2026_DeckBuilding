using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 스테이지 맵 데이터 생성·보관·세이브/로드 및 UI 갱신 요청.
    /// </summary>
    public partial class StageManager : ManagerBase
    {
        #region Constants

        private const int GridX = 5;
        private const int GridY = 8;
        private const int MaxNodesPerFloor = 3;
        private const int MinNodesPerFloor = 3;
        private const int MinStartNodes = 2;
        private const int MinOutgoing = 1;
        private const int MaxOutgoing = 2;

        // STAGE_TYPE 배치 규칙 (층 번호는 1부터: 맨 아래=1)
        private const int MinEliteFloorNumber = 4;
        private const int MaxEliteCount = 2;
        private const int MinShopFloorNumber = 3;
        private const int MaxShopCount = 2;
        private const int MaxEventCount = 4;

        // TODO: 타입별 스테이지 풀로 교체 — StageStepDataSO 풀이 비어 있을 때만 사용
        private const string DefaultBattleStageTid = "stage_0001";
        private const int DefaultStartStepIndex = 1;

        #endregion

        #region Serialized Fields

        [SerializeField]
        private StageNodeUI _stageNodeUI;

        [SerializeField]
        private bool _initializeOnStart = false;

        [Header("Stage Clear Reward")]
        [SerializeField]
        private int _rewardOfferCount = 3;

        [Tooltip("카드가 나올 기본 확률. ProgressTables에 값이 있으면 그쪽 우선")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _defaultCardChance = 0.7f;

        [SerializeField]
        private StageRewardProgressTable[] _rewardProgressTables =
        {
            new StageRewardProgressTable
            {
                MinProgressStep = 0,
                MaxProgressStep = 2,
                CardChance = 0.75f,
                GradeWeights = new[]
                {
                    new RewardGradeWeight { Grade = ITEM_GRADE.COMMON, Weight = 80f },
                    new RewardGradeWeight { Grade = ITEM_GRADE.RARE, Weight = 20f },
                }
            },
            new StageRewardProgressTable
            {
                MinProgressStep = 3,
                MaxProgressStep = 5,
                CardChance = 0.65f,
                GradeWeights = new[]
                {
                    new RewardGradeWeight { Grade = ITEM_GRADE.COMMON, Weight = 55f },
                    new RewardGradeWeight { Grade = ITEM_GRADE.RARE, Weight = 45f },
                }
            },
            new StageRewardProgressTable
            {
                MinProgressStep = 6,
                MaxProgressStep = -1,
                CardChance = 0.55f,
                GradeWeights = new[]
                {
                    new RewardGradeWeight { Grade = ITEM_GRADE.COMMON, Weight = 35f },
                    new RewardGradeWeight { Grade = ITEM_GRADE.RARE, Weight = 65f },
                }
            },
        };

        #endregion

        #region Properties

        public StageMapData MapData => _mapData;
        public int CurrentStepIndex => _currentStepIndex;

        #endregion

        #region Fields

        private StageMapData _mapData;
        private readonly List<MapNode> _allNodes = new();
        private readonly List<MapNode>[] _nodesByFloor = new List<MapNode>[GridY];
        private int _activeBattleNodeId = -1;
        private int _currentStepIndex = DefaultStartStepIndex;

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
            EnsureMapData();
            RefreshStageNodeUI();
        }

        /// <summary>
        /// 세이브 존재 여부. (현재 미구현 — 항상 false)
        /// </summary>
        public bool HasSaveData()
        {
            return TryLoadSave(out _);
        }

        /// <summary>
        /// 세이브에서 맵을 로드한다. 성공 시 true.
        /// </summary>
        public bool TryLoadRun()
        {
            if (!TryLoadSave(out StageMapSaveData saveData))
                return false;

            if (saveData?.MapData == null || saveData.MapData.Nodes == null || saveData.MapData.Nodes.Count == 0)
                return false;

            _mapData = saveData.MapData;
            _currentStepIndex = Mathf.Max(DefaultStartStepIndex, _mapData.StepIndex);
            _activeBattleNodeId = -1;
            Debug.Log($"[StageManager] 세이브 런을 불러왔습니다. Step={_currentStepIndex}");
            return true;
        }

        /// <summary>
        /// 새 런용 맵을 생성하고 세이브한다. (캐릭터 세팅 완료 후 BootFlow에서 호출)
        /// </summary>
        public void CreateNewRun()
        {
            _activeBattleNodeId = -1;
            _stageNodeUI = null;
            _currentStepIndex = DefaultStartStepIndex;
            GenerateMap();
            SaveMapData();
            Debug.Log($"[StageManager] 새 런 맵을 생성했습니다. Step={_currentStepIndex}");
        }

        /// <summary>
        /// 다음 스테이지(스텝) 맵을 생성한다. GridY 보스 클리어 후 호출 예정.
        /// </summary>
        public bool TryAdvanceToNextStep()
        {
            int nextStep = _currentStepIndex + 1;
            if (!CanEnterStep(nextStep))
            {
                Debug.Log($"[StageManager] 다음 스텝 없음 또는 최종 스텝 도달: current={_currentStepIndex}");
                return false;
            }

            _currentStepIndex = nextStep;
            _activeBattleNodeId = -1;
            GenerateMap();
            SaveMapData();
            Debug.Log($"[StageManager] 다음 스텝 맵 생성: Step={_currentStepIndex}");
            return true;
        }

        public bool IsFinalStep()
        {
            if (TryGetStageStepDataSO(out StageStepDataSO stepSO) && stepSO.MaxStepIndex > 0)
                return _currentStepIndex >= stepSO.MaxStepIndex;

            return false;
        }

        private bool CanEnterStep(int stepIndex)
        {
            if (stepIndex < DefaultStartStepIndex)
                return false;

            if (!TryGetStageStepDataSO(out StageStepDataSO stepSO) || stepSO == null)
                return true;

            if (stepSO.MaxStepIndex <= 0)
                return true;

            return stepIndex <= stepSO.MaxStepIndex;
        }

        public void ShowStageUI()
        {
            EnsureMapData();

            UIManager uiManager = ResolveUIManager();
            if (uiManager == null)
                return;

            uiManager.Show(PublicVariable.Address.StageNodeUIPrefab, uiBase =>
            {
                if (uiBase is not StageNodeUI stageNodeUI)
                {
                    Debug.LogError("[StageManager] StageNodeUI 프리팹에 StageNodeUI 컴포넌트가 없습니다.");
                    return;
                }

                _stageNodeUI = stageNodeUI;
                stageNodeUI.BuildMap(_mapData, OnNodeClicked);
            });
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
            EnterNode(node);
        }

        /// <summary>
        /// 인게임 전투 종료 후 호출. 승리 시 클리어→리워드→StageNodeUI 플로우로 이어진다.
        /// </summary>
        public void OnBattleFinished(bool isVictory)
        {
            GameManager.Instance?.ClearInGameStage();

            if (isVictory && _activeBattleNodeId >= 0)
            {
                ApplyNodeCleared(_activeBattleNodeId);
                SaveMapData();
                StartStageClearRewardFlow();
                return;
            }

            if (!isVictory)
                Debug.Log("[StageManager] 전투 패배 - 맵 진행은 유지합니다.");

            _activeBattleNodeId = -1;
            ReturnToStageNodeUI();
        }

        #endregion

        #region Stage Clear Reward Flow

        /// <summary>
        /// 스테이지 클리어 후 플로우 진입.
        /// 클리어 → 리워드 UI 출력 → 리워드 선택 → StageNodeUI 이동
        /// </summary>
        private void StartStageClearRewardFlow()
        {
            OnStageCleared();
            ShowRewardUIAsync();
        }

        private void OnStageCleared()
        {
            Debug.Log($"[StageManager] 스테이지 클리어: nodeId={_activeBattleNodeId}");
        }

        private async void ShowRewardUIAsync()
        {
            List<StageRewardOffer> offers = await BuildRewardOffersAsync(_rewardOfferCount);
            if (offers == null || offers.Count == 0)
            {
                Debug.LogWarning("[StageManager] 보상 후보가 비어 있어 StageNodeUI로 이동합니다.");
                OnRewardSelected(null);
                return;
            }

            UIManager uiManager = ResolveUIManager();
            if (uiManager == null)
            {
                OnRewardSelected(null);
                return;
            }

            uiManager.Show(PublicVariable.Address.StageRewardUIPrefab, uiBase =>
            {
                if (uiBase is not StageRewardUI rewardUI)
                {
                    Debug.LogError("[StageManager] StageRewardUI 컴포넌트가 없습니다.");
                    OnRewardSelected(null);
                    return;
                }

                rewardUI.Setup(offers, OnRewardSelected);
            });
        }

        private async System.Threading.Tasks.Task<List<StageRewardOffer>> BuildRewardOffersAsync(int count)
        {
            var result = new List<StageRewardOffer>(count);
            var usedTids = new HashSet<string>();

            var gameManager = GameManager.Instance;
            if (gameManager == null)
                return result;

            CardDataSO cardDataSO = await gameManager.GetSOAsync<CardDataSO>(PublicVariable.Address.CardDataSO);
            ItemDataSO itemDataSO = await gameManager.GetSOAsync<ItemDataSO>(PublicVariable.Address.ItemDataSO);

            if (cardDataSO != null && !cardDataSO.IsIndexBuilt)
                cardDataSO.BuildIndex();
            if (itemDataSO != null && !itemDataSO.IsRewardIndexBuilt)
                itemDataSO.BuildRewardIndex();

            int progressStep = GetRewardProgressStep();
            StageRewardProgressTable table = ResolveRewardProgressTable(progressStep);
            CHARACTER_EQUIP_TYPE weaponType = ResolvePlayerWeaponType();

            const int maxAttemptsPerSlot = 24;
            for (int i = 0; i < count; i++)
            {
                StageRewardOffer offer = null;
                for (int attempt = 0; attempt < maxAttemptsPerSlot; attempt++)
                {
                    offer = RollSingleRewardOffer(table, weaponType, cardDataSO, itemDataSO);
                    if (offer == null)
                        continue;

                    if (!string.IsNullOrEmpty(offer.Tid) && usedTids.Contains(offer.Tid))
                        continue;

                    if (!IsValidRewardCandidate(offer))
                        continue;

                    break;
                }

                if (offer == null)
                {
                    Debug.LogWarning($"[StageManager] 보상 슬롯 {i} 생성 실패");
                    continue;
                }

                if (!string.IsNullOrEmpty(offer.Tid))
                    usedTids.Add(offer.Tid);

                result.Add(offer);
            }

            return result;
        }

        private StageRewardOffer RollSingleRewardOffer(
            StageRewardProgressTable table,
            CHARACTER_EQUIP_TYPE weaponType,
            CardDataSO cardDataSO,
            ItemDataSO itemDataSO)
        {
            float cardChance = table.CardChance > 0f ? table.CardChance : _defaultCardChance;
            bool rollCard = UnityEngine.Random.value <= cardChance;
            ITEM_GRADE grade = RollGrade(table.GradeWeights);

            if (rollCard)
            {
                if (cardDataSO != null &&
                    cardDataSO.TryGetRandomRewardCard(weaponType, grade, out CardData card) &&
                    card != null)
                {
                    return new StageRewardOffer
                    {
                        Kind = STAGE_REWARD_KIND.CARD,
                        Tid = card.Tid,
                        CardData = card,
                        Grade = grade,
                    };
                }

                // 카드 실패 시 아이템으로 폴백
            }

            if (itemDataSO != null &&
                itemDataSO.TryGetRandomRewardItem(grade, out ItemData item) &&
                item != null)
            {
                return new StageRewardOffer
                {
                    Kind = STAGE_REWARD_KIND.ITEM,
                    Tid = item.Tid,
                    ItemData = item,
                    Grade = grade,
                };
            }

            // 아이템도 실패하면 반대 타입 재시도
            if (!rollCard &&
                cardDataSO != null &&
                cardDataSO.TryGetRandomRewardCard(weaponType, grade, out CardData fallbackCard) &&
                fallbackCard != null)
            {
                return new StageRewardOffer
                {
                    Kind = STAGE_REWARD_KIND.CARD,
                    Tid = fallbackCard.Tid,
                    CardData = fallbackCard,
                    Grade = grade,
                };
            }

            return null;
        }

        /// <summary>
        /// 보유 아이템/중복 등 필터용. 추후 구현.
        /// </summary>
        private bool IsValidRewardCandidate(StageRewardOffer offer)
        {
            if (offer == null)
                return false;

            // TODO: 보유 중인 유니크 아이템, 금지 카드 등 검증
            return true;
        }

        private int GetRewardProgressStep()
        {
            if (_mapData?.Nodes == null)
                return 0;

            int clearedBattles = 0;
            for (int i = 0; i < _mapData.Nodes.Count; i++)
            {
                StageNodeData node = _mapData.Nodes[i];
                if (node == null || !node.IsVisited)
                    continue;

                if (node.StageType == STAGE_TYPE.BATTLE_NORMAL ||
                    node.StageType == STAGE_TYPE.BATTLE_ELITE ||
                    node.StageType == STAGE_TYPE.BATTLE_BOSS)
                {
                    clearedBattles++;
                }
            }

            return Mathf.Max(0, clearedBattles - 1);
        }

        private StageRewardProgressTable ResolveRewardProgressTable(int progressStep)
        {
            if (_rewardProgressTables != null)
            {
                for (int i = 0; i < _rewardProgressTables.Length; i++)
                {
                    StageRewardProgressTable table = _rewardProgressTables[i];
                    bool minOk = progressStep >= table.MinProgressStep;
                    bool maxOk = table.MaxProgressStep < 0 || progressStep <= table.MaxProgressStep;
                    if (minOk && maxOk)
                        return table;
                }
            }

            return new StageRewardProgressTable
            {
                MinProgressStep = 0,
                MaxProgressStep = -1,
                CardChance = _defaultCardChance,
                GradeWeights = new[]
                {
                    new RewardGradeWeight { Grade = ITEM_GRADE.COMMON, Weight = 70f },
                    new RewardGradeWeight { Grade = ITEM_GRADE.RARE, Weight = 30f },
                }
            };
        }

        private static ITEM_GRADE RollGrade(RewardGradeWeight[] weights)
        {
            if (weights == null || weights.Length == 0)
                return ITEM_GRADE.COMMON;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i].Weight > 0f)
                    total += weights[i].Weight;
            }

            if (total <= 0f)
                return weights[0].Grade;

            float roll = UnityEngine.Random.Range(0f, total);
            float cursor = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i].Weight <= 0f)
                    continue;

                cursor += weights[i].Weight;
                if (roll <= cursor)
                    return weights[i].Grade;
            }

            return weights[weights.Length - 1].Grade;
        }

        private static CHARACTER_EQUIP_TYPE ResolvePlayerWeaponType()
        {
            var players = GameManager.Instance?.PlayerCharacters;
            if (players == null || players.Count == 0 || players[0] == null)
                return CHARACTER_EQUIP_TYPE.NONE;

            return players[0].EquipType;
        }

        /// <summary>
        /// 리워드 선택 완료 후 플레이어에 반영하고 StageNodeUI로 이동.
        /// </summary>
        private void OnRewardSelected(StageRewardOffer selected)
        {
            if (selected != null)
                ApplySelectedReward(selected);

            var uiManager = ResolveUIManager();
            if (uiManager != null && uiManager.Current is StageRewardUI)
                uiManager.Close();

            _activeBattleNodeId = -1;
            ReturnToStageNodeUI();
        }

        private void ApplySelectedReward(StageRewardOffer selected)
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[StageManager] GameManager가 없습니다.");
                return;
            }

            var players = gameManager.PlayerCharacters;
            if (players == null || players.Count == 0 || players[0] == null)
            {
                Debug.LogError("[StageManager] 플레이어 캐릭터가 없습니다.");
                return;
            }

            UnitInfo player = players[0];

            switch (selected.Kind)
            {
                case STAGE_REWARD_KIND.CARD:
                    if (!string.IsNullOrEmpty(selected.Tid))
                        gameManager.AddCard(player, selected.Tid);
                    break;

                case STAGE_REWARD_KIND.ITEM:
                    if (selected.ItemData != null)
                        player.AddItem(selected.ItemData);
                    else if (!string.IsNullOrEmpty(selected.Tid))
                        player.AddItem(selected.Tid);
                    break;
            }

            Debug.Log($"[StageManager] 보상 적용: {selected.Kind} / {selected.Tid}");
        }

        #endregion

        #region Node Enter / Battle Flow

        private void EnterNode(StageNodeData node)
        {
            switch (node.StageType)
            {
                case STAGE_TYPE.BATTLE_NORMAL:
                case STAGE_TYPE.BATTLE_ELITE:
                case STAGE_TYPE.BATTLE_BOSS:
                    EnterBattle(node);
                    break;
                case STAGE_TYPE.SHOP:
                    EnterShop(node);
                    break;
                case STAGE_TYPE.EVENT:
                    EnterEvent(node);
                    break;
                default:
                    Debug.LogWarning($"[StageManager] 처리할 수 없는 노드 타입: {node.StageType}");
                    break;
            }
        }

        private void EnterBattle(StageNodeData node)
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[StageManager] GameManager.Instance가 없습니다.");
                return;
            }

            EnsureBattleStageTid(node);
            if (string.IsNullOrEmpty(node.StageTid))
            {
                Debug.LogError($"[StageManager] StageTid가 비어 있습니다. nodeId={node.NodeId}");
                return;
            }

            _activeBattleNodeId = node.NodeId;
            SetStageNodeUIVisible(false);
            GameManager.Instance.InGameStart(node.StageTid);
        }

        private static void EnsureBattleStageTid(StageNodeData node)
        {
            if (!string.IsNullOrEmpty(node.StageTid))
                return;

            // 맵 생성 시점 풀 배정이 실패한 노드용 폴백
            node.StageTid = DefaultBattleStageTid;
        }

        private void ApplyNodeCleared(int clearedNodeId)
        {
            StageNodeData cleared = FindNode(clearedNodeId);
            if (cleared == null)
                return;

            for (int i = 0; i < _mapData.Nodes.Count; i++)
            {
                StageNodeData node = _mapData.Nodes[i];
                node.IsAvailable = false;
                node.IsCurrent = false;
            }

            cleared.IsVisited = true;
            cleared.IsAvailable = false;
            cleared.IsCurrent = true;
            _mapData.CurrentNodeId = clearedNodeId;

            for (int i = 0; i < cleared.NextNodeIds.Count; i++)
            {
                StageNodeData next = FindNode(cleared.NextNodeIds[i]);
                if (next == null)
                    continue;

                next.IsAvailable = true;
            }
        }

        private void ReturnToStageNodeUI()
        {
            if (_stageNodeUI == null)
            {
                ShowStageUI();
                return;
            }

            SetStageNodeUIVisible(true);
            _stageNodeUI.ApplyMapProgress(_mapData);
        }

        private void SetStageNodeUIVisible(bool visible)
        {
            if (_stageNodeUI == null)
                return;

            _stageNodeUI.SetVisible(visible);
        }

        #endregion

        #region Map Data

        private void EnsureMapData()
        {
            if (_mapData != null && _mapData.Nodes != null && _mapData.Nodes.Count > 0)
                return;

            if (TryLoadSave(out StageMapSaveData saveData))
            {
                _mapData = saveData.MapData;
                _currentStepIndex = Mathf.Max(DefaultStartStepIndex, _mapData.StepIndex);
                Debug.Log($"[StageManager] 세이브 데이터를 불러왔습니다. Step={_currentStepIndex}");
                return;
            }

            GenerateMap();
            SaveMapData();
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
            AssignStageTypes(_mapData);
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
                StepIndex = _currentStepIndex,
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
                    StageType = STAGE_TYPE.NONE,
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

        /// <summary>
        /// 노드 STAGE_TYPE 배치.
        /// 시작=NORMAL, 종료=BOSS, ELITE/SHOP/EVENT는 층·개수 제한을 지킨다.
        /// </summary>
        private void AssignStageTypes(StageMapData mapData)
        {
            if (mapData?.Nodes == null || mapData.Nodes.Count == 0)
                return;

            int lastFloor = mapData.GridY - 1;
            var middleNodes = new List<StageNodeData>();

            for (int i = 0; i < mapData.Nodes.Count; i++)
            {
                StageNodeData node = mapData.Nodes[i];
                int floorNumber = node.Floor + 1;

                if (node.Floor == 0)
                {
                    node.StageType = STAGE_TYPE.BATTLE_NORMAL;
                    continue;
                }

                if (node.Floor == lastFloor)
                {
                    node.StageType = STAGE_TYPE.BATTLE_BOSS;
                    continue;
                }

                node.StageType = STAGE_TYPE.BATTLE_NORMAL;
                middleNodes.Add(node);
            }

            ShuffleList(middleNodes);

            int eliteCount = 0;
            int shopCount = 0;
            int eventCount = 0;

            for (int i = 0; i < middleNodes.Count; i++)
            {
                StageNodeData node = middleNodes[i];
                int floorNumber = node.Floor + 1;
                STAGE_TYPE picked = PickMiddleStageType(
                    floorNumber,
                    eliteCount,
                    shopCount,
                    eventCount);

                node.StageType = picked;

                if (picked == STAGE_TYPE.BATTLE_ELITE)
                    eliteCount++;
                else if (picked == STAGE_TYPE.SHOP)
                    shopCount++;
                else if (picked == STAGE_TYPE.EVENT)
                    eventCount++;
            }

            AssignBattleStageTids(mapData);
        }

        private void AssignBattleStageTids(StageMapData mapData)
        {
            if (mapData?.Nodes == null)
                return;

            int stepIndex = mapData.StepIndex > 0 ? mapData.StepIndex : _currentStepIndex;
            TryGetStageStepDataSO(out StageStepDataSO stepSO);

            for (int i = 0; i < mapData.Nodes.Count; i++)
            {
                StageNodeData node = mapData.Nodes[i];
                switch (node.StageType)
                {
                    case STAGE_TYPE.BATTLE_NORMAL:
                    case STAGE_TYPE.BATTLE_ELITE:
                    case STAGE_TYPE.BATTLE_BOSS:
                        if (!string.IsNullOrEmpty(node.StageTid))
                            break;

                        string tid = null;
                        if (stepSO != null)
                            tid = stepSO.GetRandomStageTid(stepIndex, node.StageType);

                        if (string.IsNullOrEmpty(tid))
                        {
                            Debug.LogWarning(
                                $"[StageManager] Step={stepIndex}, Type={node.StageType} 풀이 비어 기본 tid 사용");
                            tid = DefaultBattleStageTid;
                        }

                        node.StageTid = tid;
                        break;
                }
            }
        }

        private static bool TryGetStageStepDataSO(out StageStepDataSO stepSO)
        {
            stepSO = null;
            var gameManager = GameManager.Instance;
            if (gameManager == null)
                return false;

            return gameManager.TryGetSO(PublicVariable.Address.StageStepDataSO, out stepSO);
        }

        private static STAGE_TYPE PickMiddleStageType(
            int floorNumber,
            int eliteCount,
            int shopCount,
            int eventCount)
        {
            var candidates = new List<STAGE_TYPE> { STAGE_TYPE.BATTLE_NORMAL };

            if (floorNumber >= MinEliteFloorNumber && eliteCount < MaxEliteCount)
                candidates.Add(STAGE_TYPE.BATTLE_ELITE);

            if (floorNumber >= MinShopFloorNumber && shopCount < MaxShopCount)
                candidates.Add(STAGE_TYPE.SHOP);

            if (eventCount < MaxEventCount)
                candidates.Add(STAGE_TYPE.EVENT);

            return candidates[Random.Range(0, candidates.Count)];
        }

        private static void ShuffleList<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
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

        private static UIManager ResolveUIManager()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[StageManager] GameManager.Instance가 없습니다.");
                return null;
            }

            return GameManager.Instance.UIManager;
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
