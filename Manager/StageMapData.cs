using System;
using System.Collections.Generic;

namespace SHIN
{
    [Serializable]
    public class StageMapData
    {
        public int GridX;
        public int GridY;
        public int CurrentNodeId = -1;
        public List<StageNodeData> Nodes = new();
    }

    [Serializable]
    public class StageNodeData
    {
        public int NodeId;
        public int Floor;
        public int Slot;
        public string StageTid;
        public STAGE_TYPE StageType;
        public List<int> NextNodeIds = new();
        public bool IsVisited;
        public bool IsAvailable;
        public bool IsCurrent;
    }

    /// <summary>
    /// 로컬 세이브용 런 데이터 (맵 + 플레이어 로드아웃을 한 세트로 저장).
    /// </summary>
    [Serializable]
    public class StageMapSaveData
    {
        public StageMapData MapData = new();

        // TODO: 런 단위 플레이어 스냅샷 (유닛/장비/덱/아이템)
        // public List<PlayerRunSaveData> Players = new();
    }

    public enum STAGE_TYPE
    {
        NONE,
        BATTLE_NORMAL,
        BATTLE_ELITE,
        BATTLE_BOSS,
        SHOP,
        EVENT,
    }
}
