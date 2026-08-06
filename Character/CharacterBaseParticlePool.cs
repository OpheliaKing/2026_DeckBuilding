using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 캐릭터 단위 전투 이펙트 풀.
    /// ResourceManager로 프리팹을 로드한 뒤, 주소별로 인스턴스를 재사용합니다.
    /// World 스폰도 활성 목록으로 추적해 스테이지/캐릭터 Destroy 시 씬에 남지 않게 합니다.
    /// </summary>
    public partial class CharacterBase
    {
        private readonly Dictionary<string, Queue<GameObject>> _effectPools = new();
        private readonly Dictionary<string, GameObject> _effectPrefabs = new();
        private readonly HashSet<string> _effectLoading = new();
        private readonly Dictionary<GameObject, string> _activeEffects = new();
        private Transform _effectPoolRoot;
        private bool _effectPoolDisposed;

        /// <summary>
        /// Addressables 주소로 파티클을 스폰합니다. 없으면 로드 후 풀에서 꺼냅니다.
        /// </summary>
        public void SpawnParticleEffect(
            string address,
            ParticleSpawnSpace spawnSpace,
            Vector3 positionOffset,
            Vector3 rotationOffset,
            Transform origin)
        {
            if (_effectPoolDisposed || string.IsNullOrWhiteSpace(address) || origin == null)
                return;

            EnsureEffectPoolRoot();

            if (_effectPrefabs.TryGetValue(address, out var prefab) && prefab != null)
            {
                ActivatePooledEffect(address, prefab, spawnSpace, positionOffset, rotationOffset, origin);
                return;
            }

            if (_effectLoading.Contains(address))
                return;

            _effectLoading.Add(address);

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                _effectLoading.Remove(address);
                Debug.LogWarning($"[EffectPool] ResourceManager 없음: {address}");
                return;
            }

            resourceManager.LoadAsync<GameObject>(address, loaded =>
            {
                _effectLoading.Remove(address);

                if (loaded == null)
                {
                    Debug.LogWarning($"[EffectPool] 파티클 로드 실패: {address}");
                    return;
                }

                _effectPrefabs[address] = loaded;

                if (this == null || _effectPoolDisposed || !isActiveAndEnabled)
                    return;

                ActivatePooledEffect(address, loaded, spawnSpace, positionOffset, rotationOffset, origin);
            });
        }

        /// <summary>
        /// 카드 피격 이펙트를 HitEffectPoint에 스폰합니다.
        /// </summary>
        public void SpawnHitEffect(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return;

            SpawnParticleEffect(
                address,
                ParticleSpawnSpace.World,
                Vector3.zero,
                Vector3.zero,
                HitEffectPoint);
        }

        private void ActivatePooledEffect(
            string address,
            GameObject prefab,
            ParticleSpawnSpace spawnSpace,
            Vector3 positionOffset,
            Vector3 rotationOffset,
            Transform origin)
        {
            if (_effectPoolDisposed || origin == null)
                return;

            var instance = RentEffect(address, prefab);
            if (instance == null)
                return;

            if (spawnSpace == ParticleSpawnSpace.Child)
            {
                instance.transform.SetParent(origin, false);
                instance.transform.localPosition = positionOffset;
                instance.transform.localRotation = Quaternion.Euler(rotationOffset);
            }
            else
            {
                // 스테이지와 수명을 맞추기 위해 InGameManager 아래에 두고, 활성 목록으로도 추적
                Transform worldRoot = GameManager.Instance?.InGameManager != null
                    ? GameManager.Instance.InGameManager.transform
                    : null;
                instance.transform.SetParent(worldRoot, true);
                instance.transform.SetPositionAndRotation(
                    origin.TransformPoint(positionOffset),
                    origin.rotation * Quaternion.Euler(rotationOffset));
            }

            _activeEffects[instance] = address;
            instance.SetActive(true);
            RestartParticleSystems(instance);
            StartCoroutine(ReturnEffectWhenFinished(address, instance));
        }

        private GameObject RentEffect(string address, GameObject prefab)
        {
            if (!_effectPools.TryGetValue(address, out var pool))
            {
                pool = new Queue<GameObject>();
                _effectPools[address] = pool;
            }

            while (pool.Count > 0)
            {
                var pooled = pool.Dequeue();
                if (pooled != null)
                    return pooled;
            }

            var created = Instantiate(prefab, _effectPoolRoot);
            created.name = $"{prefab.name}_Pooled";
            created.SetActive(false);
            return created;
        }

        private IEnumerator ReturnEffectWhenFinished(string address, GameObject instance)
        {
            if (instance == null)
                yield break;

            float wait = EstimateEffectDuration(instance);
            float elapsed = 0f;

            while (elapsed < wait)
            {
                if (_effectPoolDisposed || instance == null)
                    yield break;

                // 히트스톱/일시정지에 맞춰 이펙트 수명도 CharacterTimeScale을 따름
                float scale = GameManager.Instance?.TimeManager != null
                    ? GameManager.Instance.TimeManager.EffectiveCharacterTimeScale
                    : 1f;
                elapsed += Time.deltaTime * Mathf.Max(0f, scale);
                yield return null;
            }

            if (!_effectPoolDisposed)
                ReturnEffect(address, instance);
        }

        private void ReturnEffect(string address, GameObject instance)
        {
            if (instance == null)
                return;

            _activeEffects.Remove(instance);

            if (_effectPoolDisposed || string.IsNullOrEmpty(address) || !isActiveAndEnabled)
            {
                Destroy(instance);
                return;
            }

            EnsureEffectPoolRoot();
            instance.SetActive(false);
            instance.transform.SetParent(_effectPoolRoot, false);

            if (!_effectPools.TryGetValue(address, out var pool))
            {
                pool = new Queue<GameObject>();
                _effectPools[address] = pool;
            }

            pool.Enqueue(instance);
        }

        private void EnsureEffectPoolRoot()
        {
            if (_effectPoolRoot != null)
                return;

            var root = new GameObject("EffectPool");
            root.transform.SetParent(transform, false);
            root.SetActive(false);
            _effectPoolRoot = root.transform;
        }

        private static void RestartParticleSystems(GameObject instance)
        {
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[i].Play(true);
            }
        }

        private static float EstimateEffectDuration(GameObject instance)
        {
            float duration = 1f;
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                var main = particles[i].main;
                float startLifetime = main.startLifetime.mode == ParticleSystemCurveMode.Constant
                    ? main.startLifetime.constant
                    : main.startLifetime.constantMax;
                float life = main.duration + startLifetime;
                if (life > duration)
                    duration = life;
            }

            return Mathf.Clamp(duration, 0.1f, 10f);
        }

        /// <summary>
        /// 풀 + 월드에 떠 있는 활성 이펙트까지 모두 파괴합니다.
        /// 스테이지 해제 직전/캐릭터 Destroy 시 호출합니다.
        /// </summary>
        public void ReleaseCombatEffects()
        {
            CleanupEffectPools();
            ReleaseHealthBar();
        }

        private void CleanupEffectPools()
        {
            if (_effectPoolDisposed)
                return;

            _effectPoolDisposed = true;

            if (_activeEffects.Count > 0)
            {
                var actives = new List<GameObject>(_activeEffects.Keys);
                _activeEffects.Clear();
                for (int i = 0; i < actives.Count; i++)
                {
                    if (actives[i] != null)
                        Destroy(actives[i]);
                }
            }

            foreach (var pair in _effectPools)
            {
                while (pair.Value.Count > 0)
                {
                    var go = pair.Value.Dequeue();
                    if (go != null)
                        Destroy(go);
                }
            }

            _effectPools.Clear();
            _effectPrefabs.Clear();
            _effectLoading.Clear();

            if (_effectPoolRoot != null)
            {
                Destroy(_effectPoolRoot.gameObject);
                _effectPoolRoot = null;
            }
        }
    }
}
