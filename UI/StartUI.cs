using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 타이틀 UI.
    /// 새 게임은 항상 표시, 이어하기(_continueButton)는 세이브가 있을 때만 활성화한다.
    /// 인트로: 배경 페이드 → 로고 페이드 → 버튼 순차 페이드.
    /// 버튼은 VerticalLayoutGroup 자식이라 위치 연출 없이 알파만 변경한다.
    /// </summary>
    public class StartUI : UIBase
    {
        [Header("References")]
        [SerializeField]
        private GameObject _continueButton;

        [SerializeField]
        private RectTransform _logoRoot;

        [SerializeField]
        private CanvasGroup _logoCanvasGroup;

        [SerializeField]
        private CanvasGroup _bgCanvasGroup;

        [SerializeField]
        private RectTransform _buttonsRoot;

        [Header("Atmosphere")]
        [SerializeField]
        private RosePetalFallUI _rosePetalFall;

        [Header("Intro Timing")]
        [SerializeField]
        private float _bgFadeDuration = 0.55f;

        [SerializeField]
        private float _logoDelay = 0.12f;

        [SerializeField]
        private float _logoFadeDuration = 0.45f;

        [SerializeField]
        private float _buttonStartDelay = 0.08f;

        [SerializeField]
        private float _buttonStagger = 0.07f;

        [SerializeField]
        private float _buttonFadeDuration = 0.32f;

        private readonly List<IntroButtonEntry> _buttonEntries = new();
        private Coroutine _introRoutine;
        private bool _introFinished;
        private bool _pendingIntro;

        private struct IntroButtonEntry
        {
            public CanvasGroup Group;
            public bool WasActive;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        public void Setup(bool hasSaveData)
        {
            if (_continueButton != null)
                _continueButton.SetActive(hasSaveData);

            CacheButtonEntries();
            PrepareIntroVisualState();

            _pendingIntro = true;
            if (isActiveAndEnabled)
                PlayIntro();
        }

        private void OnEnable()
        {
            if (_pendingIntro)
                PlayIntro();
        }

        public void OnClickStartButton()
        {
            if (!_introFinished)
                return;

            CloseSelf();

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[StartUI] GameManager.Instance가 없습니다.");
                return;
            }

            gameManager.OnTitleNewGameClicked();
        }

        public void OnClickContinueButton()
        {
            if (!_introFinished)
                return;

            CloseSelf();

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[StartUI] GameManager.Instance가 없습니다.");
                return;
            }

            gameManager.OnTitleContinueClicked();
        }

        public void OnClickOptionButton()
        {
            if (!_introFinished)
                return;

            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[StartUI] UIManager가 없습니다.");
                return;
            }

            uiManager.Show(PublicVariable.Address.OptionUIPrefab);
        }

        public void OnClickQuitButton()
        {
            if (!_introFinished)
                return;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void PlayIntro()
        {
            if (!isActiveAndEnabled)
            {
                _pendingIntro = true;
                return;
            }

            _pendingIntro = false;
            StopIntroRoutine();
            _introFinished = false;
            _rosePetalFall?.Play();
            _introRoutine = StartCoroutine(IntroRoutine());
        }

        private IEnumerator IntroRoutine()
        {
            // Layout이 한 프레임 안정화된 뒤 연출 (비활성→활성 직후 위치 튐 방지)
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (_buttonsRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_buttonsRoot);

            if (_bgCanvasGroup != null)
                yield return FadeCanvasGroup(_bgCanvasGroup, 0f, 1f, _bgFadeDuration);

            if (_logoDelay > 0f)
                yield return WaitUnscaled(_logoDelay);

            if (_logoCanvasGroup != null)
                yield return FadeCanvasGroup(_logoCanvasGroup, 0f, 1f, _logoFadeDuration);

            if (_buttonStartDelay > 0f)
                yield return WaitUnscaled(_buttonStartDelay);

            for (int i = 0; i < _buttonEntries.Count; i++)
            {
                IntroButtonEntry entry = _buttonEntries[i];
                if (!entry.WasActive || entry.Group == null)
                    continue;

                entry.Group.gameObject.SetActive(true);
                entry.Group.alpha = 0f;
                entry.Group.interactable = false;
                entry.Group.blocksRaycasts = false;

                StartCoroutine(FadeCanvasGroup(
                    entry.Group,
                    0f,
                    1f,
                    _buttonFadeDuration,
                    setInteractableAtEnd: true));

                if (_buttonStagger > 0f && i < _buttonEntries.Count - 1)
                    yield return WaitUnscaled(_buttonStagger);
            }

            if (_buttonFadeDuration > 0f)
                yield return WaitUnscaled(_buttonFadeDuration);

            _introFinished = true;
            _introRoutine = null;
        }

        private IEnumerator FadeCanvasGroup(
            CanvasGroup group,
            float from,
            float to,
            float duration,
            bool setInteractableAtEnd = false)
        {
            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            group.alpha = from;

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float eased = t * t * (3f - 2f * t);
                group.alpha = Mathf.LerpUnclamped(from, to, eased);
                yield return null;
            }

            group.alpha = to;
            if (setInteractableAtEnd)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float elapsed = 0f;
            float safe = Mathf.Max(0f, seconds);
            while (elapsed < safe)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void PrepareIntroVisualState()
        {
            if (_bgCanvasGroup != null)
            {
                _bgCanvasGroup.alpha = 0f;
                _bgCanvasGroup.interactable = true;
                _bgCanvasGroup.blocksRaycasts = true;
            }

            if (_logoCanvasGroup != null)
            {
                _logoCanvasGroup.alpha = 0f;
                _logoCanvasGroup.interactable = false;
                _logoCanvasGroup.blocksRaycasts = false;
            }

            for (int i = 0; i < _buttonEntries.Count; i++)
            {
                IntroButtonEntry entry = _buttonEntries[i];
                if (entry.Group == null)
                    continue;

                entry.Group.alpha = 0f;
                entry.Group.interactable = false;
                entry.Group.blocksRaycasts = false;
            }
        }

        private void CacheButtonEntries()
        {
            _buttonEntries.Clear();
            if (_buttonsRoot == null)
                return;

            for (int i = 0; i < _buttonsRoot.childCount; i++)
            {
                Transform child = _buttonsRoot.GetChild(i);
                if (child == null || !child.gameObject.activeSelf)
                    continue;

                CanvasGroup group = child.GetComponent<CanvasGroup>();
                if (group == null)
                    group = child.gameObject.AddComponent<CanvasGroup>();

                _buttonEntries.Add(new IntroButtonEntry
                {
                    Group = group,
                    WasActive = true,
                });
            }
        }

        private void ResolveReferences()
        {
            if (_logoRoot == null)
            {
                Transform logo = transform.Find("Image");
                if (logo != null)
                    _logoRoot = logo as RectTransform;
            }

            if (_logoRoot != null)
            {
                if (_logoCanvasGroup == null)
                    _logoCanvasGroup = _logoRoot.GetComponent<CanvasGroup>();
                if (_logoCanvasGroup == null)
                    _logoCanvasGroup = _logoRoot.gameObject.AddComponent<CanvasGroup>();
            }

            if (_bgCanvasGroup == null)
            {
                Transform bg = transform.Find("Bg");
                if (bg != null)
                {
                    _bgCanvasGroup = bg.GetComponent<CanvasGroup>();
                    if (_bgCanvasGroup == null)
                        _bgCanvasGroup = bg.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (_buttonsRoot == null)
            {
                Transform buttons = transform.Find("Bg/Buttons");
                if (buttons == null)
                    buttons = transform.Find("Buttons");
                if (buttons != null)
                    _buttonsRoot = buttons as RectTransform;
            }

            if (_rosePetalFall == null)
                _rosePetalFall = GetComponentInChildren<RosePetalFallUI>(true);
        }

        private void StopIntroRoutine()
        {
            if (_introRoutine != null)
            {
                StopCoroutine(_introRoutine);
                _introRoutine = null;
            }
        }

        private void CloseSelf()
        {
            _pendingIntro = false;
            StopIntroRoutine();
            _rosePetalFall?.Stop(clearVisible: true);

            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager != null && uiManager.Current == this)
            {
                uiManager.Close();
                return;
            }

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            StopIntroRoutine();
        }
    }
}
