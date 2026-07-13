using UnityEngine;

public class ManagerBase : MonoBehaviour
{
    /// <summary>
    /// 참조가 없으면 자식에서 찾고, 없으면 자식 오브젝트를 만들어 컴포넌트를 붙입니다.
    /// </summary>
    protected T EnsureManager<T>(ref T manager) where T : ManagerBase
    {
        return EnsureManager(transform, ref manager);
    }

    /// <summary>
    /// 어떤 부모 Transform에서도 사용할 수 있는 정적 버전입니다.
    /// </summary>
    public static T EnsureManager<T>(Transform parent, ref T manager) where T : ManagerBase
    {
        if (manager != null)
            return manager;

        if (parent == null)
        {
            Debug.LogError($"[ManagerBase] EnsureManager<{typeof(T).Name}> parent가 null입니다.");
            return null;
        }

        manager = parent.GetComponentInChildren<T>(true);
        if (manager != null)
            return manager;

        var go = new GameObject(typeof(T).Name);
        go.transform.SetParent(parent);
        manager = go.AddComponent<T>();
        return manager;
    }
}
