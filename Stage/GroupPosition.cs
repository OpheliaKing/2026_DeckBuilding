using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public class GroupPosition : MonoBehaviour
    {
        [SerializeField]
        private GROUP_POSITION_TYPE _groupPositionType;
        public GROUP_POSITION_TYPE GroupPositionType => _groupPositionType;

        [SerializeField]
        private List<Transform> _characterPositions = new();

        [SerializeField]
        [Tooltip("캐릭터 간 X 간격. 2명이면 -spacing/2 ~ +spacing/2 형태로 중앙 정렬됩니다.")]
        private float _positionSpacing = 3f;

        public IReadOnlyList<Transform> CharacterPositions => _characterPositions;
        public float PositionSpacing => _positionSpacing;

        /// <summary>
        /// 인원수에 맞게 슬롯 X를 중앙 정렬하고, 사용할 Transform들을 반환합니다.
        /// 1명: 0 / 2명: +spacing*0.5, -spacing*0.5 / 3명: +spacing, 0, -spacing ...
        /// </summary>
        public List<Transform> GetFormationSlots(int characterCount)
        {
            var result = new List<Transform>();

            if (characterCount <= 0 || _characterPositions == null || _characterPositions.Count == 0)
                return result;

            int count = Mathf.Min(characterCount, _characterPositions.Count);
            float centerIndex = (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var slot = _characterPositions[i];
                if (slot == null)
                    continue;

                // 기존 슬롯 순서(6, 3, 0, -3, -6)와 같이 앞쪽이 +X가 되도록 배치
                var localPos = slot.localPosition;
                localPos.x = (centerIndex - i) * _positionSpacing;
                slot.localPosition = localPos;

                result.Add(slot);
            }

            return result;
        }

        /// <summary>
        /// 인원수 기준 중앙 정렬 World 위치만 필요할 때 사용합니다.
        /// </summary>
        public List<Vector3> GetFormationWorldPositions(int characterCount)
        {
            var slots = GetFormationSlots(characterCount);
            var positions = new List<Vector3>(slots.Count);

            for (int i = 0; i < slots.Count; i++)
                positions.Add(slots[i].position);

            return positions;
        }
    }

    public enum GROUP_POSITION_TYPE
    {
        NONE,
        PLAYER,
        ENEMY,
    }
}
