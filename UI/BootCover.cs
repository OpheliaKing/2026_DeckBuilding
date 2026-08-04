using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 부트 중 Addressables 로딩 동안 카메라 clear(파란 화면)를 가리는 씬 비의존 커버.
    /// BeforeSceneLoad에서 생성하고, FadeUI가 화면을 인수한 뒤 Release한다.
    /// </summary>
    public sealed class BootCover : MonoBehaviour
    {
        private const int SortingOrder = 31000;
        private static BootCover _instance;

        public static bool IsActive => _instance != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Ensure();
        }

        public static void Ensure()
        {
            if (_instance != null)
                return;

            var root = new GameObject("BootCover");
            DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvas.overrideSorting = true;

            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.AddComponent<GraphicRaycaster>();

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGo.transform.SetParent(root.transform, false);

            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = panelGo.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            _instance = root.AddComponent<BootCover>();
        }

        /// <summary>FadeUI 등 후속 커버가 준비되면 호출. 한 번만 파괴한다.</summary>
        public static void Release()
        {
            if (_instance == null)
                return;

            BootCover cover = _instance;
            _instance = null;

            if (cover == null || cover.gameObject == null)
                return;

            // Destroy는 프레임 끝에 적용되므로, 즉시 비활성해 FindMainCanvas/부모 후보에서 제외
            cover.gameObject.SetActive(false);
            Destroy(cover.gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
