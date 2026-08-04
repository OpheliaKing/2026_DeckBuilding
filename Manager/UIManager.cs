using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// UIBase 스택 기반 UI 관리. Show(string)으로 ResourceManager를 통해 UI를 로드한다.
    /// useFade 시 FadeUI 오버레이로 가린 뒤, SignalContentReady()로 페이드인한다.
    /// </summary>
    public class UIManager : ManagerBase
    {
        [SerializeField]
        private Transform _uiRoot;

        private Transform _canvasRoot;

        private readonly Stack<UIStackEntry> _uiStack = new();

        private FadeUI _fadeUI;
        private GameObject _fadeUIObject;
        private bool _waitingContentReadyForFade;

        public int Count => _uiStack.Count;
        public UIBase Current => _uiStack.Count > 0 ? _uiStack.Peek().UI : null;
        public bool IsWaitingFadeReady => _waitingContentReadyForFade;

        /// <summary>일반 UI를 붙일 Canvas 루트. FadeUI 오버레이 Canvas는 제외한다.</summary>
        public Transform UIRoot => ResolveUIRoot();

        public void Show(string address)
        {
            Show(address, null, useFade: false);
        }

        public void Show(string address, Action<UIBase> onComplete)
        {
            Show(address, onComplete, useFade: false);
        }

        /// <summary>
        /// Boot 시 FadeUI를 미리 생성해 두고 투명 상태로 둔다.
        /// 시작 버튼 클릭 시 로드 대기 없이 즉시 가릴 수 있다.
        /// </summary>
        public async System.Threading.Tasks.Task PreloadFadeUIAsync()
        {
            if (_fadeUI != null)
            {
                _fadeUI.SetTransparentImmediate();
                BringFadeToFront();
                return;
            }

            Transform parent = ResolveUIRoot();
            if (parent == null)
                return;

            GameObject go = await CreateAsync(PublicVariable.Address.FadeUIPrefab, parent);
            if (go == null)
            {
                Debug.LogError("[UIManager] FadeUI 프리로드 실패");
                return;
            }

            FadeUI fade = go.GetComponent<FadeUI>();
            if (fade == null)
                fade = go.GetComponentInChildren<FadeUI>(true);
            if (fade == null)
                fade = go.AddComponent<FadeUI>();

            _fadeUIObject = go;
            _fadeUI = fade;
            _fadeUI.SetTransparentImmediate();
            BringFadeToFront();
        }

        /// <summary>
        /// 화면을 즉시 불투명(알파 1)으로 가린다. 페이드아웃 연출 없음.
        /// 로딩이 끝나면 SignalContentReady()로 페이드인한다.
        /// </summary>
        public void BeginFadeCover(Action onCoverReady = null)
        {
            // 프리로드된 경우 동기적으로 즉시 가림 (빈 화면 방지)
            if (_fadeUI != null)
            {
                _fadeUI.SetOpaqueImmediate();
                BringFadeToFront();
                _waitingContentReadyForFade = true;
                onCoverReady?.Invoke();
                return;
            }

            EnsureFadeUI(fade =>
            {
                if (fade == null)
                {
                    Debug.LogWarning("[UIManager] FadeUI 생성 실패 → 커버 없이 진행");
                    _waitingContentReadyForFade = false;
                    onCoverReady?.Invoke();
                    return;
                }

                fade.SetOpaqueImmediate();
                BringFadeToFront();
                _waitingContentReadyForFade = true;
                onCoverReady?.Invoke();
            });
        }

        /// <summary>
        /// UI를 표시한다.
        /// useFade=true면 BeginFadeCover 후 콘텐츠 Show (하위 호환).
        /// 시작 버튼처럼 먼저 가리고 싶으면 BeginFadeCover를 직접 호출하는 쪽을 권장.
        /// </summary>
        public void Show(string address, Action<UIBase> onComplete, bool useFade)
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError("[UIManager] address가 비어 있습니다.");
                return;
            }

            if (!useFade)
            {
                ShowInternal(address, onComplete);
                return;
            }

            BeginFadeCover(() =>
            {
                ShowInternal(address, ui =>
                {
                    BringFadeToFront();
                    onComplete?.Invoke(ui);
                });
            });
        }

        /// <summary>
        /// useFade Show 이후, 리소스/데이터 준비가 끝났을 때 호출.
        /// FadeUI가 페이드인한 뒤 오버레이를 내린다.
        /// </summary>
        public void SignalContentReady(Action onFadeComplete = null)
        {
            if (!_waitingContentReadyForFade)
            {
                onFadeComplete?.Invoke();
                return;
            }

            _waitingContentReadyForFade = false;

            // 페이드 커버 동안 비활성으로 올려 둔 스택 UI만 켠다 (스택이 비면 no-op)
            if (Current != null && Current.gameObject != null && !Current.gameObject.activeSelf)
                ShowCurrentTop();

            BringFadeToFront();

            if (_fadeUI == null)
            {
                onFadeComplete?.Invoke();
                return;
            }

            FadeUI fade = _fadeUI;
            fade.FadeIn(() =>
            {
                // 다음 전환을 위해 인스턴스는 유지하고 투명만 처리
                if (_fadeUI == fade)
                    fade.SetTransparentImmediate();

                onFadeComplete?.Invoke();
            });
        }

        /// <summary>
        /// 스택에 올리지 않고, 지정 부모 아래에 UI 프리팹만 생성한다.
        /// </summary>
        public void Create(string address, Transform parent, Action<GameObject> onComplete)
        {
            CreateAsyncInternal(address, parent, onComplete);
        }

        public async System.Threading.Tasks.Task<GameObject> CreateAsync(string address, Transform parent)
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError("[UIManager] address가 비어 있습니다.");
                return null;
            }

            if (parent == null)
            {
                Debug.LogError("[UIManager] parent가 null입니다.");
                return null;
            }

            ResourceManager resourceManager = ResolveResourceManager();
            if (resourceManager == null)
                return null;

            GameObject uiObject = await resourceManager.InstantiateAsync(address, parent);
            if (uiObject == null)
                Debug.LogError($"[UIManager] UI 생성 실패: {address}");

            return uiObject;
        }

        private async void CreateAsyncInternal(string address, Transform parent, Action<GameObject> onComplete)
        {
            GameObject result = await CreateAsync(address, parent);
            onComplete?.Invoke(result);
        }

        /// <summary>
        /// Create로 만든 UI 인스턴스를 해제한다.
        /// </summary>
        public void ReleaseCreated(GameObject uiObject)
        {
            if (uiObject == null)
                return;

            ResourceManager resourceManager = ResolveResourceManager();
            if (resourceManager != null)
                resourceManager.ReleaseInstance(uiObject);
            else
                Destroy(uiObject);
        }

        /// <summary>
        /// 스택 최상단 UI를 닫는다.
        /// revealPrevious=false면 이전 UI를 켜지 않는다(페이드 커버 중 깜빡임 방지).
        /// </summary>
        public bool Close(bool revealPrevious = true)
        {
            if (_uiStack.Count == 0)
                return false;

            PopAndReleaseUI();

            if (revealPrevious)
                ShowCurrentTop();

            BringFadeToFront();
            return true;
        }

        /// <summary>
        /// 스택에 쌓인 UI를 모두 닫는다.
        /// </summary>
        public void CloseAll()
        {
            _waitingContentReadyForFade = false;
            ReleaseFadeUI();

            while (_uiStack.Count > 0)
                PopAndReleaseUI();
        }

        private void ShowInternal(string address, Action<UIBase> onComplete)
        {
            ResourceManager resourceManager = ResolveResourceManager();
            if (resourceManager == null)
                return;

            Transform parent = ResolveUIRoot();
            if (parent == null)
                return;

            // startInactive: Addressables 생성 직후 await 양보 전 StageNodeUI가 페이드 위에 한 프레임 뜨는 것 방지
            resourceManager.InstantiateAsync(address, uiObject =>
            {
                if (uiObject == null)
                {
                    Debug.LogError($"[UIManager] UI 생성 실패: {address}");
                    return;
                }

                if (!TryGetUIBase(uiObject, out UIBase uiBase))
                {
                    resourceManager.ReleaseInstance(uiObject);
                    Debug.LogError($"[UIManager] UIBase 컴포넌트가 없습니다: {address}");
                    return;
                }

                PushUI(address, uiBase, uiObject);
                BringFadeToFront();
                onComplete?.Invoke(uiBase);
            }, parent, startInactive: true);
        }

        private void EnsureFadeUI(Action<FadeUI> onReady)
        {
            if (_fadeUI != null)
            {
                onReady?.Invoke(_fadeUI);
                return;
            }

            Transform parent = ResolveUIRoot();
            if (parent == null)
            {
                onReady?.Invoke(null);
                return;
            }

            Create(PublicVariable.Address.FadeUIPrefab, parent, go =>
            {
                if (go == null)
                {
                    onReady?.Invoke(null);
                    return;
                }

                FadeUI fade = go.GetComponent<FadeUI>();
                if (fade == null)
                    fade = go.GetComponentInChildren<FadeUI>(true);

                if (fade == null)
                {
                    // 프리팹에 스크립트가 빠져 있어도 런타임에서 보정
                    fade = go.AddComponent<FadeUI>();
                    Debug.LogWarning("[UIManager] FadeUI 컴포넌트가 없어 AddComponent로 보정했습니다.");
                }

                _fadeUIObject = go;
                _fadeUI = fade;
                BringFadeToFront();
                onReady?.Invoke(_fadeUI);
            });
        }

        private void BringFadeToFront()
        {
            if (_fadeUIObject == null)
                return;

            _fadeUIObject.transform.SetAsLastSibling();
        }

        private void ReleaseFadeUI()
        {
            if (_fadeUIObject != null)
            {
                ReleaseCreated(_fadeUIObject);
                _fadeUIObject = null;
            }

            _fadeUI = null;
            _waitingContentReadyForFade = false;
        }

        private void PushUI(string address, UIBase ui, GameObject uiObject)
        {
            if (_uiStack.Count > 0)
                _uiStack.Peek().SetVisible(false);

            var entry = new UIStackEntry(address, ui, uiObject);
            _uiStack.Push(entry);
            BringFadeToFront();

            // 페이드 커버 대기 중이면 페이드인 직전까지 비활성 유지
            if (_waitingContentReadyForFade)
                entry.SetVisible(false);
            else
                entry.SetVisible(true);

            BringFadeToFront();
        }

        private void PopAndReleaseUI()
        {
            UIStackEntry entry = _uiStack.Pop();
            entry.SetVisible(false);

            ResourceManager resourceManager = ResolveResourceManager();
            if (resourceManager != null)
                resourceManager.ReleaseInstance(entry.GameObject);
            else
                Destroy(entry.GameObject);
        }

        private void ShowCurrentTop()
        {
            if (_uiStack.Count == 0)
                return;

            _uiStack.Peek().SetVisible(true);
        }

        private Transform ResolveUIRoot()
        {
            if (_uiRoot != null)
                return _uiRoot;

            if (_canvasRoot != null)
                return _canvasRoot;

            Canvas canvas = FindMainCanvas();
            if (canvas == null)
            {
                Debug.LogError("[UIManager] Canvas를 찾을 수 없어 UI를 생성할 수 없습니다.");
                return null;
            }

            _canvasRoot = canvas.transform;
            return _canvasRoot;
        }

        /// <summary>
        /// FadeUI 등 오버레이용 Canvas를 제외한 메인 Canvas를 찾는다.
        /// </summary>
        private static Canvas FindMainCanvas()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            Canvas fallback = null;

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                    continue;

                if (canvas.GetComponent<FadeUI>() != null)
                    continue;

                if (fallback == null)
                    fallback = canvas;

                // 루트 Screen Space Canvas 우선
                if (canvas.isRootCanvas && !canvas.overrideSorting)
                    return canvas;
            }

            return fallback;
        }

        private static ResourceManager ResolveResourceManager()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[UIManager] GameManager.Instance가 없습니다.");
                return null;
            }

            return GameManager.Instance.ResourceManager;
        }

        private static bool TryGetUIBase(GameObject uiObject, out UIBase uiBase)
        {
            uiBase = uiObject.GetComponent<UIBase>();
            if (uiBase != null)
                return true;

            uiBase = uiObject.GetComponentInChildren<UIBase>(true);
            return uiBase != null;
        }

        private void OnDestroy()
        {
            CloseAll();
        }

        private sealed class UIStackEntry
        {
            public readonly string Address;
            public readonly UIBase UI;
            public readonly GameObject GameObject;

            public UIStackEntry(string address, UIBase ui, GameObject gameObject)
            {
                Address = address;
                UI = ui;
                GameObject = gameObject;
            }

            public void SetVisible(bool visible)
            {
                if (GameObject != null)
                    GameObject.SetActive(visible);
            }
        }
    }
}
