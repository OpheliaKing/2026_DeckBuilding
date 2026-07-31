using UnityEngine;

namespace SHIN
{
    public partial class GameManager : Singleton<GameManager>
    {
        [SerializeField] private ResourceManager _resourceManager;
        [SerializeField] private CameraManager _cameraManager;
        [SerializeField] private TimeManager _timeManager;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private StageManager _stageManager;
        [SerializeField] private SoundManager _soundManager;

        private CharacterSelectObject _characterSelectObject;
        private GameObject _characterSelectObjectInstance;
        private GameObject _currentStageObject;
        private InGameManager _inGameManager;
        private bool _isBooting;
        private bool _hasSaveData;
        private UnitSetupUI _bootUnitSetupUI;

        public ResourceManager ResourceManager
        {
            get
            {
                ManagerBase.EnsureManager(transform, ref _resourceManager);
                return _resourceManager;
            }
        }

        public CameraManager CameraManager
        {
            get
            {
                ManagerBase.EnsureManager(transform, ref _cameraManager);
                return _cameraManager;
            }
        }

        public TimeManager TimeManager
        {
            get
            {
                ManagerBase.EnsureManager(transform, ref _timeManager);
                return _timeManager;
            }
        }

        public UIManager UIManager
        {
            get
            {
                ManagerBase.EnsureManager(transform, ref _uiManager);
                return _uiManager;
            }
        }

        public InGameManager InGameManager => _inGameManager;

        public StageManager StageManager
        {
            get
            {
                ManagerBase.EnsureManager(transform, ref _stageManager);
                return _stageManager;
            }
        }

        public SoundManager SoundManager
        {
            get
            {
                ManagerBase.EnsureManager(transform, ref _soundManager);
                return _soundManager;
            }
        }

        /// <summary>BootFlow에서 확인한 세이브 존재 여부.</summary>
        public bool HasSaveData => _hasSaveData;

        public void Start()
        {
            BootFlow();
        }

        public void GameStart()
        {
            BootFlow();
        }

        /// <summary>
        /// 세이브 유무만 확인한 뒤 StartUI를 연다.
        /// 실제 새 게임/이어하기는 StartUI 시작 버튼에서 분기한다.
        /// </summary>
        public void BootFlow()
        {
            if (_isBooting)
                return;

            _isBooting = true;
            BootFlowAsync();
        }

        private async void BootFlowAsync()
        {
            await InitializeSOIndexesAsync();

            UIManager uiManager = UIManager;
            if (uiManager != null)
                await uiManager.PreloadFadeUIAsync();

            _hasSaveData = StageManager.HasSaveData();
            Debug.Log($"[GameManager] BootFlow 세이브 유무: {_hasSaveData}");

            ShowStartUI();
        }

        /// <summary>
        /// Boot 시점에 자주 쓰는 SO를 로드하고 조회용 인덱스를 미리 만든다.
        /// </summary>
        private async System.Threading.Tasks.Task InitializeSOIndexesAsync()
        {
            CardDataSO cardDataSO = await GetSOAsync<CardDataSO>(PublicVariable.Address.CardDataSO);
            if (cardDataSO == null)
                Debug.LogError("[GameManager] CardDataSO 초기화 실패");
            else
                cardDataSO.BuildIndex();

            ItemDataSO itemDataSO = await GetSOAsync<ItemDataSO>(PublicVariable.Address.ItemDataSO);
            if (itemDataSO == null)
                Debug.LogError("[GameManager] ItemDataSO 초기화 실패");
            else
                itemDataSO.BuildRewardIndex();

            StageStepDataSO stageStepDataSO =
                await GetSOAsync<StageStepDataSO>(PublicVariable.Address.StageStepDataSO);
            if (stageStepDataSO == null)
                Debug.LogError("[GameManager] StageStepDataSO 초기화 실패");
        }

