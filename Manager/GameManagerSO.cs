using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SHIN
{
    public partial class GameManager
    {
        private const string unitDataSoAddress = "Assets/Addressables/SO/UnitDataSO.asset";
        public string UnitDataSoAddress => unitDataSoAddress;
        private readonly Dictionary<string, ScriptableObject> _scriptableObjects = new();

        /// <summary>
        /// 캐시된 SO를 반환하고, 없으면 ResourceManager로 로드한 뒤 저장합니다.
        /// </summary>
        public async Task<T> GetSOAsync<T>(string address) where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError("[GameManager] SO address가 비어 있습니다.");
                return null;
            }

            if (_scriptableObjects.TryGetValue(address, out var cached))
            {
                if (cached is T typed)
                    return typed;

                Debug.LogError(
                    $"[GameManager] SO 타입 불일치: {address}, 요청={typeof(T).Name}, 캐시={cached.GetType().Name}");
                return null;
            }

            var so = await ResourceManager.LoadScriptableObjectAsync<T>(address);
            if (so == null)
            {
                Debug.LogError($"[GameManager] SO 로드 실패: {address}");
                return null;
            }

            _scriptableObjects[address] = so;
            return so;
        }

        /// <summary>
        /// 콜백 방식 SO 조회/로드.
        /// </summary>
        public void GetSOAsync<T>(string address, Action<T> onComplete) where T : ScriptableObject
        {
            GetSOAsyncInternal(address, onComplete);
        }

        private async void GetSOAsyncInternal<T>(string address, Action<T> onComplete) where T : ScriptableObject
        {
            var result = await GetSOAsync<T>(address);
            onComplete?.Invoke(result);
        }

        /// <summary>
        /// 이미 로드된 SO만 동기적으로 가져옵니다. 없으면 false.
        /// </summary>
        public bool TryGetSO<T>(string address, out T so) where T : ScriptableObject
        {
            so = null;

            if (_scriptableObjects.TryGetValue(address, out var cached) && cached is T typed)
            {
                so = typed;
                return true;
            }

            return false;
        }
    }
}
