using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 개별 스테이지 노드 UI.
    /// </summary>
    public class StageNodeObjectUI : MonoBehaviour, IPointerClickHandler
    {
        private const string AnimSelectAble = "SelectAble";
        private const string AnimNone = "None";

        [SerializeField]
        private Image _nodeIcon;

        [SerializeField]
        private Animator _animator;

        private StageNodeData _nodeData;
        private Action<int> _onClicked;
        private int _iconLoadVersion;

        public StageNodeData NodeData => _nodeData;

        public void Initialize(StageNodeData nodeData, Action<int> onClicked)
        {
            _nodeData = nodeData;
            _onClicked = onClicked;
            RefreshVisual();
        }

        public void Refresh(StageNodeData nodeData)
        {
            _nodeData = nodeData;
            RefreshVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_nodeData == null)
                return;

            _onClicked?.Invoke(_nodeData.NodeId);
        }

        private void RefreshVisual()
        {
            UpdateNodeIconAsync();
            UpdateSelectAbleAnimation();
        }

        private void UpdateSelectAbleAnimation()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();

            if (_animator == null || _nodeData == null)
                return;

            string stateName = _nodeData.IsAvailable ? AnimSelectAble : AnimNone;
            _animator.Play(stateName, 0, 0f);
        }

        private async void UpdateNodeIconAsync()
        {
            if (_nodeIcon == null || _nodeData == null)
                return;

            int version = ++_iconLoadVersion;
            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[StageNodeObjectUI] ResourceManager를 찾을 수 없습니다.");
                return;
            }

            string spriteName = _nodeData.StageType.GetSpriteName();
            Sprite sprite = await resourceManager.GetAtlasSpriteAsync(ATLAS_TYPE.UI, spriteName);

            if (version != _iconLoadVersion)
                return;

            if (sprite == null)
                return;

            if (_nodeIcon != null)
                _nodeIcon.sprite = sprite;
        }
    }
}
