using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// UI용 TMP 폰트 공용 로더. Noto Sans KR Regular.
    /// </summary>
    public static class UiFont
    {
        private static TMP_FontAsset _notoSansRegular;
        private static Task<TMP_FontAsset> _loadTask;

        public static TMP_FontAsset NotoSansRegular => _notoSansRegular;

        public static void ApplyNotoSansRegular(TMP_Text text)
        {
            if (text == null)
                return;

            if (_notoSansRegular != null)
            {
                text.font = _notoSansRegular;
                return;
            }

            ApplyNotoSansRegularAsync(text);
        }

        public static async void ApplyNotoSansRegularAsync(TMP_Text text)
        {
            if (text == null)
                return;

            TMP_FontAsset font = await GetNotoSansRegularAsync();
            if (text == null || font == null)
                return;

            text.font = font;
        }

        public static async Task<TMP_FontAsset> GetNotoSansRegularAsync()
        {
            if (_notoSansRegular != null)
                return _notoSansRegular;

            if (_loadTask != null)
                return await _loadTask;

            _loadTask = LoadNotoSansRegularInternalAsync();
            try
            {
                return await _loadTask;
            }
            finally
            {
                _loadTask = null;
            }
        }

        private static async Task<TMP_FontAsset> LoadNotoSansRegularInternalAsync()
        {
            if (_notoSansRegular != null)
                return _notoSansRegular;

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogWarning("[UiFont] ResourceManager가 없어 NotoSansKR-Regular를 로드할 수 없습니다.");
                return null;
            }

            _notoSansRegular = await resourceManager.LoadAsync<TMP_FontAsset>(
                PublicVariable.Address.NotoSansKrRegularFont);
            if (_notoSansRegular == null)
                Debug.LogError("[UiFont] NotoSansKR-Regular SDF 로드 실패");

            return _notoSansRegular;
        }
    }
}
