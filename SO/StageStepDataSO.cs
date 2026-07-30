using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 한 스테이지(챕터) 스텝에서 등장할 전투 StageData tid 풀.
    /// </summary>
    [Serializable]
    public class StageStepEntry
    {
        [Tooltip("스테이지 번호. 1부터 시작")]
        public int StepIndex = 1;

        [Tooltip("일반 전투에 사용할 StageData.stageTid 목록")]
        public List<string> NormalStageTids = new();

        [Tooltip("엘리트 전투에 사용할 StageData.stageTid 목록")]
        public List<string> EliteStageTids = new();

        [Tooltip("보스 전투에 사용할 StageData.stageTid 목록")]
        public List<string> BossStageTids = new();
    }

    /// <summary>
    /// 스테이지(스텝)별 전투 풀. 맵 생성 시 노드 StageType에 맞는 tid를 뽑는다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageStepDataSO", menuName = "SHIN/Stage Step Data SO")]
    public class StageStepDataSO : ScriptableObject
    {
        [SerializeField]
        private List<StageStepEntry> _steps = new();

        public IReadOnlyList<StageStepEntry> Steps => _steps;
        public int Count => _steps != null ? _steps.Count : 0;

        /// <summary>
        /// 등록된 StepIndex 중 최대값. 엔딩 판정용.
        /// </summary>
        public int MaxStepIndex
        {
            get
            {
                if (_steps == null || _steps.Count == 0)
                    return 0;

                int max = 0;
                for (int i = 0; i < _steps.Count; i++)
                {
                    if (_steps[i] != null && _steps[i].StepIndex > max)
                        max = _steps[i].StepIndex;
                }

                return max;
            }
        }

        public bool TryGetStep(int stepIndex, out StageStepEntry entry)
        {
            entry = null;
            if (_steps == null || _steps.Count == 0)
                return false;

            for (int i = 0; i < _steps.Count; i++)
            {
                StageStepEntry step = _steps[i];
                if (step == null)
                    continue;

                if (step.StepIndex == stepIndex)
                {
                    entry = step;
                    return true;
                }
            }

            return false;
        }

        public StageStepEntry GetStep(int stepIndex)
        {
            if (TryGetStep(stepIndex, out StageStepEntry entry))
                return entry;

            Debug.LogError($"[StageStepDataSO] StepIndex를 찾을 수 없습니다: {stepIndex}");
            return null;
        }

        /// <summary>
        /// 해당 스텝·전투 타입 풀에서 StageData tid를 랜덤으로 하나 반환한다.
        /// 풀이 비어 있으면 null.
        /// </summary>
        public string GetRandomStageTid(int stepIndex, STAGE_TYPE stageType)
        {
            if (!TryGetStep(stepIndex, out StageStepEntry entry) || entry == null)
                return null;

            List<string> pool = ResolvePool(entry, stageType);
            return PickRandomTid(pool);
        }

        private static List<string> ResolvePool(StageStepEntry entry, STAGE_TYPE stageType)
        {
            switch (stageType)
            {
                case STAGE_TYPE.BATTLE_NORMAL:
                    return entry.NormalStageTids;
                case STAGE_TYPE.BATTLE_ELITE:
                    return entry.EliteStageTids;
                case STAGE_TYPE.BATTLE_BOSS:
                    return entry.BossStageTids;
                default:
                    return null;
            }
        }

        private static string PickRandomTid(List<string> pool)
        {
            if (pool == null || pool.Count == 0)
                return null;

            var valid = new List<string>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (!string.IsNullOrEmpty(pool[i]))
                    valid.Add(pool[i]);
            }

            if (valid.Count == 0)
                return null;

            return valid[UnityEngine.Random.Range(0, valid.Count)];
        }
    }
}