        /// <summary>
        /// StartUI 새 게임 버튼.
        /// 즉시 페이드 커버(알파 1) → 로딩 → 준비되면 페이드인.
        /// </summary>
        public void OnTitleNewGameClicked()
        {
            UIManager uiManager = UIManager;
            if (uiManager == null)
            {
                StartNewRunCharacterSetup();
                return;
            }

            uiManager.BeginFadeCover(StartNewRunCharacterSetup);
        }

        /// <summary>
        /// StartUI 이어하기 버튼.
        /// </summary>
        public void OnTitleContinueClicked()
        {
            ContinueRun();
        }

        private void ShowStartUI()
        {
            UIManager uiManager = UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[GameManager] UIManager가 없습니다.");
                _isBooting = false;
                return;
            }

            uiManager.Show(PublicVariable.Address.StartUIPrefab, uiBase =>
            {
                if (uiBase is not StartUI startUI)
                {
                    Debug.LogError("[GameManager] StartUI 컴포넌트가 없습니다.");
                    _isBooting = false;
                    return;
                }

                startUI.Setup(_hasSaveData);
                _isBooting = false;
            });
        }

        /// <summary>
        /// 엔딩 시퀀스 종료 후 런 진행을 초기화하고 타이틀로 복귀한다.
        /// </summary>
        public void ReturnToTitleAfterEnding()
        {
            ClearPlayerCharacters();
            _playerGold = 0;
            _hasSaveData = false;
            ClearInGameStage();

            UIManager uiManager = UIManager;
            if (uiManager != null)
                uiManager.CloseAll();

            Debug.Log("[GameManager] 엔딩 종료 → 타이틀(StartUI) 복귀, 세이브 초기화");

            // CloseAll이 Fade를 지울 수 있으므로 타이틀 전에 다시 프리로드
            PreloadFadeThenShowStartUI();
        }

        private async void PreloadFadeThenShowStartUI()
        {
            UIManager uiManager = UIManager;
            if (uiManager != null)
                await uiManager.PreloadFadeUIAsync();

            ShowStartUI();
        }

        private void ContinueRun()
        {
            if (!StageManager.TryLoadRun())
            {
                Debug.LogWarning("[GameManager] 세이브 로드 실패 → 새 게임으로 전환합니다.");
                _hasSaveData = false;
                OnTitleNewGameClicked();
                return;
            }

            // TODO: StageMapSaveData 플레이어 스냅샷 복원
            Debug.Log("[GameManager] 이어하기 → StageUI");
            StageManager.ShowStageUI();
        }

        private void StartNewRunCharacterSetup()
        {
            ClearPlayerCharacters();
            ClearInGameStage();
            SpawnCharacterSelectObjectAsync();
        }

        private async void SpawnCharacterSelectObjectAsync()
        {
            ReleaseCharacterSelectObject();

            GameObject instance = await ResourceManager.InstantiateAsync(
                PublicVariable.Address.CharacterSelectObjectPrefab);

            if (instance == null)
            {
                Debug.LogError(
                    $"[GameManager] CharacterSelectObject 생성 실패: {PublicVariable.Address.CharacterSelectObjectPrefab}");
                OpenUnitSetupUIFallback();
                return;
            }

            _characterSelectObjectInstance = instance;
            _characterSelectObject = instance.GetComponent<CharacterSelectObject>();
            if (_characterSelectObject == null)
                _characterSelectObject = instance.GetComponentInChildren<CharacterSelectObject>(true);

            if (_characterSelectObject == null)
            {
                Debug.LogError("[GameManager] CharacterSelectObject 컴포넌트가 없습니다.");
                ReleaseCharacterSelectObject();
                OpenUnitSetupUIFallback();
                return;
            }

            _characterSelectObject.OnSetupCompleted -= OnNewRunSetupCompleted;
            _characterSelectObject.OnSetupCompleted += OnNewRunSetupCompleted;
            _characterSelectObject.Show();
        }

