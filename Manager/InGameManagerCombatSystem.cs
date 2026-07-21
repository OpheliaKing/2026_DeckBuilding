using System.Collections;
using System.Collections.Generic;
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
        private bool _isBattleEnded;
        private CardResolveSession _resolveSession;

        public CardData SelectedCard => _selectedCard;
        public bool IsWaitingForTarget => _isWaitingForTarget;
        public bool IsResolvingCard => _isResolvingCard;
        public bool IsBattleEnded => _isBattleEnded;

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
            public List<CharacterBase> BuffTargets;
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

            if (!CurrentActor.UnitInfo.CanAffordCard(cardObject.CardData))
            {
                NotifyInsufficientCardCost(CurrentActor, cardObject.CardData);
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

        /// <summary>
        /// 카드 코스트 부족 시 호출. UI 연동 지점.
        /// </summary>
        private void NotifyInsufficientCardCost(CharacterBase user, CardData card)
        {
            int need = card != null ? card.Cost : 0;
            int current = user?.UnitInfo != null ? user.UnitInfo.CurrentCardCost : 0;
            int max = user?.UnitInfo != null ? user.UnitInfo.MaxCardCost : 0;

            Debug.LogWarning(
                $"[Combat] 코스트 부족: {card?.Name} / 필요:{need} / 현재:{current}/{max}");

            // TODO: 코스트 부족 UI 출력
            // PlayerUI?.ShowInsufficientCost(need, current, max);
        }

        private void BeginTargetSelection(CardData card)
        {
            _isWaitingForTarget = true;

            // 버프만 팀 카메라 사용. 다른 카드로 전환 시 반드시 끔.
            bool useBuffCamera = card != null && card.CardType == CARD_TYPE.BUFF;
            SetBuffTargetCameraActive(useBuffCamera);

            Debug.Log($"[Combat] 대상 선택 대기 중... (타입: {card?.CardType}) / 우클릭·Esc 취소");
        }

        private void SetBuffTargetCameraActive(bool active)
        {
            var teamCamera = _playerGroupPosition?.TeamTargrtCamera;
            if (teamCamera == null)
                return;

            teamCamera.gameObject.SetActive(active);
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

            if (target == null)
            {
                Debug.LogWarning("[Combat] 유효하지 않은 대상입니다.");
                return;
            }

            if (target.IsDead)
            {
                if (!CanSelectDeadUnitAsCardTarget(CurrentActor, target, _selectedCard))
                {
                    // TODO: 죽은 유닛 대상 불가 UI
                    return;
                }
            }
            else if (!IsValidTarget(CurrentActor, target, _selectedCard))
            {
                if (_selectedCard.CardType == CARD_TYPE.BUFF)
                {
                    // TODO: 잘못된 대상 UI 표시
                    return;
                }

                Debug.LogWarning(
                    $"[Combat] 이 카드({_selectedCard.CardType})의 대상이 올바르지 않습니다: {GetCombatName(target)}");
                return;
            }

            var user = CurrentActor;
            var card = _selectedCard;
            bool keepBuffCamera = card.CardType == CARD_TYPE.BUFF;
            ClearCardSelection(keepBuffCamera);

            UseCard(user, target, card);
        }

        /// <summary>
        /// AI/자동사냥용. 플레이어 조작과 동일한 UseCard 경로로 카드를 사용합니다.
        /// </summary>
        public bool TryPlayCard(CharacterBase user, CharacterBase target, CardData card)
        {
            if (_isResolvingCard)
            {
                Debug.LogWarning("[Combat] 카드 연출 중에는 사용할 수 없습니다.");
                return false;
            }

            if (user == null || target == null || card == null)
            {
                Debug.LogError("[Combat] TryPlayCard 인자가 null입니다.");
                return false;
            }

            if (CurrentActor != user)
            {
                Debug.LogWarning($"[Combat] 현재 행동자가 아닙니다: {GetCombatName(user)}");
                return false;
            }

            if (user.IsDead)
                return false;

            if (!user.UnitInfo.CanAffordCard(card))
            {
                NotifyInsufficientCardCost(user, card);
                return false;
            }

            if (target.IsDead)
                return CanSelectDeadUnitAsCardTarget(user, target, card);

            if (!IsValidTarget(user, target, card))
            {
                Debug.LogWarning(
                    $"[Combat] 유효하지 않은 대상: {card.Name} → {GetCombatName(target)}");
                return false;
            }

            UseCard(user, target, card);
            return true;
        }

        public IEnumerator WaitUntilCardResolveComplete()
        {
            while (_isResolvingCard && !_isBattleEnded)
                yield return null;
        }

        /// <summary>
        /// AI가 행동할 카드가 없을 때 전투 종료 여부를 다시 검사합니다.
        /// </summary>
        public void EvaluateBattleEndFromAI()
        {
            CheckBattleEnd();
        }

        /// <summary>
        /// user 기준 카드의 유효 대상 목록을 반환합니다.
        /// </summary>
        public List<CharacterBase> GetValidTargets(CharacterBase user, CardData card)
        {
            var result = new List<CharacterBase>();
            if (user == null || card == null)
                return result;

            CollectValidTargetsFromList(_playerCharacters, user, card, result);
            CollectValidTargetsFromList(_enemyCharacters, user, card, result);
            return result;
        }

        private void CollectValidTargetsFromList(
            IReadOnlyList<CharacterBase> characters,
            CharacterBase user,
            CardData card,
            List<CharacterBase> result)
        {
            if (characters == null)
                return;

            for (int i = 0; i < characters.Count; i++)
            {
                var target = characters[i];
                if (target == null || target.IsDead)
                    continue;

                if (IsValidTarget(user, target, card))
                    result.Add(target);
            }
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
                DeactivateBuffTargetCameraIfNeeded(card);
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
            DeactivateBuffTargetCameraIfNeeded(card);
            _resolveSession = null;
            _isResolvingCard = false;
        }

        private void DeactivateBuffTargetCameraIfNeeded(CardData card)
        {
            if (card != null && card.CardType == CARD_TYPE.BUFF)
                SetBuffTargetCameraActive(false);
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
            else if (card.CardType == CARD_TYPE.BUFF)
            {
                session.BuffTargets = BuildBuffTargets(user, target, card);
            }

            return session;
        }

        private List<CharacterBase> BuildBuffTargets(CharacterBase user, CharacterBase clickedTarget, CardData card)
        {
            var targets = new List<CharacterBase>();

            if (card == null)
                return targets;

            switch (card.BuffTargetType)
            {
                case CARD_BUFF_TARGET_TYPE.SELF:
                case CARD_BUFF_TARGET_TYPE.TEAM:
                    if (clickedTarget != null && clickedTarget.IsAlive)
                        targets.Add(clickedTarget);
                    break;

                case CARD_BUFF_TARGET_TYPE.ALL:
                    {
                        bool userIsPlayer = IsPlayerCharacter(user);
                        var allies = userIsPlayer ? _playerCharacters : _enemyCharacters;
                        for (int i = 0; i < allies.Count; i++)
                        {
                            var ally = allies[i];
                            if (ally != null && ally.IsAlive)
                                targets.Add(ally);
                        }
                    }
                    break;
            }

            return targets;
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
                    ApplyBuffEffect(session);
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
                    HandleAnimBuff(session);
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

            if (applied > 0)
            {
                FireItemEffects(ITEM_EFFECT_TIMING.ON_ATTACK, new ItemEffectContext
                {
                    Owner = session.User,
                    Source = session.User,
                    Target = session.Target,
                    Card = session.Card,
                    Damage = applied,
                });

                FireItemEffects(ITEM_EFFECT_TIMING.ON_DAMAGE, new ItemEffectContext
                {
                    Owner = session.Target,
                    Source = session.User,
                    Target = session.Target,
                    Card = session.Card,
                    Damage = applied,
                });

                FireHealthThresholdItemEffects(session.Target);
            }

            // 마지막 공격 판정에서만 사망 처리 (Die/디졸브)
            if (isLastHit && session.Target.IsDead)
            {
                FireItemEffects(ITEM_EFFECT_TIMING.ON_KILL, new ItemEffectContext
                {
                    Owner = session.User,
                    Source = session.User,
                    Target = session.Target,
                    Card = session.Card,
                    Damage = applied,
                });

                FireItemEffects(ITEM_EFFECT_TIMING.ON_DEATH, new ItemEffectContext
                {
                    Owner = session.Target,
                    Source = session.User,
                    Target = session.Target,
                    Card = session.Card,
                    Damage = applied,
                });

                ProcessDeath(session.Target);
            }
        }

        private void HandleAnimBuff(CardResolveSession session)
        {
            if (session?.Card == null || session.Card.CardType != CARD_TYPE.BUFF)
                return;

            ApplyBuffEffect(session);
        }

        private void ApplyBuffEffect(CardResolveSession session)
        {
            if (session?.Card == null || session.Card.CardType != CARD_TYPE.BUFF)
                return;

            var buffData = session.Card.BuffData;
            if (buffData == null || buffData.BuffEffectType == CARD_BUFF_EFFECT_TYPE.NONE)
            {
                Debug.LogWarning($"[Combat][BUFF] 버프 데이터 없음: {session.Card.Name}");
                return;
            }

            if (session.BuffTargets == null || session.BuffTargets.Count == 0)
            {
                Debug.LogWarning($"[Combat][BUFF] 적용 대상 없음: {session.Card.Name}");
                return;
            }

            for (int i = 0; i < session.BuffTargets.Count; i++)
            {
                var target = session.BuffTargets[i];
                if (target == null || target.IsDead)
                    continue;

                target.ApplyBuffFromCard(session.Card);
            }
        }

        private void FinishCardResolve(CardResolveSession session)
        {
            if (session == null)
                return;

            if (session.User?.UnitInfo != null && session.Card != null)
            {
                if (!session.User.UnitInfo.TrySpendCardCost(session.Card))
                {
                    Debug.LogWarning(
                        $"[Combat] 카드 소모 시점에 코스트 부족: {session.Card.Name} / " +
                        $"현재:{session.User.UnitInfo.CurrentCardCost}");
                }
            }

            ConsumePlayedCard(session.User, session.Card);

            if (session.User != null)
            {
                FireItemEffects(ITEM_EFFECT_TIMING.ON_USE_CARD, new ItemEffectContext
                {
                    Owner = session.User,
                    Source = session.User,
                    Target = session.Target,
                    Card = session.Card,
                });
            }

            // 대상 사망은 마지막 Hit 판정에서 이미 처리됨
            if (session.User != null && session.User.IsDead)
                ProcessDeath(session.User);
        }

        private bool IsValidTarget(CharacterBase user, CharacterBase target, CardData card)
        {
            if (user == null || target == null || card == null || target.IsDead)
                return false;

            bool sameTeam = IsPlayerCharacter(user) == IsPlayerCharacter(target);

            switch (card.CardType)
            {
                case CARD_TYPE.ATTACK:
                case CARD_TYPE.DEBUFF:
                    return !sameTeam;

                case CARD_TYPE.DEFENSE:
                    return sameTeam;

                case CARD_TYPE.BUFF:
                    return IsValidBuffTarget(user, target, card);

                case CARD_TYPE.SPECIAL:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 죽은 유닛을 카드 대상으로 클릭/선택할 수 있는지.
        /// 현재는 모든 카드 불가. 부활 등에서 조건만 확장하면 됩니다.
        /// </summary>
        private bool CanSelectDeadUnitAsCardTarget(CharacterBase user, CharacterBase target, CardData card)
        {
            if (user == null || target == null || card == null || !target.IsDead)
                return false;

            // TODO: 부활 카드 등 죽은 대상 전용 효과 허용
            return false;
        }

        private bool IsValidBuffTarget(CharacterBase user, CharacterBase target, CardData card)
        {
            if (user == null || target == null || card == null || target.IsDead)
                return false;

            bool sameTeam = IsPlayerCharacter(user) == IsPlayerCharacter(target);
            if (!sameTeam)
                return false;

            switch (card.BuffTargetType)
            {
                case CARD_BUFF_TARGET_TYPE.SELF:
                    return target == user;
                case CARD_BUFF_TARGET_TYPE.TEAM:
                case CARD_BUFF_TARGET_TYPE.ALL:
                    return true;
                default:
                    return false;
            }
        }

        public void CancelCardSelection()
        {
            ClearCardSelection();
        }

        private void ClearCardSelection(bool keepBuffCamera = false)
        {
            if (!keepBuffCamera)
                SetBuffTargetCameraActive(false);

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

            bool isPlayerUnit = character.UnitInfo != null &&
                                character.UnitInfo.UnitType == UNIT_TYPE.PLAYER;

            // 플레이어는 디졸브를 쓰지 않으므로, NPC 디졸브 중복만 방지
            if (!isPlayerUnit && (character.IsDissolving || character.HasCompletedDeathVisual))
                return;

            Debug.Log($"[Combat] 사망 처리: {GetCombatName(character)}");

            RemoveFromTurnSystem(character);

            if (isPlayerUnit)
            {
                // 부활 대비: 리스트 유지, 콜라이더/오브젝트 유지
                StartCoroutine(ProcessDeathRoutine(character));
                CheckBattleEnd();
                return;
            }

            _playerCharacters.Remove(character);
            _enemyCharacters.Remove(character);

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

            bool isPlayerUnit = character.UnitInfo != null &&
                                character.UnitInfo.UnitType == UNIT_TYPE.PLAYER;

            if (isPlayerUnit)
            {
                if (!character.TryPlayAnimation(CharacterBase.DeathAnimationName))
                    character.TryPlayAnimation("Death");

                yield return character.WaitCurrentAnimationEnd(CharacterBase.DeathAnimationName, 2.5f);
                yield break;
            }

            if (!character.HasCompletedDeathVisual && !character.IsDissolving)
            {
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
            if (_isBattleEnded)
                return;

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
            {
                Debug.Log("[Combat] 전투 승리");
                EndBattle();
            }
            else if (!anyPlayerAlive)
            {
                Debug.Log("[Combat] 전투 패배");
                EndBattle();
            }
        }

        private void EndBattle()
        {
            if (_isBattleEnded)
                return;

            _isBattleEnded = true;
            _isResolvingCard = false;
            _resolveSession = null;
            _currentTurnEntry = null;

            StopAllAITurns();
            ClearCardSelection();
            PlayerUI?.SetInteractable(false);
            FireItemEffects(ITEM_EFFECT_TIMING.BATTLE_END);
        }

        /// <summary>
        /// 전투 종료 시 모든 캐릭터 AI 루틴을 중단합니다.
        /// </summary>
        private void StopAllAITurns()
        {
            StopAITurnsForList(_playerCharacters);
            StopAITurnsForList(_enemyCharacters);
        }

        private static void StopAITurnsForList(IReadOnlyList<CharacterBase> characters)
        {
            if (characters == null)
                return;

            for (int i = 0; i < characters.Count; i++)
                characters[i]?.StopAITurn();
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
