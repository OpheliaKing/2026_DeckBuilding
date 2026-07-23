using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace SHIN
{
    public class ResourceManager : ManagerBase
    {
        private readonly Dictionary<string, AsyncOperationHandle> _loadedHandles = new();
        private readonly Dictionary<ATLAS_TYPE, SpriteAtlas> _atlasCache = new();

        /// <summary>
        /// Addressables로 에셋을 로드합니다. 동일 주소는 캐시된 핸들을 재사용합니다.
        /// </summary>
        public async Task<T> LoadAsync<T>(string address) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError("[ResourceManager] address가 비어 있습니다.");
                return null;
            }

            if (_loadedHandles.TryGetValue(address, out var cached) && cached.IsValid())
            {
                if (cached.Status == AsyncOperationStatus.Succeeded)
                    return cached.Result as T;

                if (cached.IsDone == false)
                {
                    await cached.Task;
                    return cached.Status == AsyncOperationStatus.Succeeded ? cached.Result as T : null;
                }
            }

            var handle = Addressables.LoadAssetAsync<T>(address);
            _loadedHandles[address] = handle;

            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[ResourceManager] 로드 실패: {address}");
                _loadedHandles.Remove(address);
                return null;
            }

            return handle.Result;
        }

        /// <summary>
        /// 콜백 방식 로드.
        /// </summary>
        public void LoadAsync<T>(string address, Action<T> onComplete) where T : UnityEngine.Object
        {
            LoadAsyncInternal(address, onComplete);
        }

        private async void LoadAsyncInternal<T>(string address, Action<T> onComplete) where T : UnityEngine.Object
        {
            var result = await LoadAsync<T>(address);
            onComplete?.Invoke(result);
        }

        /// <summary>
        /// Addressables로 ScriptableObject를 로드합니다.
        /// </summary>
        public async Task<T> LoadScriptableObjectAsync<T>(string address) where T : ScriptableObject
        {
            return await LoadAsync<T>(address);
        }

        /// <summary>
        /// 콜백 방식으로 ScriptableObject를 로드합니다.
        /// </summary>
        public void LoadScriptableObjectAsync<T>(string address, Action<T> onComplete) where T : ScriptableObject
        {
            LoadAsync(address, onComplete);
        }

        /// <summary>
        /// Addressables로 프리팹을 로드한 뒤 즉시 생성합니다.
        /// </summary>
        public async Task<GameObject> InstantiateAsync(
            string address,
            Transform parent = null,
            bool instantiateInWorldSpace = false)
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError("[ResourceManager] address가 비어 있습니다.");
                return null;
            }

            var handle = Addressables.InstantiateAsync(address, parent, instantiateInWorldSpace);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[ResourceManager] 생성 실패: {address}");
                return null;
            }

            return handle.Result;
        }

        /// <summary>
        /// 위치/회전을 지정해 프리팹을 생성합니다.
        /// </summary>
        public async Task<GameObject> InstantiateAsync(
            string address,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError("[ResourceManager] address가 비어 있습니다.");
                return null;
            }

            var handle = Addressables.InstantiateAsync(address, position, rotation, parent);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[ResourceManager] 생성 실패: {address}");
                return null;
            }

            return handle.Result;
        }

        /// <summary>
        /// 콜백 방식 생성.
        /// </summary>
        public void InstantiateAsync(string address, Action<GameObject> onComplete, Transform parent = null)
        {
            InstantiateAsyncInternal(address, onComplete, parent);
        }

        private async void InstantiateAsyncInternal(
            string address,
            Action<GameObject> onComplete,
            Transform parent)
        {
            var result = await InstantiateAsync(address, parent);
            onComplete?.Invoke(result);
        }

        /// <summary>
        /// LoadAsync로 로드한 에셋 핸들을 해제합니다.
        /// </summary>
        public void Release(string address)
        {
            if (!_loadedHandles.TryGetValue(address, out var handle))
                return;

            if (handle.IsValid())
                Addressables.Release(handle);

            _loadedHandles.Remove(address);
        }

        /// <summary>
        /// InstantiateAsync로 생성한 인스턴스를 해제합니다.
        /// </summary>
        public void ReleaseInstance(GameObject instance)
        {
            if (instance == null)
                return;

            Addressables.ReleaseInstance(instance);
        }

        /// <summary>
        /// 로드해 둔 에셋 핸들을 모두 해제합니다. (Instantiate 인스턴스는 별도 ReleaseInstance)
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var handle in _loadedHandles.Values)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }

            _loadedHandles.Clear();
        }

        /// <summary>
        /// 아틀라스에서 스프라이트를 가져옵니다. 아틀라스는 캐시됩니다.
        /// </summary>
        public async Task<Sprite> GetAtlasSpriteAsync(ATLAS_TYPE atlasType, string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
            {
                Debug.LogError("[ResourceManager] spriteName이 비어 있습니다.");
                return null;
            }

            SpriteAtlas atlas = await GetAtlasAsync(atlasType);
            if (atlas == null)
                return null;

            Sprite sprite = atlas.GetSprite(spriteName);
            if (sprite == null)
            {
                Debug.LogError($"[ResourceManager] 아틀라스 스프라이트를 찾을 수 없습니다: {atlasType} / {spriteName}");
                return null;
            }

            return sprite;
        }

        /// <summary>
        /// 콜백 방식으로 아틀라스 스프라이트를 가져옵니다.
        /// </summary>
        public void GetAtlasSpriteAsync(ATLAS_TYPE atlasType, string spriteName, Action<Sprite> onComplete)
        {
            GetAtlasSpriteAsyncInternal(atlasType, spriteName, onComplete);
        }

        private async void GetAtlasSpriteAsyncInternal(
            ATLAS_TYPE atlasType,
            string spriteName,
            Action<Sprite> onComplete)
        {
            Sprite sprite = await GetAtlasSpriteAsync(atlasType, spriteName);
            onComplete?.Invoke(sprite);
        }

        /// <summary>
        /// ATLAS_TYPE에 해당하는 SpriteAtlas를 로드합니다.
        /// </summary>
        public async Task<SpriteAtlas> GetAtlasAsync(ATLAS_TYPE atlasType)
        {
            if (_atlasCache.TryGetValue(atlasType, out SpriteAtlas cached) && cached != null)
                return cached;

            if (!TryGetAtlasAddress(atlasType, out string address))
            {
                Debug.LogError($"[ResourceManager] 지원하지 않는 ATLAS_TYPE입니다: {atlasType}");
                return null;
            }

            SpriteAtlas atlas = await LoadAsync<SpriteAtlas>(address);
            if (atlas == null)
            {
                Debug.LogError($"[ResourceManager] 아틀라스 로드 실패: {atlasType} ({address})");
                return null;
            }

            _atlasCache[atlasType] = atlas;
            return atlas;
        }

        private static bool TryGetAtlasAddress(ATLAS_TYPE atlasType, out string address)
        {
            switch (atlasType)
            {
                case ATLAS_TYPE.UI:
                    address = PublicVariable.Address.UIAtlas;
                    return true;
                default:
                    address = null;
                    return false;
            }
        }

        private void OnDestroy()
        {
            _atlasCache.Clear();
            ReleaseAll();
        }
    }

    public enum ATLAS_TYPE
    {
        UI,
    }
}