        private void OnNewRunSetupCompleted(UnitInfo unitInfo)
        {
            if (_characterSelectObject != null)
                _characterSelectObject.OnSetupCompleted -= OnNewRunSetupCompleted;

            if (_bootUnitSetupUI != null)
            {
                _bootUnitSetupUI.OnSetupCompleted -= OnNewRunSetupCompleted;
                _bootUnitSetupUI = null;
            }

            ReleaseCharacterSelectObject();

            if (unitInfo == null)
            {
                Debug.LogError("[GameManager] 새 런 캐릭터 세팅 결과가 null입니다.");
                return;
            }

            StageManager.CreateNewRun();
            StageManager.ShowStageUI();
        }

        private void ReleaseCharacterSelectObject()
        {
            if (_characterSelectObject != null)
            {
                _characterSelectObject.OnSetupCompleted -= OnNewRunSetupCompleted;
                _characterSelectObject = null;
            }

            if (_characterSelectObjectInstance == null)
                return;

            ResourceManager.ReleaseInstance(_characterSelectObjectInstance);
            _characterSelectObjectInstance = null;
        }

        private void OpenUnitSetupUIFallback()
        {
            UIManager uiManager = UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[GameManager] UIManager가 없습니다.");
                return;
            }

            uiManager.Show(PublicVariable.Address.UnitSetupUIPrefab, uiBase =>
            {
                if (uiBase is not UnitSetupUI unitSetupUI)
                {
                    Debug.LogError("[GameManager] UnitSetupUI 컴포넌트가 없습니다.");
                    uiManager.SignalContentReady();
                    return;
                }

                _bootUnitSetupUI = unitSetupUI;
                _bootUnitSetupUI.OnSetupCompleted -= OnNewRunSetupCompleted;
                _bootUnitSetupUI.OnSetupCompleted += OnNewRunSetupCompleted;
                _bootUnitSetupUI.BeginSetup(onContentReady: () => uiManager.SignalContentReady());
            });
        }

        public void InGameStart(string stageTid)
        {
            GetSOAsync<StageDataSO>(PublicVariable.Address.StageDataSO, stageDataSO =>
            {
                if (stageDataSO == null)
                {
                    Debug.LogError("[GameManager] StageDataSO 로드 실패");
                    UIManager?.SignalContentReady();
                    return;
                }

                var stageData = stageDataSO.GetStageData(stageTid);
                if (stageData == null)
                {
                    Debug.LogError($"[GameManager] StageData 로드 실패: {stageTid}");
                    UIManager?.SignalContentReady();
                    return;
                }

                if (string.IsNullOrEmpty(stageData.stagePrefabPath))
                {
                    Debug.LogError($"[GameManager] stagePrefabPath가 비어 있습니다: {stageTid}");
                    UIManager?.SignalContentReady();
                    return;
                }

                LoadStagePrefab(stageData);
            });
        }

        /// <summary>
        /// 인게임 스테이지 인스턴스를 해제합니다.
        /// </summary>
        public void ClearInGameStage()
        {
            ClearCurrentStage();
        }

        private void LoadStagePrefab(StageData stageData)
        {
            ClearCurrentStage();

            ResourceManager.InstantiateAsync(stageData.stagePrefabPath, stageObject =>
            {
                if (stageObject == null)
                {
                    Debug.LogError($"[GameManager] 스테이지 프리팹 생성 실패: {stageData.stagePrefabPath}");
                    UIManager?.SignalContentReady();
                    return;
                }

                _currentStageObject = stageObject;
                _inGameManager = stageObject.GetComponentInChildren<InGameManager>(true);

                if (_inGameManager == null)
                    _inGameManager = stageObject.AddComponent<InGameManager>();

                _inGameManager.StageInit(stageData);
                Debug.Log($"[GameManager] 스테이지 로드 완료: {stageData.stageTid}");
            });
        }

        private void ClearCurrentStage()
        {
            if (_currentStageObject == null)
            {
                _inGameManager = null;
                return;
            }

            ResourceManager.ReleaseInstance(_currentStageObject);
            _currentStageObject = null;
            _inGameManager = null;
        }
    }
}
