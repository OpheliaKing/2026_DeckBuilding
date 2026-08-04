using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// UI 표시 방식.
    /// Screen: 스택 전환 시 이전 UI를 숨긴다.
    /// Popup: 이전 UI를 유지한 채 위에 올린다.
    /// </summary>
    public enum UI_TYPE
    {
        Screen = 0,
        Popup = 1,
    }

    public class UIBase : MonoBehaviour
    {
        [SerializeField]
        private UI_TYPE _uiType = UI_TYPE.Screen;

        /// <summary>UI 표시 타입. 하위 클래스에서 override로 고정할 수 있다.</summary>
        public virtual UI_TYPE UiType => _uiType;

        protected void SetUiType(UI_TYPE uiType)
        {
            _uiType = uiType;
        }
    }
}
