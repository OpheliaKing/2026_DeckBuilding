using System;
using UnityEngine;

namespace SHIN
{
    public enum ANIMATOR_SOUND_KIND
    {
        SE = 0,
        VOICE = 1,
    }

    /// <summary>
    /// Animator State normalizedTime 기준으로 SE/VOICE를 재생한다.
    /// SE: Addressables 전체 경로.
    /// VOICE: Assets/Addressables/Sound/Voice/{UnitTid}/{파일명}
    /// </summary>
    public class AnimatorSoundBehaviour : StateMachineBehaviour
    {
        [Serializable]
        public class SoundCue
        {
            [Tooltip("SE=Addressables 전체 경로 / VOICE=파일명(확장자 포함 권장)")]
            public string SoundKey;

            public ANIMATOR_SOUND_KIND Kind = ANIMATOR_SOUND_KIND.SE;

            [Range(0f, 1f)]
            [Tooltip("이 normalizedTime 이상이면 1회 재생")]
            public float NormalizedTime = 0.1f;
        }

        [SerializeField]
        private SoundCue[] _cues = Array.Empty<SoundCue>();

        private bool[] _played;
        private string _voiceUnitTid;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            EnsurePlayedBuffer();
            ResetPlayedFlags();
            _voiceUnitTid = ResolveVoiceUnitTid(animator);
            TryPlayCues(stateInfo.normalizedTime);
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // Enter당 1사이클만. wrap으로 재발화하지 않음.
            if (stateInfo.normalizedTime >= 1f)
                return;

            TryPlayCues(stateInfo.normalizedTime);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            ResetPlayedFlags();
            _voiceUnitTid = null;
        }

        private void TryPlayCues(float normalizedTime)
        {
            if (_cues == null || _cues.Length == 0)
                return;

            EnsurePlayedBuffer();

            float t = Mathf.Clamp01(normalizedTime);
            for (int i = 0; i < _cues.Length; i++)
            {
                if (_played[i])
                    continue;

                SoundCue cue = _cues[i];
                if (cue == null || string.IsNullOrEmpty(cue.SoundKey))
                {
                    _played[i] = true;
                    continue;
                }

                if (t + 1e-4f < cue.NormalizedTime)
                    continue;

                _played[i] = true;
                PlayCue(cue);
            }
        }

        private void PlayCue(SoundCue cue)
        {
            var soundManager = GameManager.Instance?.SoundManager;
            if (soundManager == null)
            {
                Debug.LogWarning("[AnimatorSoundBehaviour] SoundManager가 없습니다.");
                return;
            }

            switch (cue.Kind)
            {
                case ANIMATOR_SOUND_KIND.SE:
                    soundManager.PlaySe(cue.SoundKey);
                    break;

                case ANIMATOR_SOUND_KIND.VOICE:
                {
                    if (string.IsNullOrEmpty(_voiceUnitTid))
                    {
                        Debug.LogWarning(
                            $"[AnimatorSoundBehaviour] VOICE UnitTid를 찾지 못했습니다. key={cue.SoundKey}");
                        return;
                    }

                    string path = BuildVoicePath(_voiceUnitTid, cue.SoundKey);
                    soundManager.PlayVoice(path);
                    break;
                }
            }
        }

        private void EnsurePlayedBuffer()
        {
            int count = _cues != null ? _cues.Length : 0;
            if (_played == null || _played.Length != count)
                _played = new bool[count];
        }

        private void ResetPlayedFlags()
        {
            if (_played == null)
                return;

            for (int i = 0; i < _played.Length; i++)
                _played[i] = false;
        }

        /// <summary>
        /// 전투: CharacterBase → UnitData.unitTid
        /// 선택화면: CharacterSelectModel → CharacterSelectData.UnitDataSOTid
        /// (둘 다 동일 Unit Tid를 가리키는 전제)
        /// </summary>
        private static string ResolveVoiceUnitTid(Animator animator)
        {
            if (animator == null)
                return null;

            CharacterBase character = animator.GetComponentInParent<CharacterBase>();
            if (character != null)
            {
                string tid = character.UnitTid;
                if (!string.IsNullOrEmpty(tid))
                    return tid;
            }

            CharacterSelectModel selectModel = animator.GetComponentInParent<CharacterSelectModel>();
            if (selectModel != null)
            {
                string tid = selectModel.UnitTid;
                if (!string.IsNullOrEmpty(tid))
                    return tid;
            }

            return null;
        }

        private static string BuildVoicePath(string unitTid, string voiceFileName)
        {
            return $"{PublicVariable.Address.VoiceRoot}{unitTid}/{voiceFileName}";
        }
    }
}
