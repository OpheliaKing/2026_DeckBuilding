using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public enum BGM_STATE
    {
        None = 0,
        Intro,
        CharacterSelect,
        StageMap,
        Battle,
        Shop,
        Event,
    }

    [Serializable]
    public class BgmData
    {
        public BGM_STATE State = BGM_STATE.None;
        public string Path;
        public bool Loop = true;
    }

    [CreateAssetMenu(fileName = "BGMDataSO", menuName = "SHIN/BGM Data SO")]
    public class BGMDataSO : ScriptableObject
    {
        [SerializeField]
        private List<BgmData> _bgmDatas = new();

        public IReadOnlyList<BgmData> BgmDatas => _bgmDatas;
        public int Count => _bgmDatas.Count;

        public bool TryGetBgmData(BGM_STATE state, out BgmData bgmData)
        {
            bgmData = null;
            if (state == BGM_STATE.None)
                return false;

            for (int i = 0; i < _bgmDatas.Count; i++)
            {
                BgmData data = _bgmDatas[i];
                if (data == null)
                    continue;

                if (data.State == state)
                {
                    bgmData = data;
                    return true;
                }
            }

            return false;
        }

        public string GetPath(BGM_STATE state)
        {
            if (!TryGetBgmData(state, out BgmData data) || data == null)
                return null;

            return data.Path;
        }
    }
}
