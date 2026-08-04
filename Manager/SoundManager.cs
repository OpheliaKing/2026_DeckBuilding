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
        VOICE,
    }

    /// <summary>
    /// BGM(단일) / SE(다중) / VOICE(단일) 재생. path 기준 AudioClip 캐시 후 ResourceManager로 로드.
    /// </summary>
    public class SoundManager : ManagerBase
    {
        [Header("Sources")]
        [SerializeField]
        private AudioSource _bgmSource;

        [SerializeField]
        private AudioSource _voiceSource;

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
        private float _voiceVolume = 1f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _masterVolume = 1f;

        [Header("Preload")]
        [Tooltip("Boot 시 Addressables로 미리 로드할 SE path 목록. 첫 클릭 지연 방지용.")]
        [SerializeField]
        private List<string> _preloadSePaths = new()
        {
            PublicVariable.Address.UiButtonClickSe,
            PublicVariable.Address.SeCardDraw,
            PublicVariable.Address.DefaultHitSe,
        };

        private readonly Dictionary<string, AudioClip> _clipCache = new();
        private readonly Dictionary<string, Task<AudioClip>> _loadingTasks = new();
        private readonly List<AudioSource> _seSources = new();
        private int _seNextIndex;

        private BGM_STATE _currentBgmState = BGM_STATE.None;
        private BGMDataSO _bgmDataSO;
        private Task<BGMDataSO> _bgmDataLoadTask;

        public float BgmVolume => _bgmVolume;
        public float SeVolume => _seVolume;
        public float VoiceVolume => _voiceVolume;
        public float MasterVolume => _masterVolume;
        public BGM_STATE CurrentBgmState => _currentBgmState;

        private void Awake()
        {
            EnsureSources();
            ApplyVolumes();
        }

        /// <summary>
        /// BGMDataSO 기준으로 상태별 BGM 재생.
        /// 같은 state가 이미 재생 중이면 유지한다. path가 비어 있으면 스킵한다.
        /// </summary>
        public void PlayBgm(BGM_STATE state, bool force = false)
        {
            PlayBgmByStateAsync(state, force);
        }

        public async Task PlayBgmByStateAsync(BGM_STATE state, bool force = false)
        {
            if (state == BGM_STATE.None)
            {
                StopBgm();
                return;
            }

            if (!force
                && state == _currentBgmState
                && _bgmSource != null
                && _bgmSource.isPlaying)
                return;

            BGMDataSO so = await GetBgmDataSOAsync();
            if (so == null || !so.TryGetBgmData(state, out BgmData data) || data == null)
            {
                Debug.LogWarning($"[SoundManager] BGM 데이터 없음: {state}");
                return;
            }

            if (string.IsNullOrEmpty(data.Path))
            {
                Debug.LogWarning($"[SoundManager] BGM path가 비어 있습니다: {state}");
                return;
            }

            bool played = await PlayBgmAsync(data.Path, data.Loop);
            if (played)
                _currentBgmState = state;
        }

        /// <summary>지정 state의 BGM 클립을 미리 로드한다.</summary>
        public void PreloadBgm(BGM_STATE state)
        {
            PreloadBgmAsync(state);
        }

        public async Task PreloadBgmAsync(BGM_STATE state)
        {
            if (state == BGM_STATE.None)
                return;

            BGMDataSO so = await GetBgmDataSOAsync();
            string path = so?.GetPath(state);
            if (string.IsNullOrEmpty(path))
                return;

            await GetOrLoadClipAsync(path);
        }

        /// <summary>BGM 재생. 이미 같은 path면 유지, 다르면 교체. loop 기본 true.</summary>
        public void PlayBgm(string path, bool loop = true)
        {
            PlayBgmAsync(path, loop);
        }

        public async Task<bool> PlayBgmAsync(string path, bool loop = true)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[SoundManager] BGM path가 비어 있습니다.");
                return false;
            }

            EnsureSources();
            AudioClip clip = await GetOrLoadClipAsync(path);
            if (clip == null || _bgmSource == null)
                return false;

            if (_bgmSource.clip == clip && _bgmSource.isPlaying)
            {
                _bgmSource.loop = loop;
                return true;
            }

            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            _bgmSource.volume = GetEffectiveVolume(SOUND_TYPE.BGM);
            _bgmSource.Play();
            return true;
        }

        public void StopBgm()
        {
            _currentBgmState = BGM_STATE.None;
            if (_bgmSource == null)
                return;

            _bgmSource.Stop();
            _bgmSource.clip = null;
        }

        private async Task<BGMDataSO> GetBgmDataSOAsync()
        {
            if (_bgmDataSO != null)
                return _bgmDataSO;

            if (_bgmDataLoadTask != null)
                return await _bgmDataLoadTask;

            _bgmDataLoadTask = LoadBgmDataSOInternalAsync();
            try
            {
                return await _bgmDataLoadTask;
            }
            finally
            {
                _bgmDataLoadTask = null;
            }
        }

        private async Task<BGMDataSO> LoadBgmDataSOInternalAsync()
        {
            if (_bgmDataSO != null)
                return _bgmDataSO;

            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[SoundManager] GameManager를 찾을 수 없습니다.");
                return null;
            }

            _bgmDataSO = await gameManager.GetSOAsync<BGMDataSO>(PublicVariable.Address.BGMDataSO);
            if (_bgmDataSO == null)
                Debug.LogError("[SoundManager] BGMDataSO 로드 실패");

            return _bgmDataSO;
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

        /// <summary>캐릭터 VOICE 재생. 이전 VOICE는 끊고 새로 재생한다.</summary>
        public void PlayVoice(string path)
        {
            PlayVoiceAsync(path);
        }

        public async Task PlayVoiceAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[SoundManager] VOICE path가 비어 있습니다.");
                return;
            }

            EnsureSources();
            AudioClip clip = await GetOrLoadClipAsync(path);
            if (clip == null || _voiceSource == null)
                return;

            _voiceSource.Stop();
            _voiceSource.clip = clip;
            _voiceSource.loop = false;
            _voiceSource.volume = GetEffectiveVolume(SOUND_TYPE.VOICE);
            _voiceSource.Play();
        }

        public void StopVoice()
        {
            if (_voiceSource == null)
                return;

            _voiceSource.Stop();
            _voiceSource.clip = null;
        }

        /// <summary>Inspector에 등록된 SE path들을 미리 로드한다.</summary>
        public void PreloadConfiguredSe()
        {
            PreloadConfiguredSeAsync();
        }

        public Task PreloadConfiguredSeAsync()
        {
            return PreloadSeAsync(_preloadSePaths);
        }

        /// <summary>SE 클립을 캐시에 미리 로드한다. 이미 있으면 스킵.</summary>
        public void PreloadSe(string path)
        {
            PreloadSeAsync(path);
        }

        public async Task PreloadSeAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            await GetOrLoadClipAsync(path);
        }

        public void PreloadSe(IEnumerable<string> paths)
        {
            PreloadSeAsync(paths);
        }

        public async Task PreloadSeAsync(IEnumerable<string> paths)
        {
            if (paths == null)
                return;

            var tasks = new List<Task>();
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;

                tasks.Add(GetOrLoadClipAsync(path));
            }

            if (tasks.Count == 0)
                return;

            await Task.WhenAll(tasks);
        }

        public bool IsSeCached(string path)
        {
            return !string.IsNullOrEmpty(path)
                   && _clipCache.TryGetValue(path, out AudioClip clip)
                   && clip != null;
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

        public void SetVoiceVolume(float volume)
        {
            _voiceVolume = Mathf.Clamp01(volume);
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
                case SOUND_TYPE.VOICE:
                    SetVoiceVolume(volume);
                    break;
            }
        }

        private async Task<AudioClip> GetOrLoadClipAsync(string path)
        {
            if (_clipCache.TryGetValue(path, out AudioClip cached) && cached != null)
                return cached;

            if (_loadingTasks.TryGetValue(path, out Task<AudioClip> inFlight))
                return await inFlight;

            Task<AudioClip> loadTask = LoadClipInternalAsync(path);
            _loadingTasks[path] = loadTask;

            try
            {
                return await loadTask;
            }
            finally
            {
                _loadingTasks.Remove(path);
            }
        }

        private async Task<AudioClip> LoadClipInternalAsync(string path)
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

            if (_voiceSource == null)
            {
                var voiceGo = new GameObject("VOICE");
                voiceGo.transform.SetParent(transform);
                _voiceSource = voiceGo.AddComponent<AudioSource>();
                _voiceSource.playOnAwake = false;
                _voiceSource.loop = false;
                _voiceSource.spatialBlend = 0f;
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
            float channel;
            switch (type)
            {
                case SOUND_TYPE.BGM:
                    channel = _bgmVolume;
                    break;
                case SOUND_TYPE.VOICE:
                    channel = _voiceVolume;
                    break;
                default:
                    channel = _seVolume;
                    break;
            }

            return Mathf.Clamp01(_masterVolume * channel);
        }

        private void ApplyVolumes()
        {
            if (_bgmSource != null)
                _bgmSource.volume = GetEffectiveVolume(SOUND_TYPE.BGM);

            if (_voiceSource != null)
                _voiceSource.volume = GetEffectiveVolume(SOUND_TYPE.VOICE);

            float seVol = GetEffectiveVolume(SOUND_TYPE.SE);
            for (int i = 0; i < _seSources.Count; i++)
            {
                if (_seSources[i] != null)
                    _seSources[i].volume = seVol;
            }
        }
    }
}
