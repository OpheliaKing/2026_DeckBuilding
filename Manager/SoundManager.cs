using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SHIN
{
    public enum SOUND_TYPE
    {
        BGM,
        SE,
    }

    /// <summary>
    /// BGM(단일) / SE(다중) 재생. path 기준 AudioClip 캐시 후 ResourceManager로 로드.
    /// </summary>
    public class SoundManager : ManagerBase
    {
        [Header("Sources")]
        [SerializeField]
        private AudioSource _bgmSource;

        [SerializeField]
        private int _seSourceCount = 8;

        [Header("Volume")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _bgmVolume = 1f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _seVolume = 1f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _masterVolume = 1f;

        private readonly Dictionary<string, AudioClip> _clipCache = new();
        private readonly List<AudioSource> _seSources = new();
        private int _seNextIndex;

        public float BgmVolume => _bgmVolume;
        public float SeVolume => _seVolume;
        public float MasterVolume => _masterVolume;

        private void Awake()
        {
            EnsureSources();
            ApplyVolumes();
        }

        /// <summary>BGM 재생. 이미 같은 path면 유지, 다르면 교체. loop 기본 true.</summary>
        public void PlayBgm(string path, bool loop = true)
        {
            PlayBgmAsync(path, loop);
        }

        public async Task PlayBgmAsync(string path, bool loop = true)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[SoundManager] BGM path가 비어 있습니다.");
                return;
            }

            EnsureSources();
            AudioClip clip = await GetOrLoadClipAsync(path);
            if (clip == null || _bgmSource == null)
                return;

            if (_bgmSource.clip == clip && _bgmSource.isPlaying)
            {
                _bgmSource.loop = loop;
                return;
            }

            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            _bgmSource.volume = GetEffectiveVolume(SOUND_TYPE.BGM);
            _bgmSource.Play();
        }

        public void StopBgm()
        {
            if (_bgmSource == null)
                return;

            _bgmSource.Stop();
            _bgmSource.clip = null;
        }

        /// <summary>SE 재생. 여러 개 동시 재생 가능.</summary>
        public void PlaySe(string path)
        {
            PlaySeAsync(path);
        }

        public async Task PlaySeAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[SoundManager] SE path가 비어 있습니다.");
                return;
            }

            EnsureSources();
            AudioClip clip = await GetOrLoadClipAsync(path);
            if (clip == null)
                return;

            AudioSource source = GetAvailableSeSource();
            if (source == null)
            {
                Debug.LogWarning("[SoundManager] 사용 가능한 SE AudioSource가 없습니다.");
                return;
            }

            source.volume = GetEffectiveVolume(SOUND_TYPE.SE);
            source.PlayOneShot(clip, 1f);
        }

        public void StopAllSe()
        {
            for (int i = 0; i < _seSources.Count; i++)
            {
                if (_seSources[i] != null)
                    _seSources[i].Stop();
            }
        }

        public void SetBgmVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        public void SetSeVolume(float volume)
        {
            _seVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        public void SetVolume(SOUND_TYPE type, float volume)
        {
            switch (type)
            {
                case SOUND_TYPE.BGM:
                    SetBgmVolume(volume);
                    break;
                case SOUND_TYPE.SE:
                    SetSeVolume(volume);
                    break;
            }
        }

        private async Task<AudioClip> GetOrLoadClipAsync(string path)
        {
            if (_clipCache.TryGetValue(path, out AudioClip cached) && cached != null)
                return cached;

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[SoundManager] ResourceManager를 찾을 수 없습니다.");
                return null;
            }

            AudioClip clip = await resourceManager.LoadAsync<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogError($"[SoundManager] AudioClip 로드 실패: {path}");
                return null;
            }

            _clipCache[path] = clip;
            return clip;
        }

        private void EnsureSources()
        {
            if (_bgmSource == null)
            {
                var bgmGo = new GameObject("BGM");
                bgmGo.transform.SetParent(transform);
                _bgmSource = bgmGo.AddComponent<AudioSource>();
                _bgmSource.playOnAwake = false;
                _bgmSource.loop = true;
                _bgmSource.spatialBlend = 0f;
            }

            int targetCount = Mathf.Max(1, _seSourceCount);
            while (_seSources.Count < targetCount)
            {
                var seGo = new GameObject($"SE_{_seSources.Count}");
                seGo.transform.SetParent(transform);
                var source = seGo.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                _seSources.Add(source);
            }
        }

        private AudioSource GetAvailableSeSource()
        {
            if (_seSources.Count == 0)
                return null;

            for (int i = 0; i < _seSources.Count; i++)
            {
                int index = (_seNextIndex + i) % _seSources.Count;
                AudioSource source = _seSources[index];
                if (source != null && !source.isPlaying)
                {
                    _seNextIndex = (index + 1) % _seSources.Count;
                    return source;
                }
            }

            // 전부 재생 중이면 라운드로빈으로 하나 재사용
            AudioSource fallback = _seSources[_seNextIndex];
            _seNextIndex = (_seNextIndex + 1) % _seSources.Count;
            return fallback;
        }

        private float GetEffectiveVolume(SOUND_TYPE type)
        {
            float channel = type == SOUND_TYPE.BGM ? _bgmVolume : _seVolume;
            return Mathf.Clamp01(_masterVolume * channel);
        }

        private void ApplyVolumes()
        {
            if (_bgmSource != null)
                _bgmSource.volume = GetEffectiveVolume(SOUND_TYPE.BGM);

            float seVol = GetEffectiveVolume(SOUND_TYPE.SE);
            for (int i = 0; i < _seSources.Count; i++)
            {
                if (_seSources[i] != null)
                    _seSources[i].volume = seVol;
            }
        }
    }
}
