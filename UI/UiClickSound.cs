using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// UI 클릭 SE 공통 재생. path가 비어 있으면 재생하지 않는다.
    /// </summary>
    public static class UiClickSound
    {
        public static void Play(string soundPath)
        {
            if (string.IsNullOrEmpty(soundPath))
                return;

            var soundManager = GameManager.Instance?.SoundManager;
            if (soundManager == null)
                return;

            soundManager.PlaySe(soundPath);
        }
    }
}
