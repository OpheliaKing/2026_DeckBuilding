using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class CharacterBase
    {
        private CHARACTER_AI_TYPE _aiType = CHARACTER_AI_TYPE.NONE;
        public CHARACTER_AI_TYPE AIType => _aiType;

        private Coroutine _aiTurnRoutine;
        private bool _isAiTurnRunning;

        public void SetAIType(CHARACTER_AI_TYPE aiType)
        {
            _aiType = aiType;
        }

        /// <summary>
        /// AI 턴을 시작합니다. PLAYER 자동사냥 / 적 AI 공용.
        /// </summary>
        public void StartAITurn()
        {
            if (_aiType != CHARACTER_AI_TYPE.AI)
            {
                Debug.LogWarning($"[AI] AIType이 AI가 아닙니다: {name} / {_aiType}");
                return;
            }

            var inGame = GameManager.Instance?.InGameManager;
            if (inGame == null || inGame.IsBattleEnded)
                return;

            if (_isAiTurnRunning)
                return;

            if (_aiTurnRoutine != null)
                StopCoroutine(_aiTurnRoutine);

            _aiTurnRoutine = StartCoroutine(AITurnRoutine());
        }

        public void StopAITurn()
        {
            if (_aiTurnRoutine != null)
            {
                StopCoroutine(_aiTurnRoutine);
                _aiTurnRoutine = null;
            }

            _isAiTurnRunning = false;
        }

        private IEnumerator AITurnRoutine()
        {
            _isAiTurnRunning = true;

            var inGame = GameManager.Instance?.InGameManager;
            if (inGame == null || inGame.IsBattleEnded)
            {
                _isAiTurnRunning = false;
                _aiTurnRoutine = null;
                yield break;
            }

            yield return null;

            if (inGame.IsBattleEnded || IsDead)
            {
                _isAiTurnRunning = false;
                _aiTurnRoutine = null;
                yield break;
            }

            int remainingCost = GetAITurnCostBudget();
            Debug.Log($"[AI] 턴 시작: {name} / costBudget={remainingCost} / hand={UnitInfo?.Hand?.Count ?? 0}");

            while (!IsDead && remainingCost > 0 && !inGame.IsBattleEnded)
            {
                if (inGame.IsResolvingCard)
                {
                    yield return inGame.WaitUntilCardResolveComplete();
                    if (inGame.IsBattleEnded || IsDead)
                        break;
                    continue;
                }

                if (inGame.CurrentActor != this)
                    break;

                var decision = SelectNextCardPlay(remainingCost);
                if (decision == null)
                {
                    // 상대가 모두 죽은 경우 등 → 전투 종료 재확인 후 턴 넘기지 않음
                    inGame.EvaluateBattleEndFromAI();
                    if (!inGame.IsBattleEnded)
                        Debug.Log($"[AI] 사용 가능한 카드 없음 → 턴 종료: {name}");
                    break;
                }

                int cardCost = Mathf.Max(0, decision.Card.Cost);
                if (cardCost > remainingCost)
                {
                    Debug.Log($"[AI] 코스트 부족 → 턴 종료: {name} / need={cardCost} / left={remainingCost}");
                    break;
                }

                Debug.Log(
                    $"[AI] 카드 사용: {decision.Card.Name} → {decision.Target.name} / cost={cardCost}");

                if (!inGame.TryPlayCard(this, decision.Target, decision.Card))
                {
                    Debug.LogWarning($"[AI] 카드 사용 실패: {decision.Card.Name}");
                    inGame.EvaluateBattleEndFromAI();
                    yield return null;
                    break;
                }

                remainingCost -= cardCost;
                yield return inGame.WaitUntilCardResolveComplete();
            }

            bool shouldEndTurn = !IsDead &&
                                 !inGame.IsBattleEnded &&
                                 inGame.CurrentActor == this &&
                                 !inGame.IsResolvingCard;

            _isAiTurnRunning = false;
            _aiTurnRoutine = null;

            if (!shouldEndTurn)
                yield break;

            Debug.Log($"[AI] EndTurn: {name}");
            inGame.EndTurn();
        }

        /// <summary>
        /// 이번 턴에 쓸 수 있는 코스트 예산.
        /// 코스트 시스템 본구현 전까지는 넉넉히 두고, 실제 제한은 SelectNextCardPlay에서 Card.Cost로 합니다.
        /// </summary>
        protected virtual int GetAITurnCostBudget()
        {
            // TODO: UnitInfo.MaxCost / CurrentCost 연동
            return 99;
        }

        /// <summary>
        /// 다음에 사용할 카드와 대상을 고릅니다.
        /// 몬스터 패턴 / 자동사냥 전략은 이 함수를 오버라이드하거나 여기서 분기하면 됩니다.
        /// </summary>
        protected virtual AiCardPlayDecision SelectNextCardPlay(int remainingCost)
        {
            var inGame = GameManager.Instance?.InGameManager;
            if (inGame == null || UnitInfo == null)
                return null;

            var hand = UnitInfo.Hand;
            if (hand == null || hand.Count == 0)
                return null;

            // 기본 전략: 손패 앞에서부터, 코스트 가능한 카드 중 유효 대상이 있는 첫 카드
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                if (card == null)
                    continue;

                if (card.Cost > remainingCost)
                    continue;

                var target = SelectTargetForCard(card, inGame);
                if (target == null)
                    continue;

                return new AiCardPlayDecision(card, target);
            }

            return null;
        }

        /// <summary>
        /// 카드 타입에 맞는 대상 선택. 기본은 첫 유효 대상.
        /// </summary>
        protected virtual CharacterBase SelectTargetForCard(CardData card, InGameManager inGame)
        {
            if (card == null || inGame == null)
                return null;

            // SELF 버프는 본인 고정
            if (card.CardType == CARD_TYPE.BUFF &&
                card.BuffTargetType == CARD_BUFF_TARGET_TYPE.SELF)
            {
                return IsAlive ? this : null;
            }

            var candidates = inGame.GetValidTargets(this, card);
            if (candidates == null || candidates.Count == 0)
                return null;

            // 공격/디버프: 체력 낮은 적 우선 (간단한 기본 휴리스틱)
            if (card.CardType == CARD_TYPE.ATTACK || card.CardType == CARD_TYPE.DEBUFF)
            {
                CharacterBase best = candidates[0];
                for (int i = 1; i < candidates.Count; i++)
                {
                    if (candidates[i].UnitInfo.CurrentHp < best.UnitInfo.CurrentHp)
                        best = candidates[i];
                }

                return best;
            }

            return candidates[0];
        }
    }

    public sealed class AiCardPlayDecision
    {
        public CardData Card { get; }
        public CharacterBase Target { get; }

        public AiCardPlayDecision(CardData card, CharacterBase target)
        {
            Card = card;
            Target = target;
        }
    }

    public enum CHARACTER_AI_TYPE
    {
        NONE,
        PLAYER,
        AI,
    }
}
