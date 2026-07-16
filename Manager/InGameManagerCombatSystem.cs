using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SHIN
{
    public partial class InGameManager
    {
        private InGameCardObject _selectedCardObject;
        private CardData _selectedCard;
        private bool _isWaitingForTarget;
        private bool _isResolvingCard;
        private CardResolveSession _resolveSession;

        public CardData SelectedCard => _selectedCard;
        public bool IsWaitingForTarget => _isWaitingForTarget;
        public bool IsResolvingCard => _isResolvingCard;

        private sealed class CardResolveSession
        {
            public CharacterBase User;
            public CharacterBase Target;
            public CardData Card;
            public int TotalDamage;
            public float[] HitWeights;
            public int[] HitDamages;
            public int NextHitIndex;
            public bool SetupReceived;
        }

        private void Update()
        {
            if (!_isWaitingForTarget || _isResolvingCard)
                return;

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelCardSelection();
                Debug.Log("[Combat] 카드 선택이 취소되었습니다.");
                return;
            }

            if (!Input.GetMouseButtonDown(0))
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var target = RaycastCharacterFromCamera();
            if (target != null)
                OnCombatTargetSelected(target);
        }

        public void OnCardClicked(InGameCardObject cardObject)
        {
            if (_isResolvingCard)
            {
                Debug.LogWarning("[Combat] 카드 연출 중에는 선택할 수 없습니다.");
                return;
            }

            if (cardObject == null || cardObject.CardData == null)
            {
                Debug.LogWarning("[Combat] 유효하지 않은 카드입니다.");
                return;
            }

            if (CurrentActor == null || !IsPlayerCharacter(CurrentActor))
            {
                Debug.LogWarning("[Combat] 플레이어 턴이 아닙니다.");
                return;
            }

            if (CurrentActor.IsDead)
            {
                Debug.LogWarning("[Combat] 현재 행동자가 사망 상태입니다.");
                return;
            }

            if (_isWaitingForTarget &&
                (_selectedCardObject == cardObject || _selectedCard == cardObject.CardData))
            {
                CancelCardSelection();
                Debug.Log("[Combat] 같은 카드 재선택으로 선택이 취소되었습니다.");
                return;
            }

            _selectedCardObject = cardObject;
            _selectedCard = cardObject.CardData;
            BeginTargetSelection(_selectedCard);

            Debug.Log($"[Combat] 카드 선택: {_selectedCard.Name} ({_selectedCard.CardType})");
        }

        private void BeginTargetSelection(CardData card)
        {
            _isWaitingForTarget = true;
            Debug.Log($"[Combat] 대상 선택 대기 중... (타입: {card?.CardType}) / 우클릭·Esc 취소");
        }

        private CharacterBase RaycastCharacterFromCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Combat] Main Camera가 없습니다.");
                return null;
            }

            var ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit3D, 500f))
            {
                var character = hit3D.collider.GetComponentInParent<CharacterBase>();
                if (character != null)
                    return character;
            }

            var hit2D = Physics2D.GetRayIntersection(ray, 500f);
            if (hit2D.collider != null)
            {
                var character = hit2D.collider.GetComponentInParent<CharacterBase>();
                if (character != null)
                    return character;
            }

            return null;
        }

        public void OnCombatTargetSelected(CharacterBase target)
        {
            if (_isResolvingCard)
                return;

            if (!_isWaitingForTarget || _selectedCard == null)
            {
                Debug.LogWarning("[Combat] 카드가 선택되지 않은 상태에서 대상을 선택할 수 없습니다.");
                return;
            }

            if (CurrentActor == null)
            {
                Debug.LogError("[Combat] 현재 행동자가 없습니다.");
                ClearCardSelection();
                return;
            }

            if (target == null || target.IsDead)
            {
                Debug.LogWarning("[Combat] 유효하지 않은 대상입니다.");
                return;
            }

            if (!IsValidTarget(CurrentActor, target, _selectedCard))
            {
                Debug.LogWarning(
                    $"[Combat] 이 카드({_selectedCard.CardType})의 대상이 올바르지 않습니다: {GetCombatName(target)}");
                return;
            }

            var user = CurrentActor;
            var card = _selectedCard;
            ClearCardSelection();

            UseCard(user, target, card);
        }

        /// <summary>
        /// UseCard → 애니 재생(없으면 즉시 효과) → 애니 판정 타이밍에 효과 → 종료 후 소모/사망 처리
        /// </summary>
        private void UseCard(CharacterBase user, CharacterBase target, CardData card)
        {
            if (user == null || target == null || card == null)
            {
                Debug.LogError("[Combat] UseCard 인자가 null입니다.");
                return;
            }

            StartCoroutine(UseCardRoutine(user, target, card));
        }

        private IEnumerator UseCardRoutine(CharacterBase user, CharacterBase target, CardData card)
        {
            if (_isResolvingCard)
            {
                Debug.LogWarning("[Combat] 이미 카드 연출 중입니다.");
                yield break;
            }

            _isResolvingCard = true;
            _resolveSession = CreateResolveSession(user, target, card);

            bool hasAnim = !string.IsNullOrEmpty(card.AnimationName) &&
                           user.TryPlayCardAnimation(card.AnimationName);

            if (!hasAnim)
            {
                ApplyImmediateCardEffect(_resolveSession);
                FinishCardResolve(_resolveSession);
                _resolveSession = null;
                _isResolvingCard = false;
                yield break;
            }

            Debug.Log($"[Combat] 애니 재생: {card.AnimationName} / {card.Name}");
            yield return user.WaitCurrentAnimationEnd(card.AnimationName);

            if (_resolveSession != null &&
                _resolveSession.Card.CardType == CARD_TYPE.ATTACK &&
                _resolveSession.HitDamages != null &&
                _resolveSession.NextHitIndex < _resolveSession.HitDamages.Length)
            {
                Debug.LogWarning(
                    $"[Combat] Hit 판정 부족: {_resolveSession.NextHitIndex}/{_resolveSession.HitDamages.Length}. " +
                    "남은 히트 데미지는 적용되지 않습니다. CombatAnimStateBehaviour의 HitWeights/Judgments를 확인하세요.");
            }

            FinishCardResolve(_resolveSession);
            _resolveSession = null;
            _isResolvingCard = false;
        }

        private CardResolveSession CreateResolveSession(CharacterBase user, CharacterBase target, CardData card)
        {
            var session = new CardResolveSession
            {
                User = user,
                Target = target,
                Card = card,
            };

            if (card.CardType == CARD_TYPE.ATTACK)
            {
                int damage = CalculateDamage(user, target, card);
                session.TotalDamage = ApplyAttackExtraEffects(user, target, card, damage);
            }

            return session;
        }

        private void ApplyImmediateCardEffect(CardResolveSession session)
        {
            if (session == null)
                return;

            switch (session.Card.CardType)
            {
                case CARD_TYPE.ATTACK:
                    ApplyAttackHitDamage(session, session.TotalDamage, CameraShakeLevel.None, isLastHit: true);
                    break;
                case CARD_TYPE.DEFENSE:
                    Debug.Log($"[Combat][DEFENSE] {GetCombatName(session.User)} → {GetCombatName(session.Target)} / {session.Card.Name} (구현 예정)");
                    break;
                case CARD_TYPE.BUFF:
                    Debug.Log($"[Combat][BUFF] {GetCombatName(session.User)} → {GetCombatName(session.Target)} / {session.Card.Name} (구현 예정)");
                    break;
                case CARD_TYPE.DEBUFF:
                    Debug.Log($"[Combat][DEBUFF] {GetCombatName(session.User)} → {GetCombatName(session.Target)} / {session.Card.Name} (구현 예정)");
                    break;
                case CARD_TYPE.SPECIAL:
                    Debug.Log($"[Combat][SPECIAL] {GetCombatName(session.User)} → {GetCombatName(session.Target)} / {session.Card.Name} (구현 예정)");
                    break;
                default:
                    Debug.LogWarning($"[Combat] 지원하지 않는 카드 타입: {session.Card.CardType}");
                    break;
            }
        }

        /// <summary>
        /// CombatAnimStateBehaviour OnStateEnter → HitWeightsCsv
        /// </summary>
        public void OnAnimCombatSetup(CharacterBase source, string ratiosCsv)
        {
            if (_resolveSession == null || source == null || source != _resolveSession.User)
                return;

            // 분할 State 후반에서 Setup이 다시 오면 Hit 인덱스가 리셋되므로 무시
            if (_resolveSession.SetupReceived &&
                _resolveSession.HitDamages != null &&
                _resolveSession.HitDamages.Length > 0)
                return;

            var weights = CombatDamageSplit.ParseWeightsCsv(ratiosCsv);
            _resolveSession.HitWeights = weights;
            _resolveSession.HitDamages = CombatDamageSplit.SplitByWeights(_resolveSession.TotalDamage, weights);
            _resolveSession.NextHitIndex = 0;
            _resolveSession.SetupReceived = true;

            Debug.Log(
                $"[Combat] Hit Setup: [{string.Join(",", weights)}] → 데미지조각 [{string.Join(",", _resolveSession.HitDamages)}] / 총합:{_resolveSession.TotalDamage}");
        }

        /// <summary>
        /// CombatAnimStateBehaviour 판정 큐.
        /// Setup 없이 Hit만 오면 단일 타격(전체 데미지)으로 처리합니다.
        /// </summary>
        public void OnAnimCombatJudgment(
            CharacterBase source,
            CombatJudgmentType type,
            float ratio,
            CameraShakeLevel cameraShake = CameraShakeLevel.None)
        {
            if (_resolveSession == null || source == null || source != _resolveSession.User)
                return;

            var session = _resolveSession;
            var card = session.Card;

            switch (type)
            {
                case CombatJudgmentType.Hit:
                    HandleAnimHit(session, cameraShake);
                    break;
                case CombatJudgmentType.Buff:
                    Debug.Log($"[Combat][Anim][BUFF] {card.Name} (구현 예정)");
                    break;
                case CombatJudgmentType.Debuff:
                    Debug.Log($"[Combat][Anim][DEBUFF] {card.Name} (구현 예정)");
                    break;
                case CombatJudgmentType.Defense:
                    Debug.Log($"[Combat][Anim][DEFENSE] {card.Name} (구현 예정)");
                    break;
                case CombatJudgmentType.Special:
                    Debug.Log($"[Combat][Anim][SPECIAL] {card.Name} (구현 예정)");
                    break;
            }
        }

        private void HandleAnimHit(CardResolveSession session, CameraShakeLevel cameraShake)
        {
            if (session.Card.CardType != CARD_TYPE.ATTACK)
                return;

            if (session.Target == null)
                return;

            // Setup이 없으면 단일 타격
            if (!session.SetupReceived || session.HitDamages == null)
            {
                session.HitWeights = new[] { 1f };
                session.HitDamages = new[] { session.TotalDamage };
                session.NextHitIndex = 0;
                session.SetupReceived = true;
                Debug.LogWarning("[Combat] HitWeights Setup 없이 Hit → 전체 데미지 1회 적용");
            }

            if (session.NextHitIndex >= session.HitDamages.Length)
            {
                Debug.LogWarning("[Combat] Setup된 Hit 횟수를 초과했습니다.");
                return;
            }

            int portion = session.HitDamages[session.NextHitIndex];
            session.NextHitIndex++;
            bool isLastHit = session.NextHitIndex >= session.HitDamages.Length;
            ApplyAttackHitDamage(session, portion, cameraShake, isLastHit);
        }

        private void ApplyAttackHitDamage(
            CardResolveSession session,
            int damage,
            CameraShakeLevel cameraShake = CameraShakeLevel.None,
            bool isLastHit = false)
        {
            if (session?.Target == null || damage < 0)
                return;

            // 판정 타이밍: Hit + 카메라 흔들기
            session.Target.PlayHitAnimation();

            int applied = session.Target.TakeDamage(damage);
            Debug.Log(
                $"[Combat][HIT] {GetCombatName(session.User)} → {GetCombatName(session.Target)} / {session.Card.Name} / " +
                $"히트데미지:{applied} / 남은HP:{session.Target.UnitInfo.CurrentHp}");

            if (cameraShake != CameraShakeLevel.None)
            {
                GameManager.Instance?.CameraManager?.Shake(cameraShake);
                // 쉐이크와 같은 타이밍에 캐릭터 애니/이펙트 히트스톱
                GameManager.Instance?.TimeManager?.HitStop();
            }

            // 마지막 공격 판정에서만 사망 처리 (Die/디졸브)
            if (isLastHit && session.Target.IsDead)
                ProcessDeath(session.Target);
        }

        private void FinishCardResolve(CardResolveSession session)
        {
            if (session == null)
                return;

            ConsumePlayedCard(session.User, session.Card);

            // 대상 사망은 마지막 Hit 판정에서 이미 처리됨
            if (session.User != null && session.User.IsDead)
                ProcessDeath(session.User);
        }

        private bool IsValidTarget(CharacterBase user, CharacterBase target, CardData card)
        {
            if (user == null || target == null || card == null || target.IsDead)
                return false;

            bool targetIsPlayer = IsPlayerCharacter(target);

            switch (card.CardType)
            {
                case CARD_TYPE.ATTACK:
                case CARD_TYPE.DEBUFF:
                    return !targetIsPlayer;

                case CARD_TYPE.DEFENSE:
                case CARD_TYPE.BUFF:
                    return targetIsPlayer;

                case CARD_TYPE.SPECIAL:
                    return true;

                default:
                    return false;
            }
        }

        public void CancelCardSelection()
        {
            ClearCardSelection();
        }

        private void ClearCardSelection()
        {
            _selectedCardObject = null;
            _selectedCard = null;
            _isWaitingForTarget = false;
        }

        /// <summary>외부/테스트용. 애니 없이 즉시 공격 1회 처리합니다.</summary>
        public void ProcessAttack(CharacterBase attacker, CharacterBase defender, CardData card)
        {
            UseCard(attacker, defender, card);
        }

        private void ProcessDefense(CharacterBase user, CharacterBase target, CardData card)
        {
            UseCard(user, target, card);
        }

        private void ProcessBuff(CharacterBase user, CharacterBase target, CardData card)
        {
            UseCard(user, target, card);
        }

        private void ProcessDebuff(CharacterBase user, CharacterBase target, CardData card)
        {
            UseCard(user, target, card);
        }

        private void ProcessSpecial(CharacterBase user, CharacterBase target, CardData card)
        {
            UseCard(user, target, card);
        }

        private int CalculateDamage(CharacterBase attacker, CharacterBase defender, CardData card)
        {
            float multiplier = card.AttackMultiplier > 0f ? card.AttackMultiplier : 1f;
            float raw = attacker.UnitInfo.CurrentAttack * multiplier;
            int damage = Mathf.FloorToInt(raw) - defender.UnitInfo.CurrentDefense;
            return Mathf.Max(0, damage);
        }

        private int ApplyAttackExtraEffects(
            CharacterBase attacker,
            CharacterBase defender,
            CardData card,
            int damage)
        {
            if (card.AttackEvent == null || card.AttackEvent.Count == 0)
                return damage;

            for (int i = 0; i < card.AttackEvent.Count; i++)
            {
                var eventId = card.AttackEvent[i];
                if (string.IsNullOrEmpty(eventId))
                    continue;

                Debug.Log($"[Combat] 공격 추가효과 예약: {eventId}");
                damage = ApplySingleAttackEvent(attacker, defender, card, eventId, damage);
            }

            return Mathf.Max(0, damage);
        }

        private int ApplySingleAttackEvent(
            CharacterBase attacker,
            CharacterBase defender,
            CardData card,
            string eventId,
            int damage)
        {
            return damage;
        }

        private void ConsumePlayedCard(CharacterBase attacker, CardData card)
        {
            if (attacker?.UnitInfo == null || card == null)
                return;

            if (!attacker.UnitInfo.DiscardFromHand(card))
            {
                Debug.LogWarning($"[Combat] 손패에서 카드를 제거하지 못했습니다: {card.Tid}");
                return;
            }

            if (IsPlayerCharacter(attacker) && PlayerUI != null)
                PlayerUI.RefreshHand(attacker.UnitInfo.Hand);
        }

        private void ProcessDeath(CharacterBase character)
        {
            if (character == null)
                return;

            // 마지막 Hit에서 이미 처리 중/완료면 중복 호출 방지
            if (character.IsDissolving || character.HasCompletedDeathVisual)
                return;

            Debug.Log($"[Combat] 사망 처리: {GetCombatName(character)}");

            RemoveFromTurnSystem(character);
            _playerCharacters.Remove(character);
            _enemyCharacters.Remove(character);

            // 클릭 불가
            var cols = character.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = false;

            var cols2d = character.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < cols2d.Length; i++)
                cols2d[i].enabled = false;

            StartCoroutine(ProcessDeathRoutine(character));
            CheckBattleEnd();
        }

        private IEnumerator ProcessDeathRoutine(CharacterBase character)
        {
            if (character == null)
                yield break;

            if (!character.HasCompletedDeathVisual && !character.IsDissolving)
            {
                // InGameManager에서 직접 열거형을 돌려도 되지만,
                // 캐릭터 StartCoroutine으로 실행해 중간에 끊기지 않게 한다.
                var deathRoutine = character.StartCoroutine(character.PlayDeathDissolve(playDeathAnimation: true));
                yield return deathRoutine;
            }
            else if (character.IsDissolving)
            {
                while (character != null && character.IsDissolving)
                    yield return null;
            }

            if (character != null)
                character.gameObject.SetActive(false);
        }

        private void CheckBattleEnd()
        {
            bool anyPlayerAlive = false;
            for (int i = 0; i < _playerCharacters.Count; i++)
            {
                if (_playerCharacters[i] != null && _playerCharacters[i].IsAlive)
                {
                    anyPlayerAlive = true;
                    break;
                }
            }

            bool anyEnemyAlive = false;
            for (int i = 0; i < _enemyCharacters.Count; i++)
            {
                if (_enemyCharacters[i] != null && _enemyCharacters[i].IsAlive)
                {
                    anyEnemyAlive = true;
                    break;
                }
            }

            if (!anyEnemyAlive)
                Debug.Log("[Combat] 전투 승리");
            else if (!anyPlayerAlive)
                Debug.Log("[Combat] 전투 패배");
        }

        private static string GetCombatName(CharacterBase character)
        {
            if (character?.UnitInfo?.UnitData == null)
                return character != null ? character.name : "Unknown";

            return string.IsNullOrEmpty(character.UnitInfo.UnitData.unitName)
                ? character.UnitInfo.UnitData.unitTid
                : character.UnitInfo.UnitData.unitName;
        }
    }
}
