using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class GameManager : Singleton<GameManager>
    {
        [SerializeField] private ResourceManager _resourceManager;
        [SerializeField] private CameraManager _cameraManager;
        [SerializeField] private TimeManager _timeManager;

        private GameObject _currentStageObject;
        private InGameManager _inGameManager;

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

        public InGameManager InGameManager => _inGameManager;
        
    

        public void Start()
        {
            //아래 코드는 테스트용
            AddPlayerCharacter("player_0001", (unitInfo) =>
            {
                unitInfo.SetEquipType(CHARACTER_EQUIP_TYPE.BOW);
                if (PlayerCharacters.Count > 0)
                {
                    var cardTids = new List<string> { "card_001", "card_001", "card_buff_001" };
                    AddCard(PlayerCharacters[0], cardTids, (unitInfo) =>
                    {
                        Debug.Log($"[GameManager] 카드 추가 완료: {unitInfo.DeckCardList.Count}");
                    }); ;
                }
            });

            InGameStart("stage_0001");
        }

        public void InGameStart(string stageTid)
        {
            GetSOAsync<StageDataSO>(PublicVariable.Address.StageDataSO, stageDataSO =>
            {
                if (stageDataSO == null)
                {
                    Debug.LogError("[GameManager] StageDataSO 로드 실패");
                    return;
                }

                var stageData = stageDataSO.GetStageData(stageTid);
                if (stageData == null)
                {
                    Debug.LogError($"[GameManager] StageData 로드 실패: {stageTid}");
                    return;
                }

                if (string.IsNullOrEmpty(stageData.stagePrefabPath))
                {
                    Debug.LogError($"[GameManager] stagePrefabPath가 비어 있습니다: {stageTid}");
                    return;
                }

                LoadStagePrefab(stageData);
            });
        }

        private void LoadStagePrefab(StageData stageData)
        {
            ClearCurrentStage();

            ResourceManager.InstantiateAsync(stageData.stagePrefabPath, stageObject =>
            {
                if (stageObject == null)
                {
                    Debug.LogError($"[GameManager] 스테이지 프리팹 생성 실패: {stageData.stagePrefabPath}");
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
