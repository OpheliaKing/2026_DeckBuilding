using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [CreateAssetMenu(fileName = "CharacterSelectDataSO", menuName = "SHIN/Character Select Data SO")]
    public class CharacterSelectDataSO : ScriptableObject
    {
        [SerializeField] private List<CharacterSelectData> _characterSelectDatas = new();

        public IReadOnlyList<CharacterSelectData> CharacterSelectDatas => _characterSelectDatas;
        public int Count => _characterSelectDatas.Count;

        public CharacterSelectData GetCharacterSelectData(int index)
        {
            if (index < 0 || index >= _characterSelectDatas.Count)
            {
                Debug.LogError($"[CharacterSelectDataSO] 인덱스 범위 초과: {index}");
                return null;
            }

            return _characterSelectDatas[index];
        }

        public CharacterSelectData GetCharacterSelectData(string tid)
        {
            if (string.IsNullOrEmpty(tid))
            {
                Debug.LogError("[CharacterSelectDataSO] tid가 비어 있습니다.");
                return null;
            }

            for (int i = 0; i < _characterSelectDatas.Count; i++)
            {
                if (_characterSelectDatas[i].Tid == tid)
                    return _characterSelectDatas[i];
            }

            Debug.LogError($"[CharacterSelectDataSO] tid를 찾을 수 없습니다: {tid}");
            return null;
        }

        public bool TryGetCharacterSelectData(string tid, out CharacterSelectData data)
        {
            data = null;

            if (string.IsNullOrEmpty(tid))
                return false;

            for (int i = 0; i < _characterSelectDatas.Count; i++)
            {
                if (_characterSelectDatas[i].Tid == tid)
                {
                    data = _characterSelectDatas[i];
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public class CharacterSelectData
    {
        public string Tid;
        public string Name;
        public string Description;
        public string Icon;
        public string PrefabPath;
        public string UnitDataSOTid;
    }
}
