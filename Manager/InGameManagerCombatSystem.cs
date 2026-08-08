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
        private int _pendingDeathRoutines;
        private CardResolveSession _resolveSession;

        public CardData SelectedCard => _selectedCard;
        public bool IsWaitingForTarget => _isWaitingForTarget;
        public bool IsResolvingCard => _isResolvingCard;
        public bool IsBattleEnded => _isBattleEnded;

        /// <summary>
        /// 현재 카드 해석 중인 유저의 CardData. 파티클/스킬카메라 오버라이드 등에 사용.
        /// </summary>
        public CardData GetResolvingCard(CharacterBase user)
        {
            if (_resolveSession == null || user == null)
                return null;

            if (_resolveSession.User != user)
                return null;

            return _resolveSession.Card;
        }

        /// <summary>
        /// CombatAnimStateBehaviour SkillCameraCue(Play)에서 호출.
        /// address가 비면 현재 카드 SkillCameraPath를 사용한다.
        /// </summary>
        public void OnAnimSkillCameraPlay(CharacterBase source, string address = null)
        {
            if (_resolveSession == null || source == null || source != _resolveSession.User)
                return;

            string cameraAddress = !string.IsNullOrWhiteSpace(address)
                ? address
                : _resolveSession.Card?.SkillCameraPath;

            if (string.IsNullOrWhiteSpace(cameraAddress))
                return;

            GameManager.Instance?.CameraManager?.PlaySkillCamera(cameraAddress, source.transform);
        }

        /// <summary>CombatAnimStateBehaviour SkillCameraCue(Release)에서 호출.</summary>
        public void OnAnimSkillCameraRelease(CharacterBase source)
        {
            if (_resolveSession == null || source == null || source != _resolveSession.User)
                return;

            GameManager.Instance?.CameraManager?.ReleaseSkillCamera();
        }

        private sealed class CardResolveSession
        {
            public CharacterBase User;
            public CharacterBase Target;
            public CardData Card;
            public InGameCardObject PlayedCardObject;
            public int TotalDamage;
            public float[] HitWeights;
            public int[] HitDamages;
            public int NextHitIndex;
            public bool SetupReceived;
            public List<CharacterBase> AttackTargets;
            public Dictionary<CharacterBase, int> AttackTotalDamages;
            public Dictionary<CharacterBase, int[]> AttackHitDamages;
            public bool RangeHitSoundPlayed;
            public HashSet<CardAttackEventData> AttemptedAttackEvents;
            public bool AttackEndItemEffectsFired;
            public int AppliedDamageTotal;
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

            PlayerUI?.ShowInsufficientCost(need, current, max);
        }

        private void BeginTargetSelection(CardData card)
        {
            _isWaitingForTarget = true;

            // BUFF 카드만 팀 카메라. DEBUFF/ATTACK 등은 끈다.
            bool useBuffCamera = card != null &&
                                 CardTypeUtility.ShouldUseAllyTargetCamera(card.CardType);
            SetBuffTargetCameraActive(useBuffCamera);

            Debug.Log($"[Combat] 대상 선택 대기 중... (타입: {card?.CardType}) / 우클릭·Esc 취소");
        }

        private void SetBuffTargetCameraActive(bool active)
        {
            var teamCamera = _playerGroupPosition?.TeamTargrtCamera;
            if (teamCamera == null)
            {
                if (active)
                    Debug.LogWarning("[Combat] Player GroupPosition에 TeamTargetCamera가 없습니다.");
                return;
            }

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
                if (CardTypeUtility.UsesBuffEntries(_selectedCard.CardType))
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
            var playedCardObject = _selectedCardObject;
            bool keepBuffCamera = CardTypeUtility.ShouldUseAllyTargetCamera(card.CardType);
            ClearCardSelection(keepBuffCamera);

            UseCard(user, target, card, playedCardObject);
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
        private void UseCard(
            CharacterBase user,
            CharacterBase target,
            CardData card,
            InGameCardObject playedCardObject = null)
        {
            if (user == null || target == null || card == null)
            {
                Debug.LogError("[Combat] UseCard 인자가 null입니다.");
                return;
            }

            StartCoroutine(UseCardRoutine(user, target, card, playedCardObject));
        }

        private IEnumerator UseCardRoutine(
            CharacterBase user,
            CharacterBase target,
            CardData card,
            InGameCardObject playedCardObject)
        {
            if (_isResolvingCard)
            {
                Debug.LogWarning("[Combat] 이미 카드 연출 중입니다.");
                yield break;
            }

            _isResolvingCard = true;
            _resolveSession = CreateResolveSession(user, target, card, playedCardObject);
            if (card.CardType == CARD_TYPE.ATTACK)
            {
                FireItemEffects(ITEM_EFFECT_TIMING.ON_ATTACK_START, new CombatEventContext
                {
                    Owner = user,
                    Source = user,
                    Target = target,
                    AffectedTargets = _resolveSession.AttackTargets,
                    Card = card,
                    Origin = COMBAT_EVENT_ORIGIN.CARD_ATTACK,
                });

                ExecuteCardAttackEvents(
                    _resolveSession,
                    CARD_ATTACK_EVENT_TIMING.ATTACK_START,
                    _resolveSession.AttackTargets,
                    _resolveSession.Target,
                    0);
            }

            bool teleported = TryBeginAttackTeleport(user, target, card, out Vector3 originPos, out Quaternion originRot);

            bool hasAnim = !string.IsNullOrEmpty(card.AnimationName) &&
                           user.TryPlayCardAnimation(card.AnimationName);

            if (!hasAnim)
            {
                // 애니 없는 카드: 카드 SkillCameraPath가 있으면 즉시 Play → 효과 → Finish에서 Release
                TryPlaySkillCameraForCard(user, card);
                ApplyImmediateCardEffect(_resolveSession);
                FinishCardResolve(_resolveSession);
                DeactivateBuffTargetCameraIfNeeded(card);
                EndAttackTeleport(user, teleported, originPos, originRot);
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
            EndAttackTeleport(user, teleported, originPos, originRot);
            _resolveSession = null;
            _isResolvingCard = false;
        }

        /// <summary>
        /// TeleportToTarget 카드면 대상 forward * margin 앞으로 이동하고 대상을 바라본다.
        /// </summary>
        private static bool TryBeginAttackTeleport(
            CharacterBase user,
            CharacterBase target,
            CardData card,
            out Vector3 originPos,
            out Quaternion originRot)
        {
            originPos = default;
            originRot = default;

            if (user == null || target == null || card == null)
                return false;

            if (card.CardType != CARD_TYPE.ATTACK ||
                card.AttackApproach != CARD_ATTACK_APPROACH.TeleportToTarget)
                return false;

            // 범위 공격은 제자리 유지 (다수 대상 연출과 충돌)
            if (card.IsRangeAttack)
                return false;

            Transform userTf = user.transform;
            Transform targetTf = target.transform;
            originPos = userTf.position;
            originRot = userTf.rotation;

            float margin = Mathf.Max(0f, card.TeleportMargin);
            Vector3 forward = targetTf.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            Vector3 engagePos = targetTf.position + forward * margin;
            engagePos.y = originPos.y;
            userTf.position = engagePos;

            Vector3 lookDir = targetTf.position - userTf.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 1e-6f)
                userTf.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

            return true;
        }

        private static void EndAttackTeleport(
            CharacterBase user,
            bool teleported,
            Vector3 originPos,
            Quaternion originRot)
        {
            if (!teleported || user == null)
                return;

            user.transform.SetPositionAndRotation(originPos, originRot);
        }

        private void DeactivateBuffTargetCameraIfNeeded(CardData card)
        {
            if (card != null && CardTypeUtility.ShouldUseAllyTargetCamera(card.CardType))
                SetBuffTargetCameraActive(false);
        }

        private CardResolveSession CreateResolveSession(
            CharacterBase user,
            CharacterBase target,
            CardData card,
            InGameCardObject playedCardObject)
        {
            var session = new CardResolveSession
            {
                User = user,
                Target = target,
                Card = card,
                PlayedCardObject = playedCardObject,
                AttemptedAttackEvents = new HashSet<CardAttackEventData>(),
            };

            if (card.CardType == CARD_TYPE.ATTACK)
            {
                session.AttackTargets = BuildAttackTargets(user, target, card);
                session.AttackTotalDamages = new Dictionary<CharacterBase, int>();

                for (int i = 0; i < session.AttackTargets.Count; i++)
                {
                    var attackTarget = session.AttackTargets[i];
                    int damage = CalculateDamage(user, attackTarget, card);
                    session.AttackTotalDamages[attackTarget] = damage;
                }

                if (!session.AttackTotalDamages.TryGetValue(target, out session.TotalDamage) &&
                    session.AttackTargets.Count > 0)
                {
                    session.TotalDamage = session.AttackTotalDamages[session.AttackTargets[0]];
                }
            }
            else if (CardTypeUtility.UsesBuffEntries(card.CardType))
            {
                session.BuffTargets = BuildBuffTargets(user, target, card);
            }

            return session;
        }

        private List<CharacterBase> BuildAttackTargets(
            CharacterBase user,
            CharacterBase clickedTarget,
            CardData card)
        {
            var targets = new List<CharacterBase>();
            if (user == null || card == null || card.CardType != CARD_TYPE.ATTACK)
                return targets;

            if (!card.IsRangeAttack)
            {
                if (clickedTarget != null && clickedTarget.IsAlive)
                    targets.Add(clickedTarget);
                return targets;
            }

            var enemies = IsPlayerCharacter(user) ? _enemyCharacters : _playerCharacters;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy != null && enemy.IsAlive && IsValidTarget(user, enemy, card))
                    targets.Add(enemy);
            }

            return targets;
        }

        private List<CharacterBase> BuildBuffTargets(CharacterBase user, CharacterBase clickedTarget, CardData card)
        {
            var targets = new List<CharacterBase>();
            if (card?.BuffEntries == null)
                return targets;

            for (int i = 0; i < card.BuffEntries.Count; i++)
            {
                var entry = card.BuffEntries[i];
                if (entry == null || !entry.IsValid)
                    continue;

                AddBuffTargetsByType(targets, user, clickedTarget, entry.TargetType);
            }

            return targets;
        }

        private void AddBuffTargetsByType(
            List<CharacterBase> targets,
            CharacterBase user,
            CharacterBase clickedTarget,
            CARD_BUFF_TARGET_TYPE targetType)
        {
            switch (targetType)
            {
                case CARD_BUFF_TARGET_TYPE.SELF:
                    if (user != null && user.IsAlive && !targets.Contains(user))
                        targets.Add(user);
                    break;

                case CARD_BUFF_TARGET_TYPE.TEAM:
                    if (clickedTarget != null &&
                        clickedTarget.IsAlive &&
                        IsPlayerCharacter(user) == IsPlayerCharacter(clickedTarget) &&
                        !targets.Contains(clickedTarget))
                    {
                        targets.Add(clickedTarget);
                    }
                    break;

                case CARD_BUFF_TARGET_TYPE.ALL:
                    {
                        bool userIsPlayer = IsPlayerCharacter(user);
                        var allies = userIsPlayer ? _playerCharacters : _enemyCharacters;
                        for (int i = 0; i < allies.Count; i++)
                        {
                            var ally = allies[i];
                            if (ally != null && ally.IsAlive && !targets.Contains(ally))
                                targets.Add(ally);
                        }
                    }
                    break;

                case CARD_BUFF_TARGET_TYPE.ENEMY:
                    if (clickedTarget != null &&
                        clickedTarget.IsAlive &&
                        IsPlayerCharacter(user) != IsPlayerCharacter(clickedTarget) &&
                        !targets.Contains(clickedTarget))
                    {
                        targets.Add(clickedTarget);
                    }
                    break;

                case CARD_BUFF_TARGET_TYPE.ENEMY_ALL:
                    {
                        bool userIsPlayer = IsPlayerCharacter(user);
                        var enemies = userIsPlayer ? _enemyCharacters : _playerCharacters;
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            var enemy = enemies[i];
                            if (enemy != null && enemy.IsAlive && !targets.Contains(enemy))
                                targets.Add(enemy);
                        }
                    }
                    break;
            }
        }

        private void ApplyImmediateCardEffect(CardResolveSession session)
        {
            if (session == null)
                return;

            switch (CardTypeUtility.Normalize(session.Card.CardType))
            {
                case CARD_TYPE.ATTACK:
                    ApplyAttackHitDamage(session, session.TotalDamage, CameraShakeLevel.None, isLastHit: true);
                    break;
                case CARD_TYPE.DEFENSE:
                    Debug.Log($"[Combat][DEFENSE] {GetCombatName(session.User)} → {GetCombatName(session.Target)} / {session.Card.Name} (구현 예정)");
                    break;
                case CARD_TYPE.BUFF:
                case CARD_TYPE.DEBUFF:
                    ApplyBuffEffect(session);
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
            BuildAttackHitDamages(_resolveSession, weights);
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
                case CombatJudgmentType.Debuff:
                    HandleAnimBuff(session);
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
                BuildAttackHitDamages(session, session.HitWeights);
                session.NextHitIndex = 0;
                session.SetupReceived = true;
                Debug.LogWarning("[Combat] HitWeights Setup 없이 Hit → 전체 데미지 1회 적용");
            }

            if (session.NextHitIndex >= session.HitDamages.Length)
            {
                Debug.LogWarning("[Combat] Setup된 Hit 횟수를 초과했습니다.");
                return;
            }

            int hitIndex = session.NextHitIndex;
            int portion = session.HitDamages[hitIndex];
            session.NextHitIndex++;
            bool isLastHit = session.NextHitIndex >= session.HitDamages.Length;
            ApplyAttackHitDamage(session, portion, cameraShake, isLastHit, hitIndex);
        }

        private void BuildAttackHitDamages(CardResolveSession session, float[] weights)
        {
            if (session?.AttackTotalDamages == null)
                return;

            session.AttackHitDamages = new Dictionary<CharacterBase, int[]>();
            foreach (var pair in session.AttackTotalDamages)
            {
                session.AttackHitDamages[pair.Key] =
                    CombatDamageSplit.SplitByWeights(pair.Value, weights);
            }
        }

        private void ApplyAttackHitDamage(
            CardResolveSession session,
            int damage,
            CameraShakeLevel cameraShake = CameraShakeLevel.None,
            bool isLastHit = false,
            int hitIndex = -1)
        {
            if (session?.Target == null || damage < 0 || session.Card == null)
                return;

            var targets = session.AttackTargets;
            bool hasAttackTargets = targets != null && targets.Count > 0;
            int targetCount = hasAttackTargets ? targets.Count : 1;

            string resolveEffectPath = string.IsNullOrEmpty(session.Card.ResolveEffectPath)
                ? PublicVariable.Address.DefaultHitEffectPrefab
                : session.Card.ResolveEffectPath;
            string resolveSoundPath = string.IsNullOrEmpty(session.Card.ResolveSoundPath)
                ? PublicVariable.Address.DefaultHitSe
                : session.Card.ResolveSoundPath;

            bool shouldPlayResolveSound =
                !session.Card.IsRangeAttack || !session.RangeHitSoundPlayed;
            if (shouldPlayResolveSound && !string.IsNullOrEmpty(resolveSoundPath))
            {
                GameManager.Instance?.SoundManager?.PlaySe(resolveSoundPath);
                if (session.Card.IsRangeAttack)
                    session.RangeHitSoundPlayed = true;
            }

            bool foundTarget = false;
            int appliedThisHit = 0;
            var hitTargets = new List<CharacterBase>();
            var killedTargets = new List<CharacterBase>();
            for (int i = 0; i < targetCount; i++)
            {
                var target = hasAttackTargets ? targets[i] : session.Target;
                if (target == null)
                    continue;

                foundTarget = true;
                hitTargets.Add(target);
                target.PlayHitAnimation();
                target.SpawnHitEffect(resolveEffectPath);

                int targetDamage = GetAttackHitDamage(session, target, hitIndex, damage);
                int applied = target.TakeDamage(targetDamage, session.User);
                appliedThisHit += applied;
                session.AppliedDamageTotal += applied;
                Debug.Log(
                    $"[Combat][HIT] {GetCombatName(session.User)} → {GetCombatName(target)} / {session.Card.Name} / " +
                    $"히트데미지:{applied} / 남은HP:{target.UnitInfo.CurrentHp}");

                if (applied > 0)
                {
                    FireItemEffects(ITEM_EFFECT_TIMING.ON_TARGET_HIT, new CombatEventContext
                    {
                        Owner = session.User,
                        Source = session.User,
                        Target = target,
                        AffectedTargets = session.AttackTargets,
                        Card = session.Card,
                        Damage = applied,
                        Origin = COMBAT_EVENT_ORIGIN.CARD_ATTACK,
                    });

                    FireItemEffects(ITEM_EFFECT_TIMING.ON_DAMAGE, new CombatEventContext
                    {
                        Owner = target,
                        Source = session.User,
                        Target = target,
                        AffectedTargets = session.AttackTargets,
                        Card = session.Card,
                        Damage = applied,
                        Origin = COMBAT_EVENT_ORIGIN.CARD_ATTACK,
                    });

                    FireHealthThresholdItemEffects(target);
                }

                // 마지막 공격 판정에서만 사망 처리 (Die/디졸브)
                if (isLastHit && target.IsDead)
                {
                    killedTargets.Add(target);
                    FireItemEffects(ITEM_EFFECT_TIMING.ON_KILL, new CombatEventContext
                    {
                        Owner = session.User,
                        Source = session.User,
                        Target = target,
                        Card = session.Card,
                        Damage = applied,
                    });

                    FireItemEffects(ITEM_EFFECT_TIMING.ON_DEATH, new CombatEventContext
                    {
                        Owner = target,
                        Source = session.User,
                        Target = target,
                        Card = session.Card,
                        Damage = applied,
                    });

                    ProcessDeath(target);
                }
            }

            if (foundTarget)
            {
                FireItemEffects(ITEM_EFFECT_TIMING.ON_HIT, new CombatEventContext
                {
                    Owner = session.User,
                    Source = session.User,
                    Target = session.Target,
                    AffectedTargets = hitTargets,
                    Card = session.Card,
                    Damage = appliedThisHit,
                    Origin = COMBAT_EVENT_ORIGIN.CARD_ATTACK,
                });

                ExecuteCardAttackEvents(
                    session,
                    CARD_ATTACK_EVENT_TIMING.EACH_HIT,
                    hitTargets,
                    session.Target,
                    appliedThisHit);
            }

            if (isLastHit && killedTargets.Count > 0)
            {
                ExecuteCardAttackEvents(
                    session,
                    CARD_ATTACK_EVENT_TIMING.ON_KILL,
                    killedTargets,
                    killedTargets[0],
                    appliedThisHit);
            }

            if (foundTarget && cameraShake != CameraShakeLevel.None)
            {
                GameManager.Instance?.CameraManager?.Shake(cameraShake);
                // 범위 공격도 판정 1회당 쉐이크/히트스톱은 한 번만 적용
                GameManager.Instance?.TimeManager?.HitStop();
            }

            if (isLastHit)
            {
                ExecuteCardAttackEvents(
                    session,
                    CARD_ATTACK_EVENT_TIMING.FINAL_HIT,
                    session.AttackTargets,
                    session.Target,
                    session.AppliedDamageTotal);
                FireAttackEndItemEffects(session);
            }
        }

        private void FireAttackEndItemEffects(CardResolveSession session)
        {
            if (session?.Card == null ||
                session.Card.CardType != CARD_TYPE.ATTACK ||
                session.AttackEndItemEffectsFired)
            {
                return;
            }

            session.AttackEndItemEffectsFired = true;
            FireItemEffects(ITEM_EFFECT_TIMING.ON_ATTACK_END, new CombatEventContext
            {
                Owner = session.User,
                Source = session.User,
                Target = session.Target,
                AffectedTargets = session.AttackTargets,
                Card = session.Card,
                Damage = session.AppliedDamageTotal,
                Origin = COMBAT_EVENT_ORIGIN.CARD_ATTACK,
            });
        }

        private int GetAttackHitDamage(
            CardResolveSession session,
            CharacterBase target,
            int hitIndex,
            int fallbackDamage)
        {
            if (session?.AttackHitDamages != null &&
                hitIndex >= 0 &&
                session.AttackHitDamages.TryGetValue(target, out var hitDamages) &&
                hitIndex < hitDamages.Length)
            {
                return hitDamages[hitIndex];
            }

            if (session?.AttackTotalDamages != null &&
                session.AttackTotalDamages.TryGetValue(target, out int totalDamage))
            {
                return totalDamage;
            }

            return fallbackDamage;
        }

        private void ExecuteCardAttackEvents(
            CardResolveSession session,
            CARD_ATTACK_EVENT_TIMING timing,
            IReadOnlyList<CharacterBase> affectedTargets,
            CharacterBase primaryTarget,
            int damage)
        {
            if (session?.Card == null || session.Card.CardType != CARD_TYPE.ATTACK)
                return;

            var attackEvents = session.Card.AttackEvents;
            if (attackEvents == null || attackEvents.Count == 0)
                return;

            session.AttemptedAttackEvents ??= new HashSet<CardAttackEventData>();
            for (int i = 0; i < attackEvents.Count; i++)
            {
                var eventData = attackEvents[i];
                if (eventData == null ||
                    eventData.Timing != timing ||
                    string.IsNullOrEmpty(eventData.EventTid))
                {
                    continue;
                }

                if (!eventData.AllowRepeatedExecution)
                {
                    if (!session.AttemptedAttackEvents.Add(eventData))
                        continue;
                }

                if (timing == CARD_ATTACK_EVENT_TIMING.ON_KILL &&
                    eventData.AllowRepeatedExecution &&
                    affectedTargets != null)
                {
                    for (int targetIndex = 0; targetIndex < affectedTargets.Count; targetIndex++)
                    {
                        var killedTarget = affectedTargets[targetIndex];
                        if (killedTarget == null)
                            continue;

                        TryExecuteCardAttackEvent(
                            session,
                            eventData,
                            timing,
                            new[] { killedTarget },
                            killedTarget,
                            damage);
                    }
                    continue;
                }

                TryExecuteCardAttackEvent(
                    session,
                    eventData,
                    timing,
                    affectedTargets,
                    primaryTarget,
                    damage);
            }
        }

        private void TryExecuteCardAttackEvent(
            CardResolveSession session,
            CardAttackEventData eventData,
            CARD_ATTACK_EVENT_TIMING timing,
            IReadOnlyList<CharacterBase> affectedTargets,
            CharacterBase primaryTarget,
            int damage)
        {
            float triggerChance = Mathf.Clamp01(eventData.TriggerChance);
            if (triggerChance <= 0f ||
                (triggerChance < 1f && Random.value > triggerChance))
            {
                return;
            }

            var context = new CombatEventContext
            {
                Owner = session.User,
                Source = session.User,
                Target = primaryTarget ?? session.Target,
                AffectedTargets = affectedTargets,
                Card = session.Card,
                Damage = damage,
                Origin = COMBAT_EVENT_ORIGIN.CARD_ATTACK,
            };

            ExecuteCardAttackEvent(
                eventData,
                context,
                $"CardAttack:{session.Card.Tid}/{timing}");
        }

        private void HandleAnimBuff(CardResolveSession session)
        {
            if (session?.Card == null || !CardTypeUtility.UsesBuffEntries(session.Card.CardType))
                return;

            ApplyBuffEffect(session);
        }

        private void ApplyBuffEffect(CardResolveSession session)
        {
            if (session?.Card == null || !CardTypeUtility.UsesBuffEntries(session.Card.CardType))
                return;

            var buffEntries = session.Card.BuffEntries;
            if (buffEntries == null || buffEntries.Count == 0)
            {
                Debug.LogWarning($"[Combat][BUFF] 버프 목록 없음: {session.Card.Name}");
                return;
            }

            bool appliedAny = false;
            bool resolveSoundPlayed = false;
            var presentedTargets = new HashSet<CharacterBase>();

            for (int i = 0; i < buffEntries.Count; i++)
            {
                var entry = buffEntries[i];
                if (entry == null || !entry.IsValid)
                    continue;

                if (!TryGetBuffData(entry.BuffTid, out var buffData) ||
                    buffData == null ||
                    !buffData.IsValid)
                {
                    Debug.LogWarning(
                        $"[Combat][BUFF] BuffData 없음: card={session.Card.Tid} / buff={entry.BuffTid}");
                    continue;
                }

                var targets = new List<CharacterBase>();
                AddBuffTargetsByType(
                    targets,
                    session.User,
                    session.Target,
                    entry.TargetType);

                if (targets.Count == 0)
                {
                    Debug.LogWarning(
                        $"[Combat][BUFF] 적용 대상 없음: {session.Card.Name} / {entry.TargetType}");
                    continue;
                }

                if (!resolveSoundPlayed)
                {
                    PlayCardResolveSound(session.Card, useAttackDefault: false);
                    resolveSoundPlayed = true;
                }

                for (int t = 0; t < targets.Count; t++)
                {
                    var target = targets[t];
                    if (target == null || target.IsDead)
                        continue;

                    if (presentedTargets.Add(target))
                        PlayCardResolveEffect(target, session.Card, useAttackDefault: false);

                    target.ApplyBuffData(buffData, session.Card.Tid);
                    appliedAny = true;
                }
            }

            if (!appliedAny)
                Debug.LogWarning($"[Combat][BUFF] 적용된 버프 없음: {session.Card.Name}");
        }

        private void PlayCardResolveEffect(
            CharacterBase target,
            CardData card,
            bool useAttackDefault)
        {
            if (target == null || card == null)
                return;

            string effectPath = card.ResolveEffectPath;
            if (string.IsNullOrEmpty(effectPath))
            {
                if (!useAttackDefault)
                    return;

                effectPath = PublicVariable.Address.DefaultHitEffectPrefab;
            }

            target.SpawnHitEffect(effectPath);
        }

        private void PlayCardResolveSound(CardData card, bool useAttackDefault)
        {
            if (card == null)
                return;

            string soundPath = card.ResolveSoundPath;
            if (string.IsNullOrEmpty(soundPath))
            {
                if (!useAttackDefault)
                    return;

                soundPath = PublicVariable.Address.DefaultHitSe;
            }

            if (!string.IsNullOrEmpty(soundPath))
                GameManager.Instance?.SoundManager?.PlaySe(soundPath);
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

            ConsumePlayedCard(session.User, session.Card, session.PlayedCardObject);

            if (IsPlayerCharacter(session.User))
                PlayerUI?.RefreshCostUI();

            if (session.Card != null && session.Card.CardType == CARD_TYPE.ATTACK)
                FireAttackEndItemEffects(session);

            if (session.User != null)
            {
                FireItemEffects(ITEM_EFFECT_TIMING.ON_USE_CARD, new CombatEventContext
                {
                    Owner = session.User,
                    Source = session.User,
                    Target = session.Target,
                    AffectedTargets = session.AttackTargets,
                    Card = session.Card,
                    Damage = session.AppliedDamageTotal,
                });
            }

            // 애니 Release 누락 대비 안전 해제
            GameManager.Instance?.CameraManager?.ReleaseSkillCamera();

            // 대상 사망은 마지막 Hit 판정에서 이미 처리됨
            if (session.User != null && session.User.IsDead)
                ProcessDeath(session.User);
        }

        private static void TryPlaySkillCameraForCard(CharacterBase user, CardData card)
        {
            if (user == null || card == null || string.IsNullOrWhiteSpace(card.SkillCameraPath))
                return;

            GameManager.Instance?.CameraManager?.PlaySkillCamera(card.SkillCameraPath, user.transform);
        }

        private bool IsValidTarget(CharacterBase user, CharacterBase target, CardData card)
        {
            if (user == null || target == null || card == null || target.IsDead)
                return false;

            bool sameTeam = IsPlayerCharacter(user) == IsPlayerCharacter(target);

            switch (card.CardType)
            {
                case CARD_TYPE.ATTACK:
                    return !sameTeam;

                case CARD_TYPE.DEFENSE:
                    return sameTeam;

                case CARD_TYPE.BUFF:
                case CARD_TYPE.DEBUFF:
                    return IsValidBuffOrDebuffTarget(user, target, card);

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

        private bool IsValidBuffOrDebuffTarget(CharacterBase user, CharacterBase target, CardData card)
        {
            if (user == null || target == null || card == null || target.IsDead)
                return false;

            bool sameTeam = IsPlayerCharacter(user) == IsPlayerCharacter(target);

            // CardType으로 기본 진영을 제한 (버프=아군, 디버프=적)
            if (card.CardType == CARD_TYPE.BUFF && !sameTeam)
                return false;
            if (card.CardType == CARD_TYPE.DEBUFF && sameTeam)
                return false;

            if (!card.NeedsBuffTargetSelection)
            {
                if (card.CardType == CARD_TYPE.BUFF)
                    return target == user;
                // 디버프 + 엔트리 없으면 적 클릭만으로 허용
                return !sameTeam;
            }

            var buffEntries = card.BuffEntries;
            if (buffEntries == null || buffEntries.Count == 0)
                return false;

            for (int i = 0; i < buffEntries.Count; i++)
            {
                var entry = buffEntries[i];
                if (entry == null)
                    continue;

                switch (entry.TargetType)
                {
                    case CARD_BUFF_TARGET_TYPE.SELF:
                        if (card.CardType == CARD_TYPE.BUFF && target == user)
                            return true;
                        break;

                    case CARD_BUFF_TARGET_TYPE.TEAM:
                    case CARD_BUFF_TARGET_TYPE.ALL:
                        if (card.CardType == CARD_TYPE.BUFF && sameTeam)
                            return true;
                        break;

                    case CARD_BUFF_TARGET_TYPE.ENEMY:
                    case CARD_BUFF_TARGET_TYPE.ENEMY_ALL:
                        if (card.CardType == CARD_TYPE.DEBUFF && !sameTeam)
                            return true;
                        break;
                }
            }

            return false;
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
            if (attacker?.UnitInfo == null || defender?.UnitInfo == null)
                return 0;

            float multiplier = card != null && card.AttackMultiplier > 0f
                ? card.AttackMultiplier
                : 1f;

            // 기본 공격력(ATTACK_UP + STRENGTH 포함) × 카드 배율
            float raw = attacker.UnitInfo.CurrentAttack * multiplier;

            // WEAK: 주는 피해 % 감소 (value=25 → 25%)
            float weakPercent = attacker.UnitInfo.GetBuffValueSum(BUFF_EFFECT_TYPE.WEAK);
            if (weakPercent > 0f)
                raw *= Mathf.Max(0f, 1f - weakPercent / 100f);

            int damage = Mathf.FloorToInt(raw) - defender.UnitInfo.CurrentDefense;
            damage = Mathf.Max(0, damage);

            // VULNERABLE: 받는 피해 % 증가 (value=50 → 50%)
            float vulnerablePercent = defender.UnitInfo.GetBuffValueSum(BUFF_EFFECT_TYPE.VULNERABLE);
            if (vulnerablePercent > 0f && damage > 0)
                damage = Mathf.FloorToInt(damage * (1f + vulnerablePercent / 100f));

            return Mathf.Max(0, damage);
        }

        private void ConsumePlayedCard(
            CharacterBase attacker,
            CardData card,
            InGameCardObject playedCardObject)
        {
            if (attacker?.UnitInfo == null || card == null)
                return;

            if (!attacker.UnitInfo.DiscardFromHand(card))
            {
                Debug.LogWarning($"[Combat] 손패에서 카드를 제거하지 못했습니다: {card.Tid}");
                return;
            }

            if (!IsPlayerCharacter(attacker) || PlayerUI == null)
                return;

            // 전체 리프레시 대신 사용한 카드만 제거하고 나머지가 빈자리를 채움
            if (playedCardObject != null)
                PlayerUI.RemoveCardFromHand(playedCardObject);
            else
                PlayerUI.RemoveCardFromHand(card);
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
                // 전투 종료(StageRewardUI)는 사망 연출 종료 후에만 판정
                StartCoroutine(ProcessDeathRoutineAndCheckBattleEnd(character));
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

            // 사망 판정(HP/턴 제거)은 즉시, 전투 종료 UI는 디졸브 종료 후
            StartCoroutine(ProcessDeathRoutineAndCheckBattleEnd(character));
        }

        private IEnumerator ProcessDeathRoutineAndCheckBattleEnd(CharacterBase character)
        {
            _pendingDeathRoutines++;
            yield return ProcessDeathRoutine(character);
            _pendingDeathRoutines = Mathf.Max(0, _pendingDeathRoutines - 1);

            if (_pendingDeathRoutines <= 0)
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

            // 진행 중인 사망 연출이 있으면 StageRewardUI 등 종료 플로우를 미룬다.
            if (_pendingDeathRoutines > 0)
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
                EndBattle(isVictory: true);
            }
            else if (!anyPlayerAlive)
            {
                Debug.Log("[Combat] 전투 패배");
                EndBattle(isVictory: false);
            }
        }

        private void EndBattle(bool isVictory)
        {
            if (_isBattleEnded)
                return;

            _isBattleEnded = true;
            _isResolvingCard = false;
            _resolveSession = null;
            _currentTurnEntry = null;

            StopAllAITurns();
            ClearCardSelection();
            SetPlayerUIVisible(false);
            PlayerUI?.SetInteractable(false);
            FireItemEffects(ITEM_EFFECT_TIMING.BATTLE_END);
            SyncPlayerCombatHpToRunData();

            GameManager.Instance?.StageManager?.OnBattleFinished(isVictory);
        }

        /// <summary>
        /// 전투용 UnitInfo 체력을 GameManager 런 영속 UnitInfo에 반영한다.
        /// </summary>
        private void SyncPlayerCombatHpToRunData()
        {
            for (int i = 0; i < _playerCharacters.Count; i++)
            {
                CharacterBase character = _playerCharacters[i];
                if (character?.UnitInfo == null)
                    continue;

                character.UnitInfo.SyncHpToRunSource();
            }
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
