using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 엔딩 시퀀스 UI. 클릭으로 페이지를 넘기고, 마지막 클릭 후 엔딩 레이어 페이드 → 타이틀 복귀.
    /// </summary>
    public class EndingSequenceUI : UIBase, IPointerClickHandler
    {
        [SerializeField]
        private Image _sequenceImage;

        [SerializeField]
        private TextMeshProUGUI _sequenceText;

        [SerializeField]
        private CanvasGroup _endingLayer;

        [Header("Timing")]
        [SerializeField]
        private float _clickCooldown = 0.35f;

        [SerializeField]
        private float _endingFadeDuration = 1.5f;

        [SerializeField]
        private float _endingHoldSeconds = 2f;

        private EndingSequenceData _sequenceData;
        private readonly List<EndingSequencePage> _pages = new();
        private int _pageIndex;
        private bool _inputLocked;
        private bool _isFinishing;
        private float _inputUnlockTime;
        private int _imageLoadVersion;
        private Action _onCompleted;

        private void Awake()
        {
            EnsureUiClickable();
        }

        public void ShowEndingSequence(EndingSequenceData endingSequence, Action onCompleted = null)
        {
            _sequenceData = endingSequence;
            _onCompleted = onCompleted;
            _pageIndex = 0;
            _isFinishing = false;
            _pages.Clear();

            if (endingSequence?.Pages != null)
            {
                for (int i = 0; i < endingSequence.Pages.Count; i++)
                    _pages.Add(endingSequence.Pages[i]);
            }

            InitializeEndingLayerHidden();
            SetInputLocked(true, _clickCooldown);

            if (_pages.Count == 0)
            {
                Debug.LogWarning("[EndingSequenceUI] 엔딩 페이지가 비어 있어 바로 엔딩 레이어로 진행합니다.");
                StartCoroutine(PlayEndingLayerAndFinish());
                return;
            }

            ApplyCurrentPageAsync();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                return;

            HandleClick();
        }

        /// <summary>하이어라키 버튼 OnClick용.</summary>
        public void OnClickAdvance()
        {
            HandleClick();
        }

        private void HandleClick()
        {
            if (_isFinishing || _inputLocked || Time.unscaledTime < _inputUnlockTime)
                return;

            if (_pages.Count == 0)
                return;

            // 마지막 페이지에서 클릭 → 엔딩 레이어 페이드
            if (_pageIndex >= _pages.Count - 1)
            {
                StartCoroutine(PlayEndingLayerAndFinish());
                return;
            }

            _pageIndex++;
            SetInputLocked(true, _clickCooldown);
            ApplyCurrentPageAsync();
        }

        private void InitializeEndingLayerHidden()
        {
            if (_endingLayer == null)
                return;

            if (!_endingLayer.gameObject.activeSelf)
                _endingLayer.gameObject.SetActive(true);

            _endingLayer.alpha = 0f;
            _endingLayer.interactable = false;
            _endingLayer.blocksRaycasts = false;
        }

        private async void ApplyCurrentPageAsync()
        {
            if (_pageIndex < 0 || _pageIndex >= _pages.Count)
                return;

            EndingSequencePage page = _pages[_pageIndex];

            if (_sequenceText != null)
                _sequenceText.text = page.Text ?? string.Empty;

            int version = ++_imageLoadVersion;
            if (_sequenceImage == null)
                return;

            if (string.IsNullOrEmpty(page.ImagePath))
            {
                _sequenceImage.enabled = false;
                return;
            }

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[EndingSequenceUI] ResourceManager가 없습니다.");
                return;
            }

            Sprite sprite = await resourceManager.GetAtlasSpriteAsync(ATLAS_TYPE.UI, page.ImagePath);
            if (version != _imageLoadVersion)
                return;

            if (sprite == null)
            {
                _sequenceImage.enabled = false;
                return;
            }

            _sequenceImage.sprite = sprite;
            _sequenceImage.enabled = true;
        }

        private IEnumerator PlayEndingLayerAndFinish()
        {
            if (_isFinishing)
                yield break;

            _isFinishing = true;
            SetInputLocked(true, 999f);

            if (_endingLayer != null)
            {
                if (!_endingLayer.gameObject.activeSelf)
                    _endingLayer.gameObject.SetActive(true);

                _endingLayer.blocksRaycasts = true;
                float duration = Mathf.Max(0.01f, _endingFadeDuration);
                float elapsed = 0f;
                float startAlpha = _endingLayer.alpha;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    _endingLayer.alpha = Mathf.Lerp(startAlpha, 1f, t);
                    yield return null;
                }

                _endingLayer.alpha = 1f;
            }

            float hold = Mathf.Max(0f, _endingHoldSeconds);
            if (hold > 0f)
                yield return new WaitForSecondsRealtime(hold);

            Action callback = _onCompleted;
            _onCompleted = null;
            callback?.Invoke();
        }

        private void SetInputLocked(bool locked, float cooldownSeconds)
        {
            _inputLocked = locked;
            _inputUnlockTime = Time.unscaledTime + Mathf.Max(0f, cooldownSeconds);

            if (locked && cooldownSeconds < 10f)
                StartCoroutine(UnlockInputAfterCooldown(cooldownSeconds));
            else if (!locked)
                _inputLocked = false;
        }

        private IEnumerator UnlockInputAfterCooldown(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
            if (!_isFinishing)
                _inputLocked = false;
        }

        private void OnDestroy()
        {
            _imageLoadVersion++;
            _onCompleted = null;
        }

        private void EnsureUiClickable()
        {
            Graphic graphic = GetComponent<Graphic>();
            if (graphic == null)
            {
                var image = gameObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = true;
            }
            else
            {
                graphic.raycastTarget = true;
            }
        }
    }
}
