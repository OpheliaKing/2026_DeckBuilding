using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class StageManager
    {
        private const string EventNoSuccessFallbackText = "아무 일도 일어나지 않았다.";

        private int _activeEventNodeId = -1;
        private StageEventData _activeEventData;
        private StageEventUI _activeEventUI;

        private void EnterEvent(StageNodeData node)
        {
            if (node == null)
                return;

            _activeEventNodeId = node.NodeId;
            SetStageNodeUIVisible(false);
            StartEventFlowAsync();
        }

        private async void StartEventFlowAsync()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[StageManager] GameManager가 없습니다.");
                AbortEventFlow();
                return;
            }

            StageEventDataSO eventDataSO = await gameManager.GetSOAsync<StageEventDataSO>(
                PublicVariable.Address.StageEventDataSO);

            if (eventDataSO == null)
            {
                Debug.LogError("[StageManager] StageEventDataSO 로드 실패");
                AbortEventFlow();
                return;
            }

            StageEventData eventData = eventDataSO.GetRandomEventData();
            if (eventData == null)
            {
                Debug.LogError("[StageManager] 랜덤 이벤트 데이터를 가져오지 못했습니다.");
                AbortEventFlow();
                return;
            }

            _activeEventData = eventData;

            UIManager uiManager = ResolveUIManager();
            if (uiManager == null)
            {
                AbortEventFlow();
                return;
            }

            uiManager.Show(PublicVariable.Address.StageEventUIPrefab, uiBase =>
            {
                if (uiBase is not StageEventUI eventUI)
                {
                    Debug.LogError("[StageManager] StageEventUI 컴포넌트가 없습니다.");
                    AbortEventFlow();
                    return;
                }

                _activeEventUI = eventUI;
                eventUI.Setup(eventData, OnEventChoiceSelected);
            });
        }

        private void OnEventChoiceSelected(int choiceIndex)
        {
            if (_activeEventData == null)
            {
                Debug.LogError("[StageManager] 활성 이벤트 데이터가 없습니다.");
                FinishEventFlow();
                return;
            }

            if (_activeEventData.Choices == null ||
                choiceIndex < 0 ||
                choiceIndex >= _activeEventData.Choices.Count)
            {
                Debug.LogError($"[StageManager] 잘못된 선택지 인덱스: {choiceIndex}");
                FinishEventFlow();
                return;
            }

            StageEventChoice choice = _activeEventData.Choices[choiceIndex];
            List<StageEventEffect> succeeded = ResolveAndApplyChoiceEffects(choice);

            string resultText;
            if (succeeded == null || succeeded.Count == 0)
            {
                Debug.LogError(
                    "[StageManager] 성공한 StageEventEffect가 없습니다. 기획상 최소 1개는 성공해야 합니다.");
                resultText = EventNoSuccessFallbackText;
            }
            else
            {
                StageEventEffect best = PickHighestPriorityEffect(succeeded);
                resultText = best != null && !string.IsNullOrEmpty(best.EventTextString)
                    ? best.EventTextString
                    : EventNoSuccessFallbackText;
            }

            if (_activeEventUI != null)
            {
                _activeEventUI.ShowResult(resultText, FinishEventFlow);
            }
            else
            {
                FinishEventFlow();
            }
        }

        private List<StageEventEffect> ResolveAndApplyChoiceEffects(StageEventChoice choice)
        {
            var succeeded = new List<StageEventEffect>();

            if (choice?.Effects == null || choice.Effects.Count == 0)
                return succeeded;

            for (int i = 0; i < choice.Effects.Count; i++)
            {
                StageEventEffect effect = choice.Effects[i];
                if (effect == null)
                    continue;

                if (!RollEffectSuccess(effect.Probability))
                    continue;

                ApplyEventEffect(effect);
                succeeded.Add(effect);
            }

            return succeeded;
        }

        private static bool RollEffectSuccess(float probability)
        {
            if (probability <= 0f)
                return false;

            if (probability >= 1f)
                return true;

            return Random.value <= probability;
        }

        private static StageEventEffect PickHighestPriorityEffect(List<StageEventEffect> effects)
        {
            StageEventEffect best = null;
            for (int i = 0; i < effects.Count; i++)
            {
                StageEventEffect effect = effects[i];
                if (effect == null)
                    continue;

                if (best == null || effect.Priority > best.Priority)
                    best = effect;
            }

            return best;
        }

        private void ApplyEventEffect(StageEventEffect effect)
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[StageManager] GameManager가 없습니다.");
                return;
            }

            UnitInfo player = null;
            var players = gameManager.PlayerCharacters;
            if (players != null && players.Count > 0)
                player = players[0];

            switch (effect.ResultType)
            {
                case STAGE_EVENT_RESULT_TYPE.GET_GOLD:
                    gameManager.AddGold(Mathf.Abs(effect.IntValue));
                    break;

                case STAGE_EVENT_RESULT_TYPE.LOSE_GOLD:
                    gameManager.AddGold(-Mathf.Abs(effect.IntValue));
                    break;

                case STAGE_EVENT_RESULT_TYPE.GET_HP:
                    if (player != null)
                        player.ApplyHeal(Mathf.Abs(effect.IntValue));
                    else
                        Debug.LogError("[StageManager] 플레이어가 없어 HP 회복을 적용할 수 없습니다.");
                    break;

                case STAGE_EVENT_RESULT_TYPE.LOSE_HP:
                    if (player != null)
                        player.ApplyDamage(Mathf.Abs(effect.IntValue));
                    else
                        Debug.LogError("[StageManager] 플레이어가 없어 HP 감소를 적용할 수 없습니다.");
                    break;

                case STAGE_EVENT_RESULT_TYPE.GET_ITEM:
                    if (player == null)
                    {
                        Debug.LogError("[StageManager] 플레이어가 없어 아이템을 적용할 수 없습니다.");
                        break;
                    }

                    if (!string.IsNullOrEmpty(effect.TidValue))
                        player.AddItem(effect.TidValue);
                    break;

                case STAGE_EVENT_RESULT_TYPE.GET_CARD:
                    if (player == null)
                    {
                        Debug.LogError("[StageManager] 플레이어가 없어 카드를 적용할 수 없습니다.");
                        break;
                    }

                    if (!string.IsNullOrEmpty(effect.TidValue))
                        gameManager.AddCard(player, effect.TidValue);
                    break;

                case STAGE_EVENT_RESULT_TYPE.NONE:
                    break;

                default:
                    Debug.LogWarning($"[StageManager] 미처리 이벤트 결과 타입: {effect.ResultType}");
                    break;
            }

            Debug.Log(
                $"[StageManager] 이벤트 효과 적용: {effect.ResultType} / int={effect.IntValue} / tid={effect.TidValue}");
        }

        private void FinishEventFlow()
        {
            if (_activeEventNodeId >= 0)
            {
                ApplyNodeCleared(_activeEventNodeId);
                SaveMapData();
            }

            var uiManager = ResolveUIManager();
            if (uiManager != null && uiManager.Current is StageEventUI)
                uiManager.Close();

            _activeEventNodeId = -1;
            _activeEventData = null;
            _activeEventUI = null;
            ReturnToStageNodeUI();
        }

        private void AbortEventFlow()
        {
            var uiManager = ResolveUIManager();
            if (uiManager != null && uiManager.Current is StageEventUI)
                uiManager.Close();

            _activeEventNodeId = -1;
            _activeEventData = null;
            _activeEventUI = null;
            ReturnToStageNodeUI();
        }
    }
}
