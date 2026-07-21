using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public partial class GameManager
    {
        private List<UnitInfo> _playerCharacters = new List<UnitInfo>();
        public IReadOnlyList<UnitInfo> PlayerCharacters => _playerCharacters;

        private void AddPlayerCharacter(string unitTid,Action<UnitInfo> onComplete = null)
        {

            if (string.IsNullOrEmpty(unitTid))
            {
                return;
            }

            GetSOAsync<UnitDataSO>(PublicVariable.Address.UnitDataSO, unitDataSO =>
            {
                if (unitDataSO == null)
                {
                    Debug.LogError("[InGameManager] UnitDataSO 로드 실패");
                    return;
                }

                var data = unitDataSO.GetUnitData(unitTid);
                if (data == null)
                {
                    Debug.LogError("[InGameManager] UnitData 로드 실패");
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

