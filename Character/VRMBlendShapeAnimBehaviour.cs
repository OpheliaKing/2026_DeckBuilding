using System;
using System.Collections.Generic;
using UnityEngine;
using VRM;

namespace SHIN
{
    /// <summary>
    /// Animator State에 붙여 VRMBlendShapeProxy 표정을 제어합니다.
    /// Start/End 구간에서 AnimationCurve 또는 BlendDuration(초) 중 하나로 강도를 조절합니다.
    /// 표정은 State Enter당 1회만 평가합니다(normalizedTime wrap 없음).
    /// </summary>
    public class VRMBlendShapeAnimBehaviour : StateMachineBehaviour
    {
        public enum WeightDriveMode
        {
            /// <summary>구간 로컬 시간(0~1)에 AnimationCurve 적용</summary>
            AnimationCurve = 0,

            /// <summary>Weight까지 도달/하강하는 시간(초)으로 선형 보간</summary>
            BlendDuration = 1,
        }

        [Serializable]
        public class BlendShapeTarget
        {
            [Tooltip("VRM 프리셋. 커스텀 클립은 Unknown + CustomName")]
            public BlendShapePreset Preset = BlendShapePreset.A;

            [Tooltip("Preset이 Unknown일 때 BlendShapeAvatar 클립 이름")]
            public string CustomName;

            [Range(0f, 1f)]
            [Tooltip("표정 최대 강도 (0~1)")]
            public float Weight = 1f;

            [Range(0f, 1f)]
            [Tooltip("표정을 켜기 시작하는 normalizedTime")]
            public float StartNormalizedTime = 0.1f;

            [Range(0f, 1f)]
            [Tooltip("표정을 끄는 normalizedTime (이 시각 이후 0)")]
            public float EndNormalizedTime = 0.3f;

            [Header("Weight Drive")]
            [Tooltip("AnimationCurve 또는 BlendDuration 중 하나만 사용")]
            public WeightDriveMode DriveMode = WeightDriveMode.BlendDuration;

            [Tooltip("DriveMode=AnimationCurve 일 때. X: 구간 로컬(0=Start~1=End), Y: 배율")]
            public AnimationCurve WeightCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

            [Min(0f)]
            [Tooltip("DriveMode=BlendDuration 일 때. 0 → Weight 도달까지 걸리는 시간(초)")]
            public float BlendInSeconds = 0.1f;

            [Min(0f)]
            [Tooltip("DriveMode=BlendDuration 일 때. Weight → 0 하강에 걸리는 시간(초)")]
            public float BlendOutSeconds = 0.1f;
        }

        [Header("Targets")]
        [SerializeField]
        private BlendShapeTarget[] _targets = Array.Empty<BlendShapeTarget>();

        [Header("Exit")]
        [SerializeField]
        [Tooltip("State 종료 시 지정한 표정 Weight를 0으로 되돌림")]
        private bool _resetOnExit = true;

        private VRMBlendShapeProxy _proxy;

        /// <summary>Enter당 1회 재생이 끝난 뒤(첫 사이클 종료) true.</summary>
        private bool _finishedOnce;

        /// <summary>같은 BlendShapeKey 타겟을 합칠 때 사용 (GC 줄이기용 재사용).</summary>
        private readonly Dictionary<BlendShapeKey, float> _mergedWeights = new();

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _finishedOnce = false;
            _proxy = ResolveProxy(animator);
            if (_proxy == null)
                return;

            ApplyForState(stateInfo);
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_proxy == null)
                _proxy = ResolveProxy(animator);

            if (_proxy == null)
                return;

            ApplyForState(stateInfo);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _finishedOnce = true;

            if (!_resetOnExit)
                return;

            if (_proxy == null)
                _proxy = ResolveProxy(animator);

            if (_proxy == null)
                return;

