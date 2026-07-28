using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    public class StageEventUI : UIBase
    {
        private const string ContinueButtonText = "확인";

        [SerializeField]
        private TextMeshProUGUI _eventTitleText;

        [SerializeField]
        private Image _eventImage;

        [SerializeField]
        private TextMeshProUGUI _eventDescriptionText;

        [SerializeField]
        private Transform _eventButtonRoot;

        private readonly List<StageEventUIButton> _buttonCache = new();

        private StageEventData _eventData;
        private Action<int> _onChoiceSelected;
        private Action _onContinue;
        private int _spawnVersion;
        private int _imageLoadVersion;
        private bool _choiceLocked;

        public void Setup(StageEventData eventData, Action<int> onChoiceSelected)
        {
            _eventData = eventData;
            _onChoiceSelected = onChoiceSelected;
            _onContinue = null;
            _choiceLocked = false;

            RefreshHeader();
            RefreshButtonsAsync();
            RefreshEventImageAsync();
        }

        /// <summary>
        /// 선택 결과 문구를 표시하고 선택 버튼을 비활성화한 뒤, 확인 버튼으로 이어간다.
        /// </summary>
        public void ShowResult(string resultText, Action onContinue)
        {
            _choiceLocked = true;
            _onChoiceSelected = null;
            _onContinue = onContinue;

            if (_eventDescriptionText != null)
                _eventDescriptionText.text = resultText ?? string.Empty;

            SetChoiceButtonsInteractable(false);
            ShowContinueButtonAsync();
        }

        private void RefreshHeader()
        {
            if (_eventData == null)
            {
                if (_eventTitleText != null)
                    _eventTitleText.text = string.Empty;
                if (_eventDescriptionText != null)
                    _eventDescriptionText.text = string.Empty;
                return;
            }

            if (_eventTitleText != null)
                _eventTitleText.text = _eventData.EventName ?? string.Empty;

            if (_eventDescriptionText != null)
                _eventDescriptionText.text = _eventData.EventDescription ?? string.Empty;
        }

        private async void RefreshButtonsAsync()
        {
            int version = ++_spawnVersion;
            HideAllButtons();

            if (_eventButtonRoot == null)
            {
                Debug.LogError("[StageEventUI] _eventButtonRoot가 없습니다.");
                return;
            }

            if (_eventData?.Choices == null || _eventData.Choices.Count == 0)
                return;

            for (int i = 0; i < _eventData.Choices.Count; i++)
            {
                if (version != _spawnVersion)
                    return;

                StageEventChoice choice = _eventData.Choices[i];
                StageEventUIButton button = await GetOrCreateButtonAsync(i);
                if (version != _spawnVersion)
                    return;

                if (button == null)
                    continue;

                button.gameObject.SetActive(true);
                button.Bind(choice != null ? choice.ButtonText : string.Empty, i, OnChoiceButtonClicked);
            }
        }

        private async void ShowContinueButtonAsync()
        {
            int version = ++_spawnVersion;
            HideAllButtons();

            if (_eventButtonRoot == null)
                return;

            StageEventUIButton button = await GetOrCreateButtonAsync(0);
            if (version != _spawnVersion || button == null)
                return;

            button.gameObject.SetActive(true);
            button.Bind(ContinueButtonText, 0, _ => OnContinueClicked());
        }

        private void OnChoiceButtonClicked(int choiceIndex)
        {
            if (_choiceLocked)
                return;

            _choiceLocked = true;
            SetChoiceButtonsInteractable(false);

            Action<int> callback = _onChoiceSelected;
            _onChoiceSelected = null;
            callback?.Invoke(choiceIndex);
        }

        private void OnContinueClicked()
        {
            Action callback = _onContinue;
            _onContinue = null;
            callback?.Invoke();
        }

        private void SetChoiceButtonsInteractable(bool interactable)
        {
            for (int i = 0; i < _buttonCache.Count; i++)
            {
                if (_buttonCache[i] != null)
                    _buttonCache[i].SetInteractable(interactable);
            }
        }

        private async System.Threading.Tasks.Task<StageEventUIButton> GetOrCreateButtonAsync(int index)
        {
            while (_buttonCache.Count <= index)
            {
                StageEventUIButton created = await SpawnButtonAsync();
                if (created == null)
                    return null;

                _buttonCache.Add(created);
            }

            return _buttonCache[index];
        }

        private async System.Threading.Tasks.Task<StageEventUIButton> SpawnButtonAsync()
        {
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[StageEventUI] UIManager를 찾을 수 없습니다.");
                return null;
            }

            GameObject go = await uiManager.CreateAsync(
                PublicVariable.Address.StageEventUIButtonPrefab,
                _eventButtonRoot);

            if (go == null)
            {
                Debug.LogError(
                    $"[StageEventUI] StageEventUIButton 생성 실패: {PublicVariable.Address.StageEventUIButtonPrefab}");
                return null;
            }

            var button = go.GetComponent<StageEventUIButton>();
            if (button == null)
                button = go.GetComponentInChildren<StageEventUIButton>(true);

            if (button == null)
            {
                Debug.LogError("[StageEventUI] StageEventUIButton 컴포넌트가 없습니다.");
                uiManager.ReleaseCreated(go);
                return null;
            }

            return button;
        }

        private async void RefreshEventImageAsync()
        {
            if (_eventImage == null)
                return;

            int version = ++_imageLoadVersion;

            if (_eventData == null || string.IsNullOrEmpty(_eventData.EventImagePath))
            {
                _eventImage.enabled = false;
                return;
            }

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[StageEventUI] ResourceManager를 찾을 수 없습니다.");
                return;
            }

            Sprite sprite = await resourceManager.GetAtlasSpriteAsync(
                ATLAS_TYPE.UI,
                _eventData.EventImagePath);

            if (version != _imageLoadVersion)
                return;

            if (sprite == null)
            {
                _eventImage.enabled = false;
                return;
            }

            _eventImage.sprite = sprite;
            _eventImage.enabled = true;
        }

        private void HideAllButtons()
        {
            for (int i = 0; i < _buttonCache.Count; i++)
            {
                if (_buttonCache[i] != null)
                    _buttonCache[i].gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            _spawnVersion++;
            _imageLoadVersion++;

            var uiManager = GameManager.Instance?.UIManager;
            for (int i = 0; i < _buttonCache.Count; i++)
            {
                if (_buttonCache[i] == null)
                    continue;

                if (uiManager != null)
                    uiManager.ReleaseCreated(_buttonCache[i].gameObject);
                else
                    Destroy(_buttonCache[i].gameObject);
            }

            _buttonCache.Clear();
        }
    }
}
