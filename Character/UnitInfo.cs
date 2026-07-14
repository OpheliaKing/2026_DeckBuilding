using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public class UnitInfo
    {

        public UnitInfo(UnitData unitData)
        {
            _unitData = unitData;
            InitUnitInfo();
        }

        private UnitData _unitData;
        public UnitData UnitData => _unitData;

        private List<CardData> _deckCardList = new List<CardData>();
        public IReadOnlyList<CardData> DeckCardList => _deckCardList;

        private int _maxHp;
        public int MaxHp => CalculateMaxHp();

        private int _currentHp;
        public int CurrentHp => _currentHp;

        public int CurrentAttack => CalculateCurrentAttack();

        public int CurrentDefense => CalculateCurrentDefense();

        public int CurrentSpeed => CalculateCurrentSpeed();

        private List<string> _itemList = new List<string>();

        public void InitUnitInfo(UnitData unitData)
        {
            _unitData = unitData;
            InitUnitInfo();
        }

        public void InitUnitInfo()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return;
            }

            _currentHp = MaxHp;
        }

        private int CalculateMaxHp()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 최대 체력 반환하도록 수정
            return _unitData.unitBaseHp;
        }

        private int CalculateCurrentAttack()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 데미지 반환하도록 수정
            return _unitData.unitBaseAttack;
        }

        private int CalculateCurrentDefense()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 방어력 반환하도록 수정
            return _unitData.unitBaseDefense;
        }

        private int CalculateCurrentSpeed()
        {
            if (_unitData == null)
            {
                Debug.LogError("UnitData is null");
                return 0;
            }
            ///아이템 계산 후 속도 반환하도록 수정
            return _unitData.unitBaseSpeed;
        }

        public void AddCard(CardData cardData)
        {
            if (cardData == null)
            {
                Debug.LogError("CardData is null");
                return;
            }
            _deckCardList.Add(cardData);
        }

    }

}

