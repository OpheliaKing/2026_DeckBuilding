using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 개별 스테이지 노드 UI.
    /// </summary>
    public class StageNodeObjectUI : ClickEventUI
    {
        private const string AnimSelectAble = "SelectAble";
        private const string AnimNone = "None";

        [SerializeField]
        private Image _nodeIcon;

        [SerializeField]
        private Animator _animator;

        [Header("Node State Colors")]
        [SerializeField]
        private Color _availableColor = Color.white;

        [SerializeField]
        private Color _currentColor = new Color(1f, 0.92f, 0.75f, 1f);

        [SerializeField]
        private Color _visitedColor = new Color(0.96f, 0.9f, 0.98f, 0.78f);

        [SerializeField]
        private Color _lockedColor = new Color(0.98f, 0.94f, 1f, 0.9f);

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

        protected override bool CanClick(PointerEventData eventData)
        {
            if (!base.CanClick(eventData))
                return false;

            return _nodeData != null;
        }

        protected override void HandleClick(PointerEventData eventData)
        {
            _onClicked?.Invoke(_nodeData.NodeId);
        }

        private void RefreshVisual()
        {
            UpdateNodeIconAsync();
            UpdateSelectAbleAnimation();
            UpdateStateColor();
        }

        private void UpdateStateColor()
        {
            if (_nodeIcon == null || _nodeData == null)
                return;

            if (_nodeData.IsCurrent)
                _nodeIcon.color = _currentColor;
            else if (_nodeData.IsAvailable)
                _nodeIcon.color = _availableColor;
            else if (_nodeData.IsVisited)
                _nodeIcon.color = _visitedColor;
            else
                _nodeIcon.color = _lockedColor;
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
            {
                _nodeIcon.sprite = sprite;
                UpdateStateColor();
            }
        }
    }
}
