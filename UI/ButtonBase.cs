using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 공용 버튼 베이스. 클릭 SE path가 있으면 onClick 시 재생한다.
    /// </summary>
    public class ButtonBase : Button
    {
        [SerializeField]
        protected string _soundClipName;

        protected override void Awake()
        {
            base.Awake();
            onClick.AddListener(PlayClickSound);
        }

        protected override void OnDestroy()
        {
            onClick.RemoveListener(PlayClickSound);
            base.OnDestroy();
        }

        /// <summary>Inspector 등에서 명시 호출할 때 사용. onClick에 또 연결하면 SE가 두 번 난다.</summary>
        public virtual void OnClick()
        {
            PlayClickSound();
        }

        protected virtual void PlayClickSound()
        {
            UiClickSound.Play(_soundClipName);
        }
    }
}
