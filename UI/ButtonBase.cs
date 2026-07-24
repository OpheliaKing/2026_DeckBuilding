using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 공용 버튼 베이스. Inspector UnityEvent로 클릭 동작을 연결한다.
    /// </summary>
    public class ButtonBase :  Button
    {
        [SerializeField]
        protected string _soundClipName;

        public virtual void OnClick()
        {
            PlayClickSound();
        }


        protected virtual void PlayClickSound()
        {
            if (string.IsNullOrEmpty(_soundClipName))
                return;

            // TODO: 사운드 매니저 추가 후 _soundClipName 재생
        }
    }
}
