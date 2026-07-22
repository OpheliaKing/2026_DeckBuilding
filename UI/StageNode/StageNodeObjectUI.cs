using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SHIN
{
    /// <summary>
    /// 개별 스테이지 노드 UI. 추후 ResourceManager 프리팹으로 교체 가능.
    /// </summary>
    public class StageNodeObjectUI : MonoBehaviour, IPointerClickHandler
    {
        private StageNodeData _nodeData;
        private Action<int> _onClicked;

        public StageNodeData NodeData => _nodeData;

        public void Initialize(StageNodeData nodeData, Action<int> onClicked)
        {
            _nodeData = nodeData;
            _onClicked = onClicked;
        }

        public void Refresh(StageNodeData nodeData)
        {
            _nodeData = nodeData;
            // TODO: ResourceManager 프리팹 기반 아이콘/텍스트 갱신
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_nodeData == null)
                return;

            _onClicked?.Invoke(_nodeData.NodeId);
        }
    }
}
