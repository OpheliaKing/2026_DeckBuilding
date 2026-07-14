using UnityEngine;
using UnityEngine.EventSystems;

namespace SHIN
{
    public partial class InGameManager
    {
        private InGameCardObject _selectedCardObject;
        private CardData _selectedCard;
        private bool _isWaitingForTarget;

        public CardData SelectedCard => _selectedCard;
        public bool IsWaitingForTarget => _isWaitingForTarget;

        private void Update()
        {
            if (!_isWaitingForTarget)
                return;

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelCardSelection();
                Debug.Log("[Combat] 카드 선택이 취소되었습니다.");
                return;
            }

            if (!Input.GetMouseButtonDown(0))
                return;

            // UI 위 클릭은 대상 선택으로 처리하지 않음 (카드 UI 등과 구분)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var target = RaycastCharacterFromCamera();
            if (target != null)
                OnCombatTargetSelected(target);
        }

        /// <summary>
        /// 손패 카드 클릭. 카드 선택 후 대상 선택 대기 상태로 전환합니다.
        /// </summary>
        public void OnCardClicked(InGameCardObject cardObject)
        {
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

            // 같은 카드를 다시 클릭하면 선택 취소
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

        /// <summary>
        /// 카메라 레이캐스트로 CharacterBase를 찾습니다.
        /// </summary>
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

        /// <summary>
        /// 대상 선택 완료 후 카드 타입에 맞게 사용합니다.
        /// </summary>
        public void OnCombatTargetSelected(CharacterBase target)
        {
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
        /// CARD_TYPE에 따라 카드 효과를 분기합니다.
        /// </summary>
        private void UseCard(CharacterBase user, CharacterBase target, CardData card)
        {
            switch (card.CardType)
            {
                case CARD_TYPE.ATTACK:
                    ProcessAttack(user, target, card);
                    break;

                case CARD_TYPE.DEFENSE:
                    ProcessDefense(user, target, card);
                    break;

                case CARD_TYPE.BUFF:
                    ProcessBuff(user, target, card);
                    break;

                case CARD_TYPE.DEBUFF:
                    ProcessDebuff(user, target, card);
                    break;

                case CARD_TYPE.SPECIAL:
                    ProcessSpecial(user, target, card);
                    break;

                default:
                    Debug.LogWarning($"[Combat] 지원하지 않는 카드 타입: {card.CardType}");
                    break;
            }
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

        /// <summary>
        /// 공격 처리: 추가효과 → 데미지 적용 → 카드 소모 → 사망 처리
        /// </summary>
        public void ProcessAttack(CharacterBase attacker, CharacterBase defender, CardData card)
        {
            if (attacker == null || defender == null || card == null)
            {
                Debug.LogError("[Combat] ProcessAttack 인자가 null입니다.");
                return;
            }

            if (attacker.UnitInfo == null || defender.UnitInfo == null)
            {
                Debug.LogError("[Combat] UnitInfo가 없습니다.");
                return;
            }

            if (attacker.IsDead || defender.IsDead)
            {
                Debug.LogWarning("[Combat] 사망한 유닛은 공격/피격할 수 없습니다.");
                return;
            }

            int damage = CalculateDamage(attacker, defender, card);
            damage = ApplyAttackExtraEffects(attacker, defender, card, damage);

            int applied = defender.TakeDamage(damage);
            Debug.Log(
                $"[Combat][ATTACK] {GetCombatName(attacker)} → {GetCombatName(defender)} / {card.Name} / 데미지:{applied} / 남은HP:{defender.UnitInfo.CurrentHp}");

            ConsumePlayedCard(attacker, card);

            if (defender.IsDead)
                ProcessDeath(defender);

            if (attacker.IsDead)
                ProcessDeath(attacker);
        }

        private void ProcessDefense(CharacterBase user, CharacterBase target, CardData card)
        {
            Debug.Log($"[Combat][DEFENSE] {GetCombatName(user)} → {GetCombatName(target)} / {card.Name} (구현 예정)");
            // TODO: 방어력 증가 등
            ConsumePlayedCard(user, card);
        }

        private void ProcessBuff(CharacterBase user, CharacterBase target, CardData card)
        {
            Debug.Log($"[Combat][BUFF] {GetCombatName(user)} → {GetCombatName(target)} / {card.Name} (구현 예정)");
            // TODO: 버프 적용
            ConsumePlayedCard(user, card);
        }

        private void ProcessDebuff(CharacterBase user, CharacterBase target, CardData card)
        {
            Debug.Log($"[Combat][DEBUFF] {GetCombatName(user)} → {GetCombatName(target)} / {card.Name} (구현 예정)");
            // TODO: 디버프 적용
            ConsumePlayedCard(user, card);
        }

        private void ProcessSpecial(CharacterBase user, CharacterBase target, CardData card)
        {
            Debug.Log($"[Combat][SPECIAL] {GetCombatName(user)} → {GetCombatName(target)} / {card.Name} (구현 예정)");
            // TODO: 특수 효과
            ConsumePlayedCard(user, card);
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

            Debug.Log($"[Combat] 사망 처리: {GetCombatName(character)}");

            RemoveFromTurnSystem(character);
            _playerCharacters.Remove(character);
            _enemyCharacters.Remove(character);

            character.gameObject.SetActive(false);
            CheckBattleEnd();
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
