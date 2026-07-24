using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class GameManager
    {
        private List<UnitInfo> _playerCharacters = new List<UnitInfo>();
        public IReadOnlyList<UnitInfo> PlayerCharacters => _playerCharacters;

        /// <summary>
        /// 유닛 세팅 UI에서 캐릭터/무기 확정 시 호출.
        /// UnitInfo 생성 → 장비 타입 → 기본 덱 카드 추가.
        /// </summary>
        public void SetupPlayerCharacter(
            string unitTid,
            CHARACTER_EQUIP_TYPE equipType,
            IReadOnlyList<string> cardTids,
            Action<UnitInfo> onComplete = null)
        {
            AddPlayerCharacter(unitTid, unitInfo =>
            {
                if (unitInfo == null)
                {
                    onComplete?.Invoke(null);
                    return;
                }

                unitInfo.SetEquipType(equipType);

                if (cardTids == null || cardTids.Count == 0)
                {
                    onComplete?.Invoke(unitInfo);
                    return;
                }

                var cardList = new List<string>(cardTids.Count);
                for (int i = 0; i < cardTids.Count; i++)
                {
                    if (!string.IsNullOrEmpty(cardTids[i]))
                        cardList.Add(cardTids[i]);
                }

                if (cardList.Count == 0)
                {
                    onComplete?.Invoke(unitInfo);
                    return;
                }

                AddCard(unitInfo, cardList, onComplete);
            });
        }

        private void AddPlayerCharacter(string unitTid, Action<UnitInfo> onComplete = null)
        {
            if (string.IsNullOrEmpty(unitTid))
            {
                onComplete?.Invoke(null);
                return;
            }

            GetSOAsync<UnitDataSO>(PublicVariable.Address.UnitDataSO, unitDataSO =>
            {
                if (unitDataSO == null)
                {
                    Debug.LogError("[GameManager] UnitDataSO 로드 실패");
                    onComplete?.Invoke(null);
                    return;
                }

                var data = unitDataSO.GetUnitData(unitTid);
                if (data == null)
                {
                    Debug.LogError($"[GameManager] UnitData 로드 실패: {unitTid}");
                    onComplete?.Invoke(null);
                    return;
                }

                var unitInfo = new UnitInfo(data);
                unitInfo.SetUnitType(UNIT_TYPE.PLAYER);
                AddPlayerCharacter(unitInfo);
                onComplete?.Invoke(unitInfo);
            });
        }

        private void AddPlayerCharacter(UnitInfo unitInfo)
        {
            _playerCharacters.Add(unitInfo);
        }
    }
}
