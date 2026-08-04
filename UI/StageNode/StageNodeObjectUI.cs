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

        [Header("Icon Colors")]
        [SerializeField]
        private Color _availableIconColor = Color.white;

        [SerializeField]
        private Color _currentIconColor = new Color(1f, 0.93f, 0.72f, 1f);

        [SerializeField]
        private Color _visitedIconColor = new Color(1f, 0.95f, 0.98f, 0.85f);

        [SerializeField]
        private Color _lockedIconColor = new Color(1f, 0.98f, 1f, 1f);

        [Header("Locked Icon Outline")]
        [SerializeField]
        private Color _lockedOutlineColor = new Color(1f, 0.96f, 1f, 1f);

        [SerializeField]
        private Vector2 _lockedOutlineDistance = new Vector2(1.4f, -1.4f);

        private StageNodeData _nodeData;
        private Action<int> _onClicked;
        private int _iconLoadVersion;
        private Outline _iconOutline;

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
            UpdateStateColor();
            UpdateSelectAbleAnimation();
        }

        private void OnEnable()
        {
            // Addressables 생성 직후/재활성화 시에도 선택 가능 애니 재적용
            if (_nodeData != null)
                UpdateSelectAbleAnimation();
        }

        private void EnsureIconOutline()
        {
            if (_nodeIcon == null)
                return;

            if (_iconOutline == null)
                _iconOutline = _nodeIcon.GetComponent<Outline>();

            if (_iconOutline == null)
                _iconOutline = _nodeIcon.gameObject.AddComponent<Outline>();
        }

        private void UpdateStateColor()
        {
            if (_nodeIcon == null || _nodeData == null)
                return;

            bool isLocked = !_nodeData.IsCurrent && !_nodeData.IsAvailable && !_nodeData.IsVisited;

            if (_nodeData.IsCurrent)
                _nodeIcon.color = _currentIconColor;
            else if (_nodeData.IsAvailable)
                _nodeIcon.color = _availableIconColor;
            else if (_nodeData.IsVisited)
                _nodeIcon.color = _visitedIconColor;
            else
                _nodeIcon.color = _lockedIconColor;

            EnsureIconOutline();
            if (_iconOutline != null)
            {
                // 갈 수 없는 곳은 밝은 아이콘 + 아웃라인으로만 가시성 확보 (노드 색 유지)
                _iconOutline.enabled = isLocked;
                if (isLocked)
                {
                    _iconOutline.effectColor = _lockedOutlineColor;
                    _iconOutline.effectDistance = _lockedOutlineDistance;
                    _iconOutline.useGraphicAlpha = true;
                }
            }
        }

        private void UpdateSelectAbleAnimation()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();

            if (_animator == null || _nodeData == null)
                return;

            if (!_animator.isActiveAndEnabled)
                _animator.enabled = true;

            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (_nodeData.IsAvailable)
            {
                AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
                // 이미 재생 중이면 재시작하지 않아 펄스가 끊기지 않게 유지
                if (!state.IsName(AnimSelectAble))
                {
                    _animator.Play(AnimSelectAble, 0, 0f);
                    _animator.Update(0f);
                }
            }
            else
            {
                _animator.Play(AnimNone, 0, 0f);
                _animator.Update(0f);
                ResetModelScale();
            }
        }

        private void ResetModelScale()
        {
            Transform model = transform.Find("Model");
            if (model != null)
                model.localScale = Vector3.one;
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
