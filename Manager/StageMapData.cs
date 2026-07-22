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
    /// 로컬 세이브용 데이터 (추후 직렬화).
    /// </summary>
    [Serializable]
    public class StageMapSaveData
    {
        public StageMapData MapData = new();
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
