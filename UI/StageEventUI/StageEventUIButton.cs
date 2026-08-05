using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    public class StageEventUIButton : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _buttonText;

        [SerializeField]
        private Button _button;

        private int _choiceIndex;
        private Action<int> _onClicked;

        public void Bind(string buttonText, int choiceIndex, Action<int> onClicked)
        {
            _choiceIndex = choiceIndex;
            _onClicked = onClicked;

            if (_buttonText != null)
            {
                _buttonText.text = buttonText ?? string.Empty;
                UiFont.ApplyBody(_buttonText);
            }

            if (_button == null)
                _button = GetComponent<Button>();

            if (_button == null)
            {
                Debug.LogError("[StageEventUIButton] Button 컴포넌트가 없습니다.");
                return;
            }

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(HandleClick);
            _button.interactable = true;
        }

        public void SetInteractable(bool interactable)
        {
            if (_button == null)
                _button = GetComponent<Button>();

            if (_button != null)
                _button.interactable = interactable;
        }

        private void HandleClick()
        {
            _onClicked?.Invoke(_choiceIndex);
        }
    }
}
