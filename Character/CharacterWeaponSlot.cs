using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 캐릭터 모델의 무기 장착 소켓.
    /// 무기 인스턴스는 캐시하며, 교체 시 비활성/활성으로 전환한다.
    /// </summary>
    public class CharacterWeaponSlot : MonoBehaviour
    {
        [SerializeField]
        private Transform _rightHandPos;

        [SerializeField]
        private Transform _leftHandPos;

        private readonly Dictionary<string, GameObject> _weaponCache = new();
        private readonly HashSet<string> _activeKeys = new();
        private int _equipVersion;

        public Transform GetPosition(WEAPON_POSITION_TYPE positionType)
        {
            switch (positionType)
            {
                case WEAPON_POSITION_TYPE.RIGHT_HAND:
                    return _rightHandPos;
                case WEAPON_POSITION_TYPE.LEFT_HAND:
                    return _leftHandPos;
                default:
                    return null;
            }
        }

        /// <summary>
        /// WeaponData PrefabEntries를 장착한다. 캐시에 있으면 활성화, 없으면 생성한다.
        /// 이전에 보이던 무기는 비활성화한다(파괴하지 않음).
        /// </summary>
        public async Task EquipAsync(WeaponData weaponData, ResourceManager resourceManager)
        {
            _equipVersion++;
            int version = _equipVersion;

            HideActiveWeapons();

            if (weaponData == null || weaponData.PrefabEntries == null || weaponData.PrefabEntries.Count == 0)
                return;

            if (resourceManager == null)
            {
                Debug.LogError($"[CharacterWeaponSlot] ResourceManager가 없습니다: {name}");
                return;
            }

            for (int i = 0; i < weaponData.PrefabEntries.Count; i++)
            {
                if (version != _equipVersion)
                    return;

                WeaponPrefabEntry entry = weaponData.PrefabEntries[i];
                if (entry == null || string.IsNullOrEmpty(entry.PrefabPath))
                    continue;

                Transform parent = GetPosition(entry.Position);
                if (parent == null)
                {
                    Debug.LogWarning(
                        $"[CharacterWeaponSlot] 소켓 없음: {name} / {entry.Position} / {entry.PrefabPath}");
                    continue;
                }

                string key = MakeCacheKey(entry.Position, entry.PrefabPath);
                if (_weaponCache.TryGetValue(key, out GameObject cached) && cached != null)
                {
                    ActivateCachedWeapon(cached, parent, key);
                    continue;
                }

                if (_weaponCache.ContainsKey(key))
                    _weaponCache.Remove(key);

                GameObject instance = await resourceManager.InstantiateAsync(entry.PrefabPath, parent);
                if (version != _equipVersion)
                {
                    if (instance != null)
                        resourceManager.ReleaseInstance(instance);
                    return;
                }

                if (instance == null)
                {
                    Debug.LogError($"[CharacterWeaponSlot] 무기 생성 실패: {entry.PrefabPath}");
                    continue;
                }

                ResetLocalTransform(instance.transform);
                _weaponCache[key] = instance;
                _activeKeys.Add(key);
                instance.SetActive(true);
            }
        }

        /// <summary>
        /// 현재 보이는 무기만 비활성화한다. 캐시는 유지한다.
        /// </summary>
        public void HideEquipped()
        {
            _equipVersion++;
            HideActiveWeapons();
        }

        /// <summary>
        /// 캐시까지 전부 해제한다. 모델 파괴 시 사용.
        /// </summary>
        public void ReleaseAll(ResourceManager resourceManager = null)
        {
            _equipVersion++;
            _activeKeys.Clear();

            if (resourceManager == null)
                resourceManager = GameManager.Instance?.ResourceManager;

            foreach (var pair in _weaponCache)
            {
                GameObject go = pair.Value;
                if (go == null)
                    continue;

                if (resourceManager != null)
                    resourceManager.ReleaseInstance(go);
                else
                    Destroy(go);
            }

            _weaponCache.Clear();
        }

        /// <summary>하위 호환. 캐시 유지 숨김이 기본이다.</summary>
        public void ClearEquipped(ResourceManager resourceManager = null)
        {
            HideEquipped();
        }

        private void HideActiveWeapons()
        {
            foreach (string key in _activeKeys)
            {
                if (!_weaponCache.TryGetValue(key, out GameObject go) || go == null)
                    continue;

                go.SetActive(false);
            }

            _activeKeys.Clear();
        }

        private void ActivateCachedWeapon(GameObject cached, Transform parent, string key)
        {
            if (cached.transform.parent != parent)
                cached.transform.SetParent(parent, false);

            ResetLocalTransform(cached.transform);
            cached.SetActive(true);
            _activeKeys.Add(key);
        }

        private static void ResetLocalTransform(Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        private static string MakeCacheKey(WEAPON_POSITION_TYPE position, string prefabPath)
        {
            return ((int)position).ToString() + "|" + prefabPath;
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }
    }
}
