using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 캐릭터 선택 화면 로직.
    /// GameManager가 Addressables 프리팹을 ResourceManager로 생성한 뒤 Show()를 호출한다.
    /// SO 로드 → UIManager로 UnitSetupUI 표시 → 미리보기 콜백으로 모델 갱신.
    /// </summary>
    public class CharacterSelectObject : MonoBehaviour
    {
        [SerializeField]
        private Transform _modelRoot;

        [Header("Appear Dissolve")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("캐릭터 모델 등장 디졸브가 끝나는 시간(초). CharacterSelectObject에서 조절")]
        private float _appearDissolveDuration = 1.2f;

        private CharacterSelectDataSO _dataSO;
        private CharacterSelectData _selectedData;
        private UnitSetupUI _unitSetupUI;
        private CharacterSelectModel _currentModel;
        private string _currentModelKey;
        private bool _isShowing;

        private readonly Dictionary<string, CachedModel> _modelCache = new();
        private readonly HashSet<string> _loadingKeys = new();

        public CharacterSelectData SelectedData => _selectedData;
        public string SelectedTid => _selectedData?.Tid;
        public CharacterSelectModel CurrentModel => _currentModel;
        public event Action<CharacterSelectData> OnCharacterSelected;

        /// <summary>캐릭터/무기 세팅 완료 시 (GameManager BootFlow가 구독).</summary>
        public event Action<UnitInfo> OnSetupCompleted;

        /// <summary>
        /// SO 로드 후 UnitSetupUI를 연다. BootFlow에서 호출한다.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            _isShowing = true;
            ShowAsync();
        }

        public void Hide()
        {
            _isShowing = false;
            HideAllCachedModels();
            UnbindUnitSetupUI();

            if (_unitSetupUI != null)
            {
                GameManager.Instance?.UIManager?.Close();
                _unitSetupUI = null;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// UnitSetupUI 캐릭터 미리보기 콜백.
        /// </summary>
        public void OnCharacterSlotSelected(CharacterSelectData data)
        {
            if (data == null || string.IsNullOrEmpty(data.Tid))
                return;

            SelectCharacter(data);
        }

        public void SelectCharacter(string tid)
        {
            if (_dataSO == null)
            {
                Debug.LogError("[CharacterSelectObject] CharacterSelectDataSO가 없습니다.");
                return;
            }

            CharacterSelectData data = _dataSO.GetCharacterSelectData(tid);
            if (data == null)
                return;

            SelectCharacter(data);
        }

        public void SelectCharacter(CharacterSelectData data)
        {
            if (data == null)
                return;

            _selectedData = data;
            ShowModel(data);
            OnCharacterSelected?.Invoke(_selectedData);
        }

        private static string GetModelCacheKey(CharacterSelectData data)
        {
            if (data == null)
                return null;

            if (!string.IsNullOrEmpty(data.PrefabPath))
                return data.PrefabPath;

            return data.Tid;
        }

        private async void ShowAsync()
        {
            if (!await EnsureDataSOAsync())
            {
                GameManager.Instance?.UIManager?.SignalContentReady();
                return;
            }

            if (!_isShowing)
            {
                GameManager.Instance?.UIManager?.SignalContentReady();
                return;
            }

            OpenUnitSetupUI();
        }

        private async System.Threading.Tasks.Task<bool> EnsureDataSOAsync()
        {
            if (_dataSO != null && _dataSO.Count > 0)
                return true;

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[CharacterSelectObject] GameManager.Instance가 없습니다.");
                return false;
            }

            _dataSO = await gameManager.GetSOAsync<CharacterSelectDataSO>(
                PublicVariable.Address.CharacterSelectDataSO);

            if (_dataSO == null || _dataSO.Count == 0)
            {
                Debug.LogError("[CharacterSelectObject] CharacterSelectDataSO가 비어 있거나 로드 실패입니다.");
                return false;
            }

            return true;
        }

        private void OpenUnitSetupUI()
        {
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[CharacterSelectObject] UIManager를 찾을 수 없습니다.");
                return;
            }

            uiManager.Show(PublicVariable.Address.UnitSetupUIPrefab, uiBase =>
            {
                if (!_isShowing)
                {
                    uiManager.SignalContentReady();
                    return;
                }

                if (uiBase is not UnitSetupUI unitSetupUI)
                {
                    Debug.LogError("[CharacterSelectObject] UnitSetupUI 컴포넌트가 없습니다.");
                    uiManager.SignalContentReady();
                    return;
                }

                UnbindUnitSetupUI();
                _unitSetupUI = unitSetupUI;
                _unitSetupUI.OnCharacterPreviewChanged += OnCharacterSlotSelected;
                _unitSetupUI.OnSetupCompleted += HandleSetupCompleted;
                _unitSetupUI.BeginSetup(onContentReady: () => uiManager.SignalContentReady());
            });
        }

        private void HandleSetupCompleted(UnitInfo unitInfo)
        {
            UnbindUnitSetupUI();
            _unitSetupUI = null;
            _isShowing = false;
            HideAllCachedModels();
            gameObject.SetActive(false);
            OnSetupCompleted?.Invoke(unitInfo);
        }

        private void UnbindUnitSetupUI()
        {
            if (_unitSetupUI == null)
                return;

            _unitSetupUI.OnCharacterPreviewChanged -= OnCharacterSlotSelected;
            _unitSetupUI.OnSetupCompleted -= HandleSetupCompleted;
        }

        private void ShowModel(CharacterSelectData data)
        {
            if (data == null)
                return;

            string cacheKey = GetModelCacheKey(data);
            if (string.IsNullOrEmpty(cacheKey))
            {
                Debug.LogError("[CharacterSelectObject] 모델 캐시 키가 비어 있습니다.");
                return;
            }

            // 같은 모델이 이미 보여도 등장 디졸브를 다시 재생(시간 조절 테스트 포함)
            if (_currentModelKey == cacheKey &&
                _modelCache.TryGetValue(cacheKey, out CachedModel current) &&
                current.GameObject != null &&
                current.GameObject.activeSelf)
            {
                if (current.Model != null)
                    current.Model.InitializeModel(_appearDissolveDuration);
                return;
            }

            if (_modelCache.TryGetValue(cacheKey, out CachedModel cached) && cached.GameObject != null)
            {
                ActivateCachedModel(cacheKey);
                return;
            }

            ShowModelAsync(data, cacheKey);
        }

        private async void ShowModelAsync(CharacterSelectData data, string cacheKey)
        {
            if (_modelRoot == null)
            {
                Debug.LogError("[CharacterSelectObject] _modelRoot가 없습니다.");
                return;
            }

            if (string.IsNullOrEmpty(data.PrefabPath))
            {
                Debug.LogError($"[CharacterSelectObject] PrefabPath가 비어 있습니다: {data.Tid}");
                return;
            }

            if (_loadingKeys.Contains(cacheKey))
                return;

            _loadingKeys.Add(cacheKey);

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                _loadingKeys.Remove(cacheKey);
                Debug.LogError("[CharacterSelectObject] ResourceManager를 찾을 수 없습니다.");
                return;
            }

            GameObject modelObject = await resourceManager.InstantiateAsync(data.PrefabPath, _modelRoot);
            _loadingKeys.Remove(cacheKey);

            if (!_isShowing)
            {
                if (modelObject != null)
                    resourceManager.ReleaseInstance(modelObject);
                return;
            }

            if (_modelCache.ContainsKey(cacheKey))
            {
                if (modelObject != null)
                    resourceManager.ReleaseInstance(modelObject);

                if (IsSelectedModel(data))
                    ActivateCachedModel(cacheKey);
                return;
            }

            if (modelObject == null)
            {
                Debug.LogError($"[CharacterSelectObject] 모델 생성 실패: {data.PrefabPath}");
                return;
            }

            var model = modelObject.GetComponent<CharacterSelectModel>();
            if (model == null)
                model = modelObject.GetComponentInChildren<CharacterSelectModel>(true);
            if (model == null)
                model = modelObject.AddComponent<CharacterSelectModel>();

            model.Initialize(data);
            modelObject.SetActive(false);

            _modelCache[cacheKey] = new CachedModel
            {
                GameObject = modelObject,
                Model = model
            };

            if (IsSelectedModel(data))
                ActivateCachedModel(cacheKey);
        }

        private bool IsSelectedModel(CharacterSelectData data)
        {
            if (_selectedData == null || data == null)
                return false;

            return GetModelCacheKey(_selectedData) == GetModelCacheKey(data);
        }

        private void ActivateCachedModel(string cacheKey)
        {
            if (!_modelCache.TryGetValue(cacheKey, out CachedModel target) || target.GameObject == null)
                return;

            foreach (var pair in _modelCache)
            {
                if (pair.Value.GameObject == null)
                    continue;

                bool shouldActive = pair.Key == cacheKey;
                if (pair.Value.GameObject.activeSelf == shouldActive)
                    continue;

                if (!shouldActive && pair.Value.Model != null)
                    pair.Value.Model.StopAppearDissolve(showFully: true);

                pair.Value.GameObject.SetActive(shouldActive);
            }

            // 타겟은 항상 켠 뒤 디졸브 (이미 활성이어도 InitializeModel에서 재생)
            if (!target.GameObject.activeSelf)
                target.GameObject.SetActive(true);

            _currentModelKey = cacheKey;
            _currentModel = target.Model;

            if (_currentModel != null)
                _currentModel.InitializeModel(_appearDissolveDuration);
        }

        private void HideAllCachedModels()
        {
            foreach (var pair in _modelCache)
            {
                if (pair.Value.GameObject != null)
                    pair.Value.GameObject.SetActive(false);
            }

            _currentModelKey = null;
            _currentModel = null;
        }

        private void ReleaseAllCachedModels()
        {
            var resourceManager = GameManager.Instance?.ResourceManager;

            foreach (var pair in _modelCache)
            {
                if (pair.Value.GameObject == null)
                    continue;

                if (resourceManager != null)
                    resourceManager.ReleaseInstance(pair.Value.GameObject);
                else
                    Destroy(pair.Value.GameObject);
            }

            _modelCache.Clear();
            _loadingKeys.Clear();
            _currentModelKey = null;
            _currentModel = null;
        }

        private void OnDestroy()
        {
            UnbindUnitSetupUI();
            ReleaseAllCachedModels();
        }

        private struct CachedModel
        {
            public GameObject GameObject;
            public CharacterSelectModel Model;
        }
    }
}
