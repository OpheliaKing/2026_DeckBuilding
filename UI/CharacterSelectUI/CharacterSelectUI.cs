using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 캐릭터 선택 UI. CharacterSelectObject로부터 리스트를 받아 슬롯을 생성한다.
    /// </summary>
    public class CharacterSelectUI : UIBase
    {
        [SerializeField]
        private Transform _characterButtonRoot;

        private readonly List<CharacterSelectSlotUI> _slots = new();
        private Action<CharacterSelectData> _onSlotSelected;
        private CharacterSelectData _selectedData;
        private int _setupVersion;

        /// <summary>
        /// 캐릭터 리스트로 슬롯을 생성하고 클릭 콜백을 연결한다.
        /// </summary>
        public void Setup(IReadOnlyList<CharacterSelectData> characterList, Action<CharacterSelectData> onSlotSelected)
        {
            _onSlotSelected = onSlotSelected;
            ClearSlots();

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

            if (version == _setupVersion)
                RefreshSelection();
        }

        private void HandleSlotClicked(CharacterSelectData data)
        {
            if (data == null)
                return;

            _selectedData = data;
            RefreshSelection();
            _onSlotSelected?.Invoke(data);
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
            ClearSlots();
        }
    }
}
