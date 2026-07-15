using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// lilToon Dissolve만 사용합니다.
    /// Cutout/Transparent (LIL_RENDER != 0) 에서만 구멍 디졸브가 동작합니다.
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

        private static Texture2D _fallbackNoise;

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

            // Opaque 패스(LIL_RENDER==0)는 Dissolve 코드가 스킵됨
            bool looksOpaque =
                shaderName.IndexOf("Opaque", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                shaderName.IndexOf("Cutout", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                shaderName.IndexOf("Transparent", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                shaderName != "lilToon"; // 기본 lilToon은 Opaque일 수 있음

            if (looksOpaque || shaderName == "lilToon" || shaderName == "Hidden/lilToon")
            {
                // Cutout 셰이더로 전환 시도 (Dissolve 필수)
                var cutout = Shader.Find("Hidden/lilToonCutout");
                if (cutout != null)
                    material.shader = cutout;
                else
                {
                    Debug.LogWarning($"[Dissolve] Cutout lilToon이 필요합니다: {material.name} / {shaderName}");
                    return false;
                }
            }

            var noise = noiseOverride != null ? noiseOverride : GetFallbackNoise();
            if (noise != null)
            {
                // Mask/Noise가 비면 Alpha 모드에서 통째로 잘리거나 패턴이 없음
                if (material.HasProperty(DissolveMaskId))
                    material.SetTexture(DissolveMaskId, noise);
                if (material.HasProperty(DissolveNoiseMaskId))
                    material.SetTexture(DissolveNoiseMaskId, noise);
            }

            if (material.HasProperty(DissolveNoiseStrengthId))
                material.SetFloat(DissolveNoiseStrengthId, Mathf.Lerp(0.25f, 0.95f, Mathf.Clamp01(noiseStrength)));

            if (material.HasProperty(DissolveColorId))
                material.SetColor(DissolveColorId, edgeColor);

            // Mode=1(Alpha), Shape=0, Border=-1(완전 유지), Blur
            material.SetVector(DissolveParamsId, new Vector4(1f, 0f, -1f, 0.12f));

            // Cutout이 알파를 너무 세게 자르지 않도록
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
            // Border -1 → 1.35 (노이즈 포함 완전 소멸)
            float border = Mathf.Lerp(-1f, 1.35f, amount01);
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
                material.SetVector(DissolveParamsId, new Vector4(1f, 0f, 1.5f, 0.05f));
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
