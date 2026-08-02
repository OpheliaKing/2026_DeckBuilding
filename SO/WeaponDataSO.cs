using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "WeaponDataSO", menuName = "SHIN/Weapon Data SO")]
    public class WeaponDataSO : ScriptableObject
    {
        [SerializeField] private List<WeaponData> _weaponDatas = new();

        public IReadOnlyList<WeaponData> WeaponDatas => _weaponDatas;
        public int Count => _weaponDatas.Count;

        public WeaponData GetWeaponData(int index)
        {
            if (index < 0 || index >= _weaponDatas.Count)
            {
                Debug.LogError($"[WeaponDataSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _weaponDatas[index];
        }

        public WeaponData GetWeaponData(string tid)
        {
            if (string.IsNullOrEmpty(tid))
            {
                Debug.LogError("[WeaponDataSO] tid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _weaponDatas.Count; i++)
            {
                if (_weaponDatas[i].Tid == tid)
                    return _weaponDatas[i];
            }

            Debug.LogError($"[WeaponDataSO] tid를 찾을 수 없습니다: {tid}");
            return null;
        }

        public bool TryGetWeaponData(string tid, out WeaponData weaponData)
        {
            weaponData = null;

            if (string.IsNullOrEmpty(tid))
                return false;

            for (int i = 0; i < _weaponDatas.Count; i++)
            {
                if (_weaponDatas[i].Tid == tid)
                {
                    weaponData = _weaponDatas[i];
                    return true;
                }
            }

            return false;
        }

        public WeaponData GetWeaponData(CHARACTER_EQUIP_TYPE weaponType)
        {
            for (int i = 0; i < _weaponDatas.Count; i++)
            {
                if (_weaponDatas[i].WeaponType == weaponType)
                    return _weaponDatas[i];
            }

            Debug.LogError($"[WeaponDataSO] WeaponType을 찾을 수 없습니다: {weaponType}");
            return null;
        }

        public bool TryGetWeaponData(CHARACTER_EQUIP_TYPE weaponType, out WeaponData weaponData)
        {
            weaponData = null;

            for (int i = 0; i < _weaponDatas.Count; i++)
            {
                if (_weaponDatas[i].WeaponType == weaponType)
                {
                    weaponData = _weaponDatas[i];
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public class WeaponData
    {
        public string Tid;
        public CHARACTER_EQUIP_TYPE WeaponType;
        public List<string> CardDeckList = new();

        public string IconPath;
        public string AnimName;

        /// <summary>
        /// 장착 위치별 무기 프리팹 Addressables 경로.
        /// Unity Dictionary 미직렬화 대신 List로 관리한다.
        /// </summary>
        public List<WeaponPrefabEntry> PrefabEntries = new();

        public bool TryGetPrefabPath(WEAPON_POSITION_TYPE position, out string prefabPath)
        {
            prefabPath = null;
            if (PrefabEntries == null)
                return false;

            for (int i = 0; i < PrefabEntries.Count; i++)
            {
                WeaponPrefabEntry entry = PrefabEntries[i];
                if (entry == null || entry.Position != position)
                    continue;

                if (string.IsNullOrEmpty(entry.PrefabPath))
                    continue;

                prefabPath = entry.PrefabPath;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// WEAPON_POSITION_TYPE → PrefabPath. Inspector 직렬화용 Dictionary 대체.
    /// </summary>
    [Serializable]
    public class WeaponPrefabEntry
    {
        public WEAPON_POSITION_TYPE Position = WEAPON_POSITION_TYPE.RIGHT_HAND;
        public string PrefabPath;
    }
}
