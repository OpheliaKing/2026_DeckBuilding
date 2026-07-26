using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 스테이지 클리어 3택1 보상 UI.
    /// </summary>
    public class StageRewardUI : UIBase
    {
        private const int RewardCount = 3;

        [SerializeField]
        private Transform _rewardObjectRoot;

        private readonly List<StageRewardObjectUI> _slotCache = new();
        private readonly List<StageRewardOffer> _offers = new();

        private Action<StageRewardOffer> _onSubmit;
        private StageRewardOffer _selectedOffer;
        private StageRewardObjectUI _selectedSlot;
        private int _spawnVersion;

        public void Setup(IReadOnlyList<StageRewardOffer> offers, Action<StageRewardOffer> onSubmit)
        {
            _onSubmit = onSubmit;
            _selectedOffer = null;
            _selectedSlot = null;
            _offers.Clear();

            if (offers != null)
            {
                for (int i = 0; i < offers.Count; i++)
                {
                    if (offers[i] != null)
                        _offers.Add(offers[i]);
                }
            }

            RefreshSlotsAsync();
        }

        public void OnClickSubmit()
        {
            if (_selectedOffer == null)
            {
                Debug.LogWarning("[StageRewardUI] 선택한 보상이 없습니다.");
                return;
            }

            Action<StageRewardOffer> callback = _onSubmit;
            _onSubmit = null;
            callback?.Invoke(_selectedOffer);
        }

        private async void RefreshSlotsAsync()
        {
            int version = ++_spawnVersion;
            HideAllSlots();

            if (_rewardObjectRoot == null)
            {
                Debug.LogError("[StageRewardUI] _rewardObjectRoot가 없습니다.");
                return;
            }

            int needCount = Mathf.Min(RewardCount, _offers.Count);
            for (int i = 0; i < needCount; i++)
            {
                if (version != _spawnVersion)
                    return;

                StageRewardObjectUI slot = await GetOrCreateSlotAsync(i);
                if (version != _spawnVersion)
                    return;

                if (slot == null)
                    continue;

                slot.gameObject.SetActive(true);
                slot.Bind(_offers[i], OnSlotClicked);
            }
        }

        private async System.Threading.Tasks.Task<StageRewardObjectUI> GetOrCreateSlotAsync(int index)
        {
            while (_slotCache.Count <= index)
            {
                StageRewardObjectUI created = await SpawnSlotAsync();
                if (created == null)
                    return null;

                _slotCache.Add(created);
            }

            return _slotCache[index];
        }

        private async System.Threading.Tasks.Task<StageRewardObjectUI> SpawnSlotAsync()
        {
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[StageRewardUI] UIManager를 찾을 수 없습니다.");
                return null;
            }

            GameObject go = await uiManager.CreateAsync(
                PublicVariable.Address.StageRewardObjectPrefab,
                _rewardObjectRoot);

            if (go == null)
            {
                Debug.LogError(
                    $"[StageRewardUI] StageRewardObject 생성 실패: {PublicVariable.Address.StageRewardObjectPrefab}");
                return null;
            }

            var slot = go.GetComponent<StageRewardObjectUI>();
            if (slot == null)
                slot = go.GetComponentInChildren<StageRewardObjectUI>(true);

            if (slot == null)
            {
                Debug.LogError("[StageRewardUI] StageRewardObjectUI 컴포넌트가 없습니다.");
                uiManager.ReleaseCreated(go);
                return null;
            }

            return slot;
        }

        private void OnSlotClicked(StageRewardObjectUI slot)
        {
            if (slot == null || slot.Offer == null)
                return;

            _selectedSlot = slot;
            _selectedOffer = slot.Offer;

            for (int i = 0; i < _slotCache.Count; i++)
            {
                if (_slotCache[i] == null)
                    continue;

                _slotCache[i].SetSelected(_slotCache[i] == slot);
            }
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < _slotCache.Count; i++)
            {
                if (_slotCache[i] != null)
                    _slotCache[i].gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            _spawnVersion++;

            var uiManager = GameManager.Instance?.UIManager;
            for (int i = 0; i < _slotCache.Count; i++)
            {
                if (_slotCache[i] == null)
                    continue;

                if (uiManager != null)
                    uiManager.ReleaseCreated(_slotCache[i].gameObject);
                else
                    Destroy(_slotCache[i].gameObject);
            }

            _slotCache.Clear();
        }
    }
}
