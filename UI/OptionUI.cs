using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// BGM/SE 볼륨 옵션 팝업.
    /// </summary>
    public class OptionUI : UIBase
    {
        private const string PrefsBgm = "SHIN_Volume_BGM";
        private const string PrefsSe = "SHIN_Volume_SE";

        [SerializeField]
        private Slider _bgmSlider;

        [SerializeField]
        private Slider _seSlider;

        [SerializeField]
        private Button _confirmButton;

        [SerializeField]
        private Button _cancelButton;

        private float _draftBgm = 1f;
        private float _draftSe = 1f;
        private float _savedBgm = 1f;
        private float _savedSe = 1f;
        private bool _bound;
        private bool _sePreviewQueued;
        private bool _fontsApplied;

        public override UI_TYPE UiType => UI_TYPE.Popup;

        private void OnEnable()
        {
            BindButtons();
            ApplyFonts();
            LoadFromSoundManager();
            ApplyDraftToSliders();
            ApplyDraftToSound(preview: false);
        }

        private void ApplyFonts()
        {
            if (_fontsApplied)
                return;

            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];
                if (text == null)
                    continue;

                string parent = text.transform.parent != null
                    ? text.transform.parent.name
                    : string.Empty;

                if (parent == "TitleBanner" || text.gameObject.name == "TitleText")
                    UiFont.ApplyTitle(text);
                else
                    UiFont.ApplyBody(text);
            }

            _fontsApplied = true;
        }

        private void BindButtons()
        {
            if (_bound)
                return;

            if (_bgmSlider != null)
                _bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            if (_seSlider != null)
            {
                _seSlider.onValueChanged.AddListener(OnSeChanged);
                EnsureSeSliderPointerUp(_seSlider);
            }
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnClickConfirm);
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnClickCancel);

            _bound = true;
        }

        private void EnsureSeSliderPointerUp(Slider slider)
        {
            var trigger = slider.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = slider.gameObject.AddComponent<EventTrigger>();

            // 드래그 중 매 프레임 SE가 나가지 않도록, 손을 뗄 때만 미리듣기
            AddTrigger(trigger, EventTriggerType.PointerUp, _ => PlaySePreviewIfNeeded());
            AddTrigger(trigger, EventTriggerType.EndDrag, _ => PlaySePreviewIfNeeded());
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => callback(data));
            trigger.triggers.Add(entry);
        }

        private void LoadFromSoundManager()
        {
            var sound = GameManager.Instance?.SoundManager;
            if (sound != null)
            {
                _savedBgm = sound.BgmVolume;
                _savedSe = sound.SeVolume;
            }
            else
            {
                _savedBgm = PlayerPrefs.GetFloat(PrefsBgm, 1f);
                _savedSe = PlayerPrefs.GetFloat(PrefsSe, 1f);
            }

            _draftBgm = _savedBgm;
            _draftSe = _savedSe;
        }

        private void ApplyDraftToSliders()
        {
            if (_bgmSlider != null)
            {
                _bgmSlider.minValue = 0f;
                _bgmSlider.maxValue = 1f;
                _bgmSlider.SetValueWithoutNotify(_draftBgm);
            }

            if (_seSlider != null)
            {
                _seSlider.minValue = 0f;
                _seSlider.maxValue = 1f;
                _seSlider.SetValueWithoutNotify(_draftSe);
            }
        }

        private void OnBgmChanged(float value)
        {
            _draftBgm = Mathf.Clamp01(value);
            ApplyDraftToSound(preview: true);
        }

        private void OnSeChanged(float value)
        {
            _draftSe = Mathf.Clamp01(value);
            ApplyDraftToSound(preview: true);
            _sePreviewQueued = true;
        }

        private void PlaySePreviewIfNeeded()
        {
            if (!_sePreviewQueued)
                return;

            _sePreviewQueued = false;
            var sound = GameManager.Instance?.SoundManager;
            sound?.PlaySe(PublicVariable.Address.UiButtonClickSe);
        }

        private void ApplyDraftToSound(bool preview)
        {
            var sound = GameManager.Instance?.SoundManager;
            if (sound == null)
                return;

            sound.SetBgmVolume(_draftBgm);
            sound.SetSeVolume(_draftSe);
        }

        public void OnClickConfirm()
        {
            _savedBgm = _draftBgm;
            _savedSe = _draftSe;
            PlayerPrefs.SetFloat(PrefsBgm, _savedBgm);
            PlayerPrefs.SetFloat(PrefsSe, _savedSe);
            PlayerPrefs.Save();

            var sound = GameManager.Instance?.SoundManager;
            sound?.SetBgmVolume(_savedBgm);
            sound?.SetSeVolume(_savedSe);

            CloseSelf();
        }

        public void OnClickCancel()
        {
            _draftBgm = _savedBgm;
            _draftSe = _savedSe;
            var sound = GameManager.Instance?.SoundManager;
            sound?.SetBgmVolume(_savedBgm);
            sound?.SetSeVolume(_savedSe);
            CloseSelf();
        }

        private void CloseSelf()
        {
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager != null && uiManager.Current == this)
            {
                uiManager.Close();
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
