using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 캐릭터 선택 UI. 슬롯 클릭은 미리보기, 확정 버튼으로 선택 데이터를 부모에 전달한다.
    /// </summary>
    public class CharacterSelectUI : UIBase
    {
        [SerializeField]
        private Transform _characterButtonRoot;

        [SerializeField]
        private Button _confirmButton;

        private readonly List<CharacterSelectSlotUI> _slots = new();
        private Action<CharacterSelectData> _onConfirmed;
        private Action<CharacterSelectData> _onPreviewChanged;
        private CharacterSelectData _selectedData;
        private int _setupVersion;

        public CharacterSelectData SelectedData => _selectedData;

        /// <summary>
        /// 캐릭터 리스트로 슬롯을 생성한다.
        /// onConfirmed: 확정 버튼 시 호출.
        /// onPreviewChanged: 슬롯 클릭(미리보기) 시 호출. null 가능.
        /// </summary>
        public void Setup(
            IReadOnlyList<CharacterSelectData> characterList,
            Action<CharacterSelectData> onConfirmed,
            Action<CharacterSelectData> onPreviewChanged = null)
        {
            _onConfirmed = onConfirmed;
            _onPreviewChanged = onPreviewChanged;
            ClearSlots();
            BindConfirmButton();

            if (characterList == null || characterList.Count == 0)
            {
                Debug.LogWarning("[CharacterSelectUI] 캐릭터 리스트가 비어 있습니다.");
                return;
            }

            if (_characterButtonRoot == null)
            {
                Debug.LogError("[CharacterSelectUI] _characterButtonRoot가 없습니다.");
                return;
            }

            SetupSlotsAsync(characterList);
        }

        public void SetSelected(CharacterSelectData data)
        {
            _selectedData = data;
            RefreshSelection();
        }

        /// <summary>
        /// Inspector 확정 버튼에서 연결할 수 있다.
        /// </summary>
        public void OnClickConfirm()
        {
            if (_selectedData == null)
            {
                Debug.LogWarning("[CharacterSelectUI] 선택된 캐릭터가 없습니다.");
                return;
            }

            _onConfirmed?.Invoke(_selectedData);
        }

        private void BindConfirmButton()
        {
            if (_confirmButton == null)
                _confirmButton = FindConfirmButton();

            if (_confirmButton == null)
                return;

            _confirmButton.onClick.RemoveListener(OnClickConfirm);
            _confirmButton.onClick.AddListener(OnClickConfirm);
        }

        private Button FindConfirmButton()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.name == "SelectButton")
                    return buttons[i];
            }

            return null;
        }

        private async void SetupSlotsAsync(IReadOnlyList<CharacterSelectData> characterList)
        {
            int version = ++_setupVersion;
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[CharacterSelectUI] UIManager를 찾을 수 없습니다.");
                return;
            }

            for (int i = 0; i < characterList.Count; i++)
            {
                if (version != _setupVersion)
                    return;

                CharacterSelectData data = characterList[i];
                if (data == null)
                    continue;

                GameObject slotObject = await uiManager.CreateAsync(
                    PublicVariable.Address.CharacterSelectButtonPrefab,
                    _characterButtonRoot);

                if (version != _setupVersion)
                {
                    if (slotObject != null)
                        uiManager.ReleaseCreated(slotObject);
                    return;
                }

                if (slotObject == null)
                {
                    Debug.LogError(
                        $"[CharacterSelectUI] 슬롯 생성 실패: {PublicVariable.Address.CharacterSelectButtonPrefab}");
                    continue;
                }

                var slot = slotObject.GetComponent<CharacterSelectSlotUI>();
                if (slot == null)
                    slot = slotObject.GetComponentInChildren<CharacterSelectSlotUI>(true);

                if (slot == null)
                {
                    Debug.LogError("[CharacterSelectUI] CharacterSelectSlotUI 컴포넌트가 없습니다.");
                    uiManager.ReleaseCreated(slotObject);
                    continue;
                }

                slot.gameObject.SetActive(true);
                slot.Bind(data, HandleSlotClicked, isSelected: false);
                _slots.Add(slot);
            }

            if (version != _setupVersion)
                return;

            if (_selectedData == null && characterList.Count > 0)
                _selectedData = characterList[0];

            RefreshSelection();

            if (_selectedData != null)
                _onPreviewChanged?.Invoke(_selectedData);
        }

        private void HandleSlotClicked(CharacterSelectData data)
        {
            if (data == null)
                return;

            _selectedData = data;
            RefreshSelection();
            _onPreviewChanged?.Invoke(data);
        }

        private void RefreshSelection()
        {
            string selectedPath = _selectedData?.PrefabPath;
            string selectedTid = _selectedData?.Tid;

            for (int i = 0; i < _slots.Count; i++)
            {
                CharacterSelectSlotUI slot = _slots[i];
                if (slot?.Data == null)
                    continue;

                bool selected;
                if (!string.IsNullOrEmpty(selectedPath))
                    selected = slot.Data.PrefabPath == selectedPath;
                else
                    selected = !string.IsNullOrEmpty(selectedTid) && slot.Data.Tid == selectedTid;

                slot.SetSelected(selected);
            }
        }

        private void ClearSlots()
        {
            _setupVersion++;

            var uiManager = GameManager.Instance?.UIManager;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == null)
                    continue;

                if (uiManager != null)
                    uiManager.ReleaseCreated(_slots[i].gameObject);
                else
                    Destroy(_slots[i].gameObject);
            }

            _slots.Clear();

            if (_characterButtonRoot == null)
                return;

            for (int i = _characterButtonRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = _characterButtonRoot.GetChild(i).gameObject;
                if (uiManager != null)
                    uiManager.ReleaseCreated(child);
                else
                    Destroy(child);
            }
        }

        private void OnDestroy()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(OnClickConfirm);

            ClearSlots();
        }
    }
}
