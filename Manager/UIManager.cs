using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// UIBase 스택 기반 UI 관리. Show(string)으로 ResourceManager를 통해 UI를 로드한다.
    /// </summary>
    public class UIManager : ManagerBase
    {
        [SerializeField]
        private Transform _uiRoot;

        private Transform _canvasRoot;

        private readonly Stack<UIStackEntry> _uiStack = new();

        public int Count => _uiStack.Count;
        public UIBase Current => _uiStack.Count > 0 ? _uiStack.Peek().UI : null;

        public void Show(string address)
        {
            Show(address, null);
        }

        public void Show(string address, Action<UIBase> onComplete)
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError("[UIManager] address가 비어 있습니다.");
                return;
            }

            ResourceManager resourceManager = ResolveResourceManager();
            if (resourceManager == null)
                return;

            Transform parent = ResolveUIRoot();
            if (parent == null)
                return;

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
                onComplete?.Invoke(uiBase);
            }, parent);
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
        /// 스택 최상단 UI를 닫고 이전 UI를 다시 표시한다.
        /// </summary>
        public bool Close()
        {
            if (_uiStack.Count == 0)
                return false;

            PopAndReleaseUI();
            ShowCurrentTop();
            return true;
        }

        /// <summary>
        /// 스택에 쌓인 UI를 모두 닫는다.
        /// </summary>
        public void CloseAll()
        {
            while (_uiStack.Count > 0)
                PopAndReleaseUI();
        }

        private void PushUI(string address, UIBase ui, GameObject uiObject)
        {
            if (_uiStack.Count > 0)
                _uiStack.Peek().SetVisible(false);

            var entry = new UIStackEntry(address, ui, uiObject);
            entry.SetVisible(true);
            _uiStack.Push(entry);
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

        /// <summary>
        /// UI 생성 부모. _uiRoot가 있으면 Canvas 하위 컨테이너로 사용하고, 없으면 씬의 Canvas를 사용한다.
        /// </summary>
        private Transform ResolveUIRoot()
        {
            if (_uiRoot != null)
                return _uiRoot;

            if (_canvasRoot != null)
                return _canvasRoot;

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[UIManager] Canvas를 찾을 수 없어 UI를 생성할 수 없습니다.");
                return null;
            }

            _canvasRoot = canvas.transform;
            return _canvasRoot;
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
