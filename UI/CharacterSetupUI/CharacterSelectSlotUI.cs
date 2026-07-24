using System;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 캐릭터 선택 리스트 슬롯 UI.
    /// </summary>
    public class CharacterSelectSlotUI : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private Text _nameText;

        [SerializeField]
        private Image _iconImage;

        [SerializeField]
        private GameObject _selectedMark;

        private CharacterSelectData _data;
        private Action<CharacterSelectData> _onClick;
        private int _iconLoadVersion;

        public string Tid => _data?.Tid;
        public CharacterSelectData Data => _data;

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();
        }

        public void Bind(CharacterSelectData data, Action<CharacterSelectData> onClick, bool isSelected)
        {
            _data = data;
            _onClick = onClick;

            if (_button == null)
                _button = GetComponent<Button>();
            if (_button == null)
                _button = GetComponentInChildren<Button>(true);

            if (_nameText != null)
                _nameText.text = data != null ? data.Name : string.Empty;

            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
                _button.onClick.AddListener(HandleClick);
            }
            else
            {
                Debug.LogError("[CharacterSelectSlotUI] Button이 없습니다.");
            }

            SetSelected(isSelected);
            UpdateIconAsync();
        }

        public void SetSelected(bool selected)
        {
            if (_selectedMark != null)
                _selectedMark.SetActive(selected);
        }

        private void HandleClick()
        {
            if (_data == null)
                return;

            _onClick?.Invoke(_data);
        }

        private async void UpdateIconAsync()
        {
            if (_iconImage == null || _data == null || string.IsNullOrEmpty(_data.Icon))
                return;

            int version = ++_iconLoadVersion;
            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
                return;

            Sprite sprite = await resourceManager.GetAtlasSpriteAsync(ATLAS_TYPE.UI, _data.Icon);
            if (version != _iconLoadVersion || sprite == null || _iconImage == null)
                return;

            _iconImage.sprite = sprite;
        }
    }
}