            ApplyAllZero();
        }

        private void ApplyForState(AnimatorStateInfo stateInfo)
        {
            if (_proxy == null || _targets == null)
                return;

            // Enter당 1회: wrap 없이 첫 사이클(0~1)만 사용. 이후는 0 유지.
            float rawNormalizedTime = stateInfo.normalizedTime;
            if (_finishedOnce || rawNormalizedTime >= 1f)
            {
                _finishedOnce = true;
                ApplyAllZero();
                return;
            }

            float normalizedTime = rawNormalizedTime;
            float stateLength = Mathf.Max(0.0001f, stateInfo.length);

            // 같은 Key(예: Fun 구간 2개)는 덮어쓰지 않고 Max로 합친다.
            _mergedWeights.Clear();
            for (int i = 0; i < _targets.Length; i++)
            {
                BlendShapeTarget target = _targets[i];
                if (target == null)
                    continue;

                if (!TryCreateKey(target, out BlendShapeKey key))
                    continue;

                float value = EvaluateTargetWeight(target, normalizedTime, stateLength);
                if (_mergedWeights.TryGetValue(key, out float existing))
                    _mergedWeights[key] = Mathf.Max(existing, value);
                else
                    _mergedWeights[key] = value;
            }

            foreach (KeyValuePair<BlendShapeKey, float> pair in _mergedWeights)
                _proxy.ImmediatelySetValue(pair.Key, pair.Value);
        }

        private void ApplyAllZero()
        {
            if (_proxy == null || _targets == null)
                return;

            _mergedWeights.Clear();
            for (int i = 0; i < _targets.Length; i++)
            {
                BlendShapeTarget target = _targets[i];
                if (target == null)
                    continue;

                if (!TryCreateKey(target, out BlendShapeKey key))
                    continue;

                if (_mergedWeights.ContainsKey(key))
                    continue;

                _mergedWeights[key] = 0f;
                _proxy.ImmediatelySetValue(key, 0f);
            }
        }

        private static float EvaluateTargetWeight(
            BlendShapeTarget target,
            float normalizedTime,
            float stateLengthSeconds)
        {
            GetOrderedRange(target.StartNormalizedTime, target.EndNormalizedTime, out float start, out float end);

            if (normalizedTime + 1e-4f < start || normalizedTime > end + 1e-4f)
                return 0f;

            float duration = Mathf.Max(1e-4f, end - start);
            float localT = Mathf.Clamp01((normalizedTime - start) / duration);

            float driveMul;
            switch (target.DriveMode)
            {
                case WeightDriveMode.AnimationCurve:
                    driveMul = EvaluateCurveMul(target, localT);
                    break;

                case WeightDriveMode.BlendDuration:
                    driveMul = EvaluateBlendDurationMul(target, normalizedTime, start, end, stateLengthSeconds);
                    break;

                default:
                    driveMul = 1f;
                    break;
            }

            return Mathf.Clamp01(target.Weight * driveMul);
        }

        private static float EvaluateCurveMul(BlendShapeTarget target, float localT)
        {
            if (target.WeightCurve == null || target.WeightCurve.length == 0)
                return 1f;

            return Mathf.Clamp01(target.WeightCurve.Evaluate(localT));
        }

        private static float EvaluateBlendDurationMul(
            BlendShapeTarget target,
            float normalizedTime,
            float start,
            float end,
            float stateLengthSeconds)
        {
            float elapsedNorm = normalizedTime - start;
            float remainNorm = end - normalizedTime;

            float blendInNorm = target.BlendInSeconds <= 0f
                ? 0f
                : target.BlendInSeconds / stateLengthSeconds;
            float blendOutNorm = target.BlendOutSeconds <= 0f
                ? 0f
                : target.BlendOutSeconds / stateLengthSeconds;

            float inFactor = blendInNorm <= 1e-6f
                ? 1f
                : Mathf.Clamp01(elapsedNorm / blendInNorm);

            float outFactor = blendOutNorm <= 1e-6f
                ? 1f
                : Mathf.Clamp01(remainNorm / blendOutNorm);

            return Mathf.Min(inFactor, outFactor);
        }

        private static void GetOrderedRange(float start, float end, out float s, out float e)
        {
            s = Mathf.Clamp01(start);
            e = Mathf.Clamp01(end);
            if (e < s)
            {
                float tmp = s;
                s = e;
                e = tmp;
            }
        }

        private static bool TryCreateKey(BlendShapeTarget target, out BlendShapeKey key)
        {
            if (target.Preset == BlendShapePreset.Unknown)
            {
                if (string.IsNullOrWhiteSpace(target.CustomName))
                {
                    key = default;
                    return false;
                }

                key = BlendShapeKey.CreateUnknown(target.CustomName);
                return true;
            }

            key = BlendShapeKey.CreateFromPreset(target.Preset);
            return true;
        }

        private static VRMBlendShapeProxy ResolveProxy(Animator animator)
        {
            if (animator == null)
                return null;

            VRMBlendShapeProxy proxy = animator.GetComponentInParent<VRMBlendShapeProxy>();
            if (proxy == null)
                proxy = animator.GetComponentInChildren<VRMBlendShapeProxy>(true);

            if (proxy == null)
                Debug.LogWarning("[VRMBlendShapeAnim] VRMBlendShapeProxy를 찾을 수 없습니다.", animator);

            return proxy;
        }
    }
}
