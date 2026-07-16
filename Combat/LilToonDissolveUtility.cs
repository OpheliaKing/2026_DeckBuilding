using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// lilToon Dissolve만 사용합니다.
    /// Opaque(LIL_RENDER==0)에서는 디졸브가 무시되므로 Cutout으로 전환합니다.
    /// </summary>
    public static class LilToonDissolveUtility
    {
        public static readonly int DissolveParamsId = Shader.PropertyToID("_DissolveParams");
        public static readonly int DissolveColorId = Shader.PropertyToID("_DissolveColor");
        public static readonly int DissolveNoiseStrengthId = Shader.PropertyToID("_DissolveNoiseStrength");
        public static readonly int DissolveNoiseMaskId = Shader.PropertyToID("_DissolveNoiseMask");
        public static readonly int DissolveMaskId = Shader.PropertyToID("_DissolveMask");
        public static readonly int InvisibleId = Shader.PropertyToID("_Invisible");
        public static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        public static readonly int IdMaskControlsDissolveId = Shader.PropertyToID("_IDMaskControlsDissolve");

        private static Texture2D _fallbackNoise;
        private static Shader _cachedCutout;
        private static Shader _cachedTransparent;

        public static bool IsLilToonMaterial(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            string n = material.shader.name;
            return n == "lilToon" ||
                   n.StartsWith("Hidden/lilToon") ||
                   n.Contains("lilToon");
        }

        public static bool PrepareForDissolve(Material material, Color edgeColor, float noiseStrength, Texture noiseOverride)
        {
            if (!IsLilToonMaterial(material) || !material.HasProperty(DissolveParamsId))
                return false;

            string shaderName = material.shader.name;
            if (shaderName == "Unlit/Color" || shaderName.Contains("Hidden/Internal"))
                return false;

            if (!IsAlphaCapableLilToon(shaderName))
            {
                var cutout = GetLilShader("Hidden/lilToonCutout", ref _cachedCutout);
                if (cutout != null)
                {
                    material.shader = cutout;
                }
                else
                {
                    var transparent = GetLilShader("Hidden/lilToonTransparent", ref _cachedTransparent);
                    if (transparent == null)
                        transparent = GetLilShader("Hidden/lilToonOnePassTransparent", ref _cachedTransparent);

                    if (transparent == null)
                    {
                        Debug.LogWarning($"[Dissolve] Cutout/Transparent lilToon 필요: {material.name} / {shaderName}");
                        return false;
                    }

                    material.shader = transparent;
                }
            }

            var noise = noiseOverride != null ? noiseOverride : GetFallbackNoise();
            if (noise != null)
            {
                // Mask가 white면 Border를 올려도 구멍이 거의 안 남 → 반드시 노이즈 텍스처 지정
                if (material.HasProperty(DissolveMaskId))
                    material.SetTexture(DissolveMaskId, noise);
                if (material.HasProperty(DissolveNoiseMaskId))
                    material.SetTexture(DissolveNoiseMaskId, noise);
            }

            if (material.HasProperty(DissolveNoiseStrengthId))
                material.SetFloat(DissolveNoiseStrengthId, Mathf.Lerp(0.35f, 1.0f, Mathf.Clamp01(noiseStrength)));

            if (material.HasProperty(DissolveColorId))
                material.SetColor(DissolveColorId, edgeColor);

            if (material.HasProperty(IdMaskControlsDissolveId))
                material.SetFloat(IdMaskControlsDissolveId, 0f);

            // Mode=1(Alpha), Shape=0, Border=-1(완전 유지), Blur
            material.SetVector(DissolveParamsId, new Vector4(1f, 0f, -1f, 0.15f));

            if (material.HasProperty(CutoffId))
                material.SetFloat(CutoffId, 0.01f);

            if (material.HasProperty(InvisibleId))
                material.SetFloat(InvisibleId, 0f);

            return true;
        }

        public static void SetDissolveAmount(Material material, float amount01, float blur = 0.2f)
        {
            if (material == null || !material.HasProperty(DissolveParamsId))
                return;

            amount01 = Mathf.Clamp01(amount01);
            // Border -1 → 2 (완전 소멸). 1.35는 마스크/노이즈 조합에 따라 덜 녹을 수 있음
            float border = Mathf.Lerp(-1f, 2f, amount01);
            float w = Mathf.Clamp(blur, 0.05f, 0.45f);
            material.SetVector(DissolveParamsId, new Vector4(1f, 0f, border, w));
        }

        public static void SetInvisible(Material material, bool invisible)
        {
            if (material == null)
                return;

            if (material.HasProperty(InvisibleId))
                material.SetFloat(InvisibleId, invisible ? 1f : 0f);
            else if (invisible && material.HasProperty(DissolveParamsId))
                material.SetVector(DissolveParamsId, new Vector4(1f, 0f, 2f, 0.05f));
        }

        private static bool IsAlphaCapableLilToon(string shaderName)
        {
            return shaderName.IndexOf("Cutout", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   shaderName.IndexOf("Transparent", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Shader GetLilShader(string shaderName, ref Shader cache)
        {
            if (cache != null)
                return cache;

            cache = Shader.Find(shaderName);
            if (cache != null)
                return cache;

            // Hidden 셰이더는 Shader.Find가 null을 주는 경우가 있어 로드된 셰이더에서 재탐색
            var all = Resources.FindObjectsOfTypeAll<Shader>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == shaderName)
                {
                    cache = all[i];
                    return cache;
                }
            }

            return null;
        }

        private static Texture2D GetFallbackNoise()
        {
            if (_fallbackNoise != null)
                return _fallbackNoise;

            const int size = 128;
            _fallbackNoise = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                name = "LilToonDissolveNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n =
                    Mathf.PerlinNoise(x * 0.07f + 2.3f, y * 0.07f + 5.1f) * 0.55f +
                    Mathf.PerlinNoise(x * 0.23f + 8.4f, y * 0.23f + 1.6f) * 0.45f;
                pixels[y * size + x] = new Color(n, n, n, 1f);
            }

            _fallbackNoise.SetPixels(pixels);
            _fallbackNoise.Apply(false, false);
            return _fallbackNoise;
        }
    }
}
