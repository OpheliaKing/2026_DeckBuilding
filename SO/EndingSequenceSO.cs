using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 엔딩 UI에서 클릭할 때마다 표시할 한 장의 이미지·텍스트.
    /// </summary>
    [Serializable]
    public struct EndingSequencePage
    {
        [Tooltip("표시할 이미지 Addressables/아틀라스 경로 또는 스프라이트 이름")]
        public string ImagePath;

        [Tooltip("표시할 텍스트")]
        public string Text;
    }

    /// <summary>
    /// 유닛별 엔딩 시퀀스 데이터.
    /// </summary>
    [Serializable]
    public class EndingSequenceData
    {
        [Tooltip("해당 엔딩을 가진 유닛 tid")]
        public string UnitTid;

        [Tooltip("클릭 진행 순서대로 이미지·텍스트 페이지")]
        public List<EndingSequencePage> Pages = new();
    }

    [CreateAssetMenu(fileName = "EndingSequenceSO", menuName = "SHIN/Ending Sequence SO")]
    public class EndingSequenceSO : ScriptableObject
    {
        [SerializeField]
        private List<EndingSequenceData> _endingSequences = new();

        public IReadOnlyList<EndingSequenceData> EndingSequences => _endingSequences;
        public int Count => _endingSequences != null ? _endingSequences.Count : 0;

        public EndingSequenceData GetEndingSequence(int index)
        {
            if (_endingSequences == null || index < 0 || index >= _endingSequences.Count)
            {
                Debug.LogError($"[EndingSequenceSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _endingSequences[index];
        }

        public EndingSequenceData GetEndingSequence(string unitTid)
        {
            if (string.IsNullOrEmpty(unitTid))
            {
                Debug.LogError("[EndingSequenceSO] unitTid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _endingSequences.Count; i++)
            {
                EndingSequenceData data = _endingSequences[i];
                if (data != null && data.UnitTid == unitTid)
                    return data;
            }

            Debug.LogError($"[EndingSequenceSO] unitTid를 찾을 수 없습니다: {unitTid}");
            return null;
        }

        public bool TryGetEndingSequence(string unitTid, out EndingSequenceData data)
        {
            data = null;

            if (string.IsNullOrEmpty(unitTid) || _endingSequences == null)
                return false;

            for (int i = 0; i < _endingSequences.Count; i++)
            {
                EndingSequenceData entry = _endingSequences[i];
                if (entry != null && entry.UnitTid == unitTid)
                {
                    data = entry;
                    return true;
                }
            }

            return false;
        }
    }
}
