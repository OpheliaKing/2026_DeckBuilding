using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 타이틀용 연분홍 꽃잎 낙하.
    /// 소수 개수가 대각선으로 천천히 떠내려가며, Inspector에서 개수 조절 가능.
    /// </summary>
    public class RosePetalFallUI : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _spawnArea;

        [SerializeField]
        private Sprite[] _petalSprites;

        [Header("Count")]
        [Tooltip("화면에 동시에 보이는 꽃잎 개수 (참고 타이틀은 대략 6~12)")]
        [SerializeField]
        [Range(1, 80)]
        private int _visibleCount = 10;

        [SerializeField]
        private int _poolExtra = 4;

        [Header("Motion (slow diagonal)")]
        [Tooltip("아래로 떨어지는 속도")]
        [SerializeField]
        private Vector2 _fallSpeedRange = new(12f, 28f);

        [Tooltip("대각선 드리프트(가로). 양수=오른쪽, 음수=왼쪽")]
        [SerializeField]
        private Vector2 _driftSpeedRange = new(10f, 26f);

        [SerializeField]
        private bool _preferDriftRight = true;

        [SerializeField]
        private Vector2 _swayAmplitudeRange = new(8f, 22f);

        [SerializeField]
        private Vector2 _swayFrequencyRange = new(0.2f, 0.55f);

        [SerializeField]
        private Vector2 _spinSpeedRange = new(-28f, 28f);

        [SerializeField]
        private Vector2 _tumbleFrequencyRange = new(0.12f, 0.35f);

        [SerializeField]
        private Vector2 _sizeRange = new(22f, 42f);

        [SerializeField]
        private Vector2 _alphaRange = new(0.62f, 0.88f);

        [Tooltip("연분홍 틴트")]
        [SerializeField]
        private Color _tint = new(1f, 0.82f, 0.9f, 1f);

        [SerializeField]
        private bool _playOnEnable = true;

        private readonly List<Petal> _petals = new();
        private readonly Queue<Petal> _pool = new();
        private bool _playing;
        private RectTransform _rect;
        private int _builtPoolSize;

        private sealed class Petal
        {
            public RectTransform Rect;
            public Image Image;
            public CanvasGroup Group;
            public float FallSpeed;
            public float DriftSpeed;
            public float SwayAmp;
            public float SwayFreq;
            public float SpinSpeed;
            public float TumbleFreq;
            public float BaseX;
            public float Phase;
            public float TumblePhase;
            public float Life;
            public float Size;
        }

        public int VisibleCount
        {
            get => _visibleCount;
            set
            {
                _visibleCount = Mathf.Clamp(value, 1, 80);
                if (_playing)
                    SyncVisibleCount();
            }
        }

        private void Awake()
        {
            _rect = transform as RectTransform;
            if (_spawnArea == null)
                _spawnArea = _rect;

            EnsurePool();
        }

        private void OnEnable()
        {
            if (_playOnEnable)
                Play();
        }

        private void OnDisable()
        {
            Stop(clearVisible: true);
        }

        public void Play()
        {
            EnsurePool();
            _playing = true;
            PrefillScreen();
        }

        public void Stop(bool clearVisible = false)
        {
            _playing = false;
            if (!clearVisible)
                return;

            for (int i = _petals.Count - 1; i >= 0; i--)
                Recycle(_petals[i]);
            _petals.Clear();
        }

        private void Update()
        {
            if (!_playing)
            {
                UpdateActivePetals(Time.unscaledDeltaTime, recycleOffscreen: false);
                return;
            }

            float dt = Time.unscaledDeltaTime;
            UpdateActivePetals(dt, recycleOffscreen: true);
            SyncVisibleCount();
        }

        private void PrefillScreen()
        {
            for (int i = _petals.Count - 1; i >= 0; i--)
                Recycle(_petals[i]);
            _petals.Clear();

            int target = Mathf.Clamp(_visibleCount, 1, 80);
            for (int i = 0; i < target; i++)
                SpawnOne(prefill: true);
        }

        private void SyncVisibleCount()
        {
            int target = Mathf.Clamp(_visibleCount, 1, 80);
            EnsurePool();

            while (_petals.Count < target)
                SpawnOne(prefill: true);

            while (_petals.Count > target)
            {
                int last = _petals.Count - 1;
                Recycle(_petals[last]);
                _petals.RemoveAt(last);
            }
        }

        private void UpdateActivePetals(float dt, bool recycleOffscreen)
        {
            Rect area = _spawnArea != null ? _spawnArea.rect : _rect.rect;
            float bottom = area.yMin - 50f;
            float left = area.xMin - 60f;
            float right = area.xMax + 60f;

            for (int i = _petals.Count - 1; i >= 0; i--)
            {
                Petal petal = _petals[i];
                if (petal?.Rect == null)
                {
                    _petals.RemoveAt(i);
                    continue;
                }

                petal.Life += dt;
                petal.BaseX += petal.DriftSpeed * dt;

                Vector2 pos = petal.Rect.anchoredPosition;
                pos.y -= petal.FallSpeed * dt;
                pos.x = petal.BaseX + Mathf.Sin(petal.Life * petal.SwayFreq + petal.Phase) * petal.SwayAmp;
                petal.Rect.anchoredPosition = pos;

                float z = petal.Rect.localEulerAngles.z + petal.SpinSpeed * dt;
                petal.Rect.localEulerAngles = new Vector3(0f, 0f, z);

                // 약한 뒤집힘 (과하지 않게)
                float tumble = Mathf.Sin(petal.Life * petal.TumbleFreq + petal.TumblePhase);
                float sx = Mathf.Lerp(0.55f, 1f, (tumble + 1f) * 0.5f);
                petal.Rect.localScale = new Vector3(sx, 1f, 1f);

                if (!recycleOffscreen)
                    continue;

                if (pos.y < bottom || pos.x < left || pos.x > right)
                    RespawnAtTop(petal, area);
            }
        }

        private void SpawnOne(bool prefill)
        {
            if (_petalSprites == null || _petalSprites.Length == 0)
                return;

            Petal petal = Rent();
            if (petal == null)
                return;

            Rect area = _spawnArea.rect;
            ConfigurePetal(petal, area, prefill);
            petal.Rect.gameObject.SetActive(true);
            _petals.Add(petal);
        }

        private void RespawnAtTop(Petal petal, Rect area)
        {
            ConfigurePetal(petal, area, prefill: false);
        }

        private void ConfigurePetal(Petal petal, Rect area, bool prefill)
        {
            float size = Random.Range(_sizeRange.x, _sizeRange.y);
            float margin = size * 0.5f;
            float x;
            float y;

            if (prefill)
            {
                x = Random.Range(area.xMin + margin, area.xMax - margin);
                y = Random.Range(area.yMin + size, area.yMax - size * 0.2f);
            }
            else
            {
                // 대각선 낙하: 위쪽 + 진행 반대쪽에서 유입
                if (_preferDriftRight)
                    x = Random.Range(area.xMin - size * 0.5f, area.xMax * 0.35f);
                else
                    x = Random.Range(area.xMax * 0.65f, area.xMax + size * 0.5f);
                y = area.yMax + Random.Range(10f, 90f);
            }

            petal.Rect.SetParent(_spawnArea, false);
            petal.Rect.anchorMin = new Vector2(0.5f, 0.5f);
            petal.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            petal.Rect.pivot = new Vector2(0.5f, 0.5f);
            petal.Rect.sizeDelta = new Vector2(size, size);
            petal.Rect.anchoredPosition = new Vector2(x, y);
            petal.Rect.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            petal.Rect.localScale = Vector3.one;
            petal.Size = size;

            Sprite sprite = _petalSprites[Random.Range(0, _petalSprites.Length)];
            petal.Image.sprite = sprite;
            petal.Image.preserveAspect = true;
            petal.Image.raycastTarget = false;
            petal.Image.color = _tint;

            if (petal.Group != null)
                petal.Group.alpha = Random.Range(_alphaRange.x, _alphaRange.y);

            petal.FallSpeed = Random.Range(_fallSpeedRange.x, _fallSpeedRange.y);
            float drift = Random.Range(_driftSpeedRange.x, _driftSpeedRange.y);
            petal.DriftSpeed = _preferDriftRight ? drift : -drift;
            // 일부는 반대 방향도 살짝
            if (Random.value < 0.18f)
                petal.DriftSpeed *= -0.65f;

            petal.SwayAmp = Random.Range(_swayAmplitudeRange.x, _swayAmplitudeRange.y);
            petal.SwayFreq = Random.Range(_swayFrequencyRange.x, _swayFrequencyRange.y);
            petal.SpinSpeed = Random.Range(_spinSpeedRange.x, _spinSpeedRange.y);
            if (Mathf.Abs(petal.SpinSpeed) < 8f)
                petal.SpinSpeed = 8f * Mathf.Sign(petal.SpinSpeed == 0f ? Random.value - 0.5f : petal.SpinSpeed);

            petal.TumbleFreq = Random.Range(_tumbleFrequencyRange.x, _tumbleFrequencyRange.y) * Mathf.PI * 2f;
            petal.BaseX = x;
            petal.Phase = Random.Range(0f, Mathf.PI * 2f);
            petal.TumblePhase = Random.Range(0f, Mathf.PI * 2f);
            petal.Life = prefill ? Random.Range(0f, 10f) : 0f;
        }

        private int DesiredPoolSize()
        {
            return Mathf.Clamp(_visibleCount, 1, 80) + Mathf.Max(0, _poolExtra);
        }

        private void EnsurePool()
        {
            int desired = DesiredPoolSize();
            if (_builtPoolSize >= desired && (_pool.Count > 0 || _petals.Count > 0))
                return;

            int need = desired - (_pool.Count + _petals.Count);
            for (int i = 0; i < need; i++)
            {
                var go = new GameObject(
                    "RosePetal",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                go.SetActive(false);

                var petal = new Petal
                {
                    Rect = go.transform as RectTransform,
                    Image = go.GetComponent<Image>(),
                    Group = go.GetComponent<CanvasGroup>(),
                };
                petal.Image.raycastTarget = false;
                petal.Group.blocksRaycasts = false;
                petal.Group.interactable = false;
                _pool.Enqueue(petal);
            }

            _builtPoolSize = Mathf.Max(_builtPoolSize, desired);
        }

        private Petal Rent()
        {
            if (_pool.Count == 0)
                EnsurePool();

            return _pool.Count > 0 ? _pool.Dequeue() : null;
        }

        private void Recycle(Petal petal)
        {
            if (petal?.Rect == null)
                return;

            petal.Rect.gameObject.SetActive(false);
            petal.Rect.SetParent(transform, false);
            _pool.Enqueue(petal);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _visibleCount = Mathf.Clamp(_visibleCount, 1, 80);
            if (!Application.isPlaying || !_playing)
                return;

            SyncVisibleCount();
        }
#endif
    }
}
