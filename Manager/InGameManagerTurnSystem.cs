using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class InGameManager
    {
        /// <summary>AV = BaseActionValue / Speed (스타레일식 행동값)</summary>
        private const float BaseActionValue = 10000f;

        private readonly List<TurnEntry> _turnEntries = new();
        private readonly List<TurnEntry> _turnOrderPreview = new();

        private TurnEntry _currentTurnEntry;

        public IReadOnlyList<TurnEntry> TurnOrderPreview => _turnOrderPreview;
        public TurnEntry CurrentTurnEntry => _currentTurnEntry;
        public CharacterBase CurrentActor => _currentTurnEntry?.Character;

        /// <summary>
        /// 전투 시작 시 효과 발동 타이밍
        /// </summary>
        private void BattleStartTiming()
        {
        }

        /// <summary>
        /// 플레이어/적 캐릭터로 턴 엔트리를 구성하고 초기 AV를 세팅합니다.
        /// </summary>
        private void InitTurnSystem()
        {
            _turnEntries.Clear();
            _turnOrderPreview.Clear();
            _currentTurnEntry = null;

            AddTurnEntries(_playerCharacters);
            AddTurnEntries(_enemyCharacters);

            for (int i = 0; i < _turnEntries.Count; i++)
                ResetActionValue(_turnEntries[i]);

            RefreshTurnOrderPreview();
            Debug.Log($"[TurnSystem] 초기화 완료: {_turnEntries.Count}명");
        }

        private void AddTurnEntries(IReadOnlyList<CharacterBase> characters)
        {
            if (characters == null)
                return;

            for (int i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character == null || character.UnitInfo == null)
                    continue;

                _turnEntries.Add(new TurnEntry(character));
            }
        }

        /// <summary>
        /// 다음 행동 캐릭터로 시간을 진행하고, 해당 캐릭터를 현재 턴으로 설정합니다.
        /// </summary>
        public CharacterBase AdvanceToNextTurn()
        {
            RemoveInvalidEntries();

            if (_turnEntries.Count == 0)
            {
                _currentTurnEntry = null;
                Debug.LogWarning("[TurnSystem] 행동 가능한 캐릭터가 없습니다.");
                return null;
            }

            var next = GetLowestAvEntry();
            if (next == null)
                return null;

            float elapsed = next.RemainingAV;
            for (int i = 0; i < _turnEntries.Count; i++)
                _turnEntries[i].RemainingAV = Mathf.Max(0f, _turnEntries[i].RemainingAV - elapsed);

            _currentTurnEntry = next;
            RefreshTurnOrderPreview();

            Debug.Log(
                $"[TurnSystem] 현재 턴: {GetCharacterName(next.Character)} (SPD {GetSpeed(next.Character)})");

            return next.Character;
        }

        /// <summary>
        /// 현재 캐릭터 행동 종료 후 AV를 재설정합니다.
        /// </summary>
        public void OnActionFinished()
        {
            if (_currentTurnEntry == null)
                return;

            if (_turnEntries.Contains(_currentTurnEntry))
                ResetActionValue(_currentTurnEntry);

            _currentTurnEntry = null;
            RefreshTurnOrderPreview();
        }

        /// <summary>
        /// 행동 전진 (남은 AV 감소). percent는 0~1 (예: 0.25 = 25% 전진).
        /// </summary>
        public void ApplyActionAdvance(CharacterBase character, float percent)
        {
            var entry = FindEntry(character);
            if (entry == null)
                return;

            percent = Mathf.Clamp01(percent);
            entry.RemainingAV *= (1f - percent);
            RefreshTurnOrderPreview();
        }

        /// <summary>
        /// 행동 지연 (남은 AV 증가). percent는 0~1 (예: 0.25 = 25% 지연).
        /// </summary>
        public void ApplyActionDelay(CharacterBase character, float percent)
        {
            var entry = FindEntry(character);
            if (entry == null)
                return;

            percent = Mathf.Clamp01(percent);
            entry.RemainingAV *= (1f + percent);
            RefreshTurnOrderPreview();
        }

        /// <summary>
        /// 남은 AV를 고정값만큼 변경합니다. 음수면 전진, 양수면 지연.
        /// </summary>
        public void ModifyRemainingAV(CharacterBase character, float deltaAV)
        {
            var entry = FindEntry(character);
            if (entry == null)
                return;

            entry.RemainingAV = Mathf.Max(0f, entry.RemainingAV + deltaAV);
            RefreshTurnOrderPreview();
        }

        /// <summary>
        /// 속도 변경 후, 변경 비율에 맞춰 남은 AV를 보정합니다.
        /// </summary>
        public void RecalculateAVOnSpeedChanged(CharacterBase character, int oldSpeed, int newSpeed)
        {
            var entry = FindEntry(character);
            if (entry == null)
                return;

            if (oldSpeed <= 0 || newSpeed <= 0)
            {
                ResetActionValue(entry);
                RefreshTurnOrderPreview();
                return;
            }

            // 속도가 올라가면 남은 AV가 줄어들고, 내려가면 늘어남
            entry.RemainingAV *= (float)oldSpeed / newSpeed;
            RefreshTurnOrderPreview();
        }

        /// <summary>
        /// 캐릭터 사망/퇴장 시 턴 목록에서 제거합니다.
        /// </summary>
        public void RemoveFromTurnSystem(CharacterBase character)
        {
            for (int i = _turnEntries.Count - 1; i >= 0; i--)
            {
                if (_turnEntries[i].Character == character)
                    _turnEntries.RemoveAt(i);
            }

            if (_currentTurnEntry != null && _currentTurnEntry.Character == character)
                _currentTurnEntry = null;

            RefreshTurnOrderPreview();
        }

        public void RefreshTurnOrderPreview()
        {
            _turnOrderPreview.Clear();
            RemoveInvalidEntries();

            _turnOrderPreview.AddRange(_turnEntries);
            _turnOrderPreview.Sort(CompareByRemainingAV);
        }

        private TurnEntry GetLowestAvEntry()
        {
            TurnEntry lowest = null;

            for (int i = 0; i < _turnEntries.Count; i++)
            {
                var entry = _turnEntries[i];
                if (lowest == null || CompareByRemainingAV(entry, lowest) < 0)
                    lowest = entry;
            }

            return lowest;
        }

        //속도관련 함수로 속도에 관해 추가적으로 기획을 해야겠다
        private void ResetActionValue(TurnEntry entry)
        {
            int speed = GetSpeed(entry.Character);
            if (speed <= 0)
            {
                Debug.LogWarning("[TurnSystem] Speed가 0 이하입니다. AV를 최댓값으로 둡니다.");
                entry.RemainingAV = BaseActionValue;
                return;
            }

            entry.RemainingAV = BaseActionValue / speed;
        }

        private TurnEntry FindEntry(CharacterBase character)
        {
            if (character == null)
                return null;

            for (int i = 0; i < _turnEntries.Count; i++)
            {
                if (_turnEntries[i].Character == character)
                    return _turnEntries[i];
            }

            return null;
        }

        private void RemoveInvalidEntries()
        {
            for (int i = _turnEntries.Count - 1; i >= 0; i--)
            {
                var entry = _turnEntries[i];
                if (entry?.Character == null || entry.Character.UnitInfo == null)
                    _turnEntries.RemoveAt(i);
            }
        }

        private static int CompareByRemainingAV(TurnEntry a, TurnEntry b)
        {
            int avCompare = a.RemainingAV.CompareTo(b.RemainingAV);
            if (avCompare != 0)
                return avCompare;

            // AV가 같으면 속도가 빠른 쪽 우선
            return GetSpeed(b.Character).CompareTo(GetSpeed(a.Character));
        }

        private static int GetSpeed(CharacterBase character)
        {
            if (character?.UnitInfo == null)
                return 0;

            return character.UnitInfo.CurrentSpeed;
        }

        private static string GetCharacterName(CharacterBase character)
        {
            if (character?.UnitInfo?.UnitData == null)
                return "Unknown";

            return string.IsNullOrEmpty(character.UnitInfo.UnitData.unitName)
                ? character.UnitInfo.UnitData.unitTid
                : character.UnitInfo.UnitData.unitName;
        }
    }

    [Serializable]
    public class TurnEntry
    {
        public CharacterBase Character { get; }
        public float RemainingAV { get; set; }

        public TurnEntry(CharacterBase character)
        {
            Character = character;
            RemainingAV = 0f;
        }
    }
}
