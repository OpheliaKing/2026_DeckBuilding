using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 좌/우 이동 요청만 올리고, 넘겨받은 Sprite만 표시하는 범용 UI.
    /// 데이터/인덱스는 사용하는 쪽에서 관리한다.
    /// </summary>
    public class IconCycleSelectUI : MonoBehaviour
    {
        [SerializeField]
        private Image _iconImage;

        [SerializeField]
        private UnityEvent<int> _onMoveRequested = new();

        /// <summary>
        /// 좌/우 버튼 클릭 시 호출. direction: -1 이전, +1 다음.
        /// 사용하는 쪽에서 구독 후 인덱스를 갱신하고 SetIcon으로 Sprite를 넘긴다.
        /// </summary>
        public event Action<int> OnMoveRequested;

        private void Awake()
        {
            ResolveIconImage();
        }

        /// <summary>
        /// Inspector에서 좌/우 버튼에 연결. direction: -1 / +1.
        /// </summary>
        public void OnClickButton(int direction)
        {
            if (direction == 0)
                return;

            int normalized = direction > 0 ? 1 : -1;
            OnMoveRequested?.Invoke(normalized);
            _onMoveRequested?.Invoke(normalized);
        }

        /// <summary>
        /// 표시할 Sprite를 설정한다. null이면 비운다.
        /// </summary>
        public void SetIcon(Sprite sprite)
        {
            ResolveIconImage();
            if (_iconImage == null)
                return;

            _iconImage.sprite = sprite;
        }

        private void ResolveIconImage()
        {
            if (_iconImage != null)
                return;

            Transform icon = transform.Find("IconFrame/Icon");
            if (icon == null)
                icon = transform.Find("Icon");

            if (icon != null)
                _iconImage = icon.GetComponent<Image>();

            if (_iconImage == null)
                _iconImage = GetComponentInChildren<Image>(true);
        }

        public void ClearIcon()
        {
            SetIcon(null);
        }
    }
}
