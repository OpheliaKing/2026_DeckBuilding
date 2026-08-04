using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// UI용 TMP 폰트 공용 로더.
    /// 본문/HUD/버튼: Gowun Dodum, 제목: Gowun Batang Bold.
    /// </summary>
    public static class UiFont
    {
        private static TMP_FontAsset _body;
        private static TMP_FontAsset _title;
        private static Task<TMP_FontAsset> _bodyLoadTask;
        private static Task<TMP_FontAsset> _titleLoadTask;

        public static TMP_FontAsset Body => _body;
        public static TMP_FontAsset Title => _title;

        /// <summary>본문/HUD/버튼용 (Gowun Dodum)</summary>
        public static void ApplyBody(TMP_Text text)
        {
            if (text == null)
                return;

            if (_body != null)
            {
                text.font = _body;
                return;
            }

            ApplyBodyAsync(text);
        }

        /// <summary>제목용 (Gowun Batang Bold)</summary>
        public static void ApplyTitle(TMP_Text text)
        {
            if (text == null)
                return;

            if (_title != null)
            {
                text.font = _title;
                return;
            }

            ApplyTitleAsync(text);
        }

        /// <summary>하위 호환: 본문 폰트 적용</summary>
        public static void ApplyNotoSansRegular(TMP_Text text) => ApplyBody(text);

        public static async void ApplyBodyAsync(TMP_Text text)
        {
            if (text == null)
                return;

            TMP_FontAsset font = await GetBodyAsync();
            if (text == null || font == null)
                return;

            text.font = font;
        }

        public static async void ApplyTitleAsync(TMP_Text text)
        {
            if (text == null)
                return;

            TMP_FontAsset font = await GetTitleAsync();
            if (text == null || font == null)
                return;

            text.font = font;
        }

        public static async void ApplyNotoSansRegularAsync(TMP_Text text) => ApplyBodyAsync(text);

        public static Task<TMP_FontAsset> GetNotoSansRegularAsync() => GetBodyAsync();

        public static async Task<TMP_FontAsset> GetBodyAsync()
        {
            if (_body != null)
                return _body;

            if (_bodyLoadTask != null)
                return await _bodyLoadTask;

            _bodyLoadTask = LoadAsync(PublicVariable.Address.GowunDodumRegularFont, "GowunDodum-Regular");
            try
            {
                _body = await _bodyLoadTask;
                return _body;
            }
            finally
            {
                _bodyLoadTask = null;
            }
        }

        public static async Task<TMP_FontAsset> GetTitleAsync()
        {
            if (_title != null)
                return _title;

            if (_titleLoadTask != null)
                return await _titleLoadTask;

            _titleLoadTask = LoadAsync(PublicVariable.Address.GowunBatangBoldFont, "GowunBatang-Bold");
            try
            {
                _title = await _titleLoadTask;
                return _title;
            }
            finally
            {
                _titleLoadTask = null;
            }
        }

        private static async Task<TMP_FontAsset> LoadAsync(string address, string label)
        {
            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogWarning($"[UiFont] ResourceManager가 없어 {label}를 로드할 수 없습니다.");
                return null;
            }

            TMP_FontAsset font = await resourceManager.LoadAsync<TMP_FontAsset>(address);
            if (font == null)
                Debug.LogError($"[UiFont] {label} SDF 로드 실패");

            return font;
        }
    }
}
