using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SHIN
{
    /// <summary>
    /// 전투 중 월드 공간에 떠오르는 데미지 숫자.
    /// 크기는 유지하고 외곽선·그림자·대비로 가독성을 확보합니다.
    /// </summary>
    public class DamageTextPopup : MonoBehaviour
    {
        private const float BaseFontSize = 2.4f;
        private const float PulseScale = 1.12f;

        private TextMeshPro _text;
        private Material _styleMaterial;
        private float _elapsed;
        private float _duration;
        private Vector3 _startPosition;
        private Vector3 _velocity;
        private Color _baseColor;
        private bool _playing;
        private Action<DamageTextPopup> _onFinished;

        public bool IsPlaying => _playing;

        public void EnsureInitialized()
        {
            if (_text != null)
                return;

            // 비활성 오브젝트에 TMP를 붙이면 Renderer가 없어 font 설정 시 NRE가 난다.
            if (!gameObject.activeInHierarchy)
                gameObject.SetActive(true);

            _text = GetComponent<TextMeshPro>();
            if (_text == null)
                _text = gameObject.AddComponent<TextMeshPro>();

            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = BaseFontSize;
            _text.enableWordWrapping = false;
            _text.raycastTarget = false;
            _text.overflowMode = TextOverflowModes.Overflow;

            TryAssignFont(allowAsyncUpgrade: false);
            ApplyVisibilityStyle();
        }

        private void TryAssignFont(bool allowAsyncUpgrade)
        {
            if (_text == null)
                return;

            // Renderer가 없으면 font 설정 시 TMP 내부 NRE
            if (_text.renderer == null)
                return;

            if (UiFont.Body != null)
            {
                _text.font = UiFont.Body;
                return;
            }

            TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
            if (fallback == null || fallback.material == null)
                fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (fallback != null && fallback.material != null)
                _text.font = fallback;

            if (allowAsyncUpgrade)
                UiFont.ApplyBody(_text);
        }

        /// <summary>
        /// 크기 변경 없이 외곽선·언더레이·정렬로 대비를 올립니다.
        /// </summary>
        private void ApplyVisibilityStyle()
        {
            if (_text == null)
                return;

            // 밝은 본문 + 검정 외곽선이 배경과 잘 분리됨
            _text.color = Color.white;
            _text.outlineWidth = 0.35f;
            _text.outlineColor = new Color(0f, 0f, 0f, 1f);

            var renderer = _text.renderer;
            if (renderer != null)
            {
                renderer.sortingOrder = 500;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            Material source = _text.fontSharedMaterial;
            if (source == null)
                return;

            if (_styleMaterial == null || _styleMaterial.shader != source.shader)
            {
                if (_styleMaterial != null)
                    Destroy(_styleMaterial);
                _styleMaterial = new Material(source);
                _styleMaterial.name = source.name + " (DamageText)";
            }

            _styleMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            _styleMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);

            _styleMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
            _styleMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));

            _styleMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.7f));
            _styleMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.12f);
            _styleMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.14f);
            _styleMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.25f);
            _styleMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);

            _text.fontMaterial = _styleMaterial;
        }

        public void Play(
            Vector3 worldPosition,
            int damage,
            float duration,
            Action<DamageTextPopup> onFinished)
        {
            if (this == null)
                return;

            EnsureInitialized();
            if (_text == null)
                return;

            // 표시 직전에 폰트/스타일 재적용 (비활성 풀에서 생성해도 안전)
            TryAssignFont(allowAsyncUpgrade: true);
            ApplyVisibilityStyle();

            _onFinished = onFinished;
            _duration = Mathf.Max(0.2f, duration);
            _elapsed = 0f;
            _playing = true;

            _startPosition = worldPosition;
            _velocity = new Vector3(
                UnityEngine.Random.Range(-0.25f, 0.25f),
                UnityEngine.Random.Range(0.7f, 1.1f),
                UnityEngine.Random.Range(-0.1f, 0.1f));

            // 거의 흰색 + 연한 핑크 틴트 (중간 핑크보다 대비가 큼)
            _baseColor = new Color(1f, 0.88f, 0.9f, 1f);
            _text.text = damage.ToString();
            _text.color = _baseColor;
            _text.fontSize = BaseFontSize;

            transform.position = _startPosition;
            gameObject.SetActive(true);
            FaceCamera();
        }

        private void Update()
        {
            if (!_playing || this == null)
                return;

            float scale = GameManager.Instance?.TimeManager != null
                ? GameManager.Instance.TimeManager.EffectiveCharacterTimeScale
                : 1f;

            _elapsed += Time.deltaTime * Mathf.Max(0f, scale);
            float t = Mathf.Clamp01(_elapsed / _duration);

            float ease = 1f - Mathf.Pow(1f - t, 2f);
            transform.position = _startPosition + _velocity * ease;

            float sizePulse = t < 0.15f
                ? Mathf.Lerp(PulseScale, 1f, t / 0.15f)
                : 1f;
            if (_text != null)
                _text.fontSize = BaseFontSize * sizePulse;

            // 후반에만 페이드해서 중간에 더 오래 또렷하게
            Color color = _baseColor;
            color.a = t < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
            if (_text != null)
            {
                _text.color = color;
                _text.outlineColor = new Color(0f, 0f, 0f, color.a);
            }

            FaceCamera();

            if (t >= 1f)
                Finish();
        }

        private void FaceCamera()
        {
            if (this == null)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            transform.rotation = Quaternion.LookRotation(
                transform.position - cam.transform.position,
                Vector3.up);
        }

        private void Finish()
        {
            if (!_playing)
                return;

            _playing = false;

            if (this != null)
                gameObject.SetActive(false);

            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke(this);
        }

        private void OnDestroy()
        {
            _playing = false;
            _onFinished = null;

            if (_styleMaterial != null)
            {
                Destroy(_styleMaterial);
                _styleMaterial = null;
            }
        }
    }
}
