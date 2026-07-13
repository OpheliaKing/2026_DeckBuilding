using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ResourceManager : ManagerBase
{
    private readonly Dictionary<string, AsyncOperationHandle> _loadedHandles = new();

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

    private void OnDestroy()
    {
        ReleaseAll();
    }
}
