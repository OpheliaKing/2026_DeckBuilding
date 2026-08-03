using UnityEngine;
using UnityEngine.EventSystems;

namespace SHIN
{
    /// <summary>
    /// IPointerClickHandler 공용 베이스. 클릭 SE 후 HandleClick으로 실제 동작을 위임한다.
    /// _soundPath가 비어 있으면 사운드는 재생하지 않는다.
    /// </summary>
    public class ClickEventUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        private string _soundPath;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!CanClick(eventData))
                return;

            PlayClickSound();
            HandleClick(eventData);
        }

        /// <summary>false면 사운드/동작 모두 스킵.</summary>
        protected virtual bool CanClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                return false;

            return true;
        }

        protected virtual void HandleClick(PointerEventData eventData)
        {
        }

        protected void PlayClickSound()
        {
            UiClickSound.Play(_soundPath);
        }
    }
}
