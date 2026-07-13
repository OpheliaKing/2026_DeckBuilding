using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public class CharacterBase : MonoBehaviour
    {
        private UnitInfo _unitInfo;
        public UnitInfo UnitInfo => _unitInfo;

        public void InitCharacter(UnitData unitData)
        {
            _unitInfo = new UnitInfo(unitData);
        }

        public void InitCharacter(UnitInfo unitInfo)
        {
            _unitInfo = unitInfo;
        }
    }

}

