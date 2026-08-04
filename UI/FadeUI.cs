using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 화면 전환용 페이드 오버레이.
    /// 스택 UI가 아니라 UIManager가 최상단에 올려 두고, 콘텐츠 준비 완료 후 FadeIn 한다.
    /// </summary>
    public class FadeUI : UIBase
    {
        private const int OverlaySortingOrder = 32000;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private float _fadeInDuration = 0.35f;

        [SerializeField]
        private float _fadeOutDuration = 0.25f;

        private Coroutine _fadeRoutine;

        public bool IsFading => _fadeRoutine != null;
        public float Alpha => _canvasGroup != null ? _canvasGroup.alpha : 0f;
        public float FadeInDuration => _fadeInDuration;
        public float FadeOutDuration => _fadeOutDuration;

        private void Awake()
        {
            EnsureCanvasGroup();
            EnsureTopmostOverlayCanvas();
        }

        public void SetFadeInDuration(float seconds)
        {
            _fadeInDuration = Mathf.Max(0.01f, seconds);
        }

        public void SetFadeOutDuration(float seconds)
        {
            _fadeOutDuration = Mathf.Max(0.01f, seconds);
        }

        /// <summary>즉시 불투명(가림). 전환 시작 시 사용.</summary>
        public void SetOpaqueImmediate()
        {
            EnsureCanvasGroup();
            EnsureTopmostOverlayCanvas();
            StopFadeRoutine();
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            gameObject.SetActive(true);
        }

        /// <summary>즉시 투명(해제).</summary>
        public void SetTransparentImmediate()
        {
            EnsureCanvasGroup();
            StopFadeRoutine();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        /// <summary>불투명 → 투명 (콘텐츠 공개).</summary>
        public void FadeIn(Action onComplete = null)
        {
            EnsureCanvasGroup();
            EnsureTopmostOverlayCanvas();
            gameObject.SetActive(true);
            StopFadeRoutine();
            _fadeRoutine = StartCoroutine(FadeRoutine(1f, 0f, _fadeInDuration, blockRaycastsWhileFading: true, onComplete));
        }

        /// <summary>투명 → 불투명 (화면 가림).</summary>
        public void FadeOut(Action onComplete = null)
        {
            EnsureCanvasGroup();
            EnsureTopmostOverlayCanvas();
            gameObject.SetActive(true);
            StopFadeRoutine();
            _fadeRoutine = StartCoroutine(FadeRoutine(0f, 1f, _fadeOutDuration, blockRaycastsWhileFading: true, onComplete));
        }

        private IEnumerator FadeRoutine(
            float from,
            float to,
            float duration,
            bool blockRaycastsWhileFading,
            Action onComplete)
        {
            _canvasGroup.blocksRaycasts = blockRaycastsWhileFading;
            _canvasGroup.interactable = blockRaycastsWhileFading;
            _canvasGroup.alpha = from;

            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float eased = t * t * (3f - 2f * t);
                _canvasGroup.alpha = Mathf.LerpUnclamped(from, to, eased);
                yield return null;
            }

            _canvasGroup.alpha = to;
            bool opaque = to >= 0.99f;
            _canvasGroup.blocksRaycasts = opaque;
            _canvasGroup.interactable = opaque;
            _fadeRoutine = null;
            onComplete?.Invoke();
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup != null)
                return;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// 형제 UI가 나중에 생성되어도 항상 위에 그리도록 독립 정렬 Canvas를 둔다.
        /// </summary>
        private void EnsureTopmostOverlayCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.overrideSorting = true;
            canvas.sortingOrder = OverlaySortingOrder;

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        private void StopFadeRoutine()
        {
            if (_fadeRoutine == null)
                return;

            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        private void OnDisable()
        {
            StopFadeRoutine();
        }
    }
}
