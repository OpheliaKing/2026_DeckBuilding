using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SHIN
{
    public class StageShopUI : UIBase
    {
        [SerializeField]
        private Transform _shopObjectRoot;

        private readonly List<StageShopUIObject> _slotCache = new();
        private readonly List<StageShopOffer> _offers = new();

        private System.Action<int> _onBuy;
        private System.Action _onExit;
        private int _spawnVersion;
        private bool _fontsApplied;

        private void OnEnable()
        {
            ApplyFonts();
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

                text.fontStyle = FontStyles.Normal;
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

        public void Setup(
            IReadOnlyList<StageShopOffer> offers,
            int currentGold,
            System.Action<int> onBuy,
            System.Action onExit)
        {
            ApplyFonts();
            _offers.Clear();
            if (offers != null)
            {
                for (int i = 0; i < offers.Count; i++)
                {
                    if (offers[i] != null)
                        _offers.Add(offers[i]);
                }
            }

            _onBuy = onBuy;
            _onExit = onExit;
            RefreshSlotsAsync(currentGold);
        }

        public void RefreshGold(int currentGold)
        {
            for (int i = 0; i < _slotCache.Count; i++)
            {
                StageShopUIObject slot = _slotCache[i];
                if (slot == null || !slot.gameObject.activeSelf)
                    continue;

                slot.RefreshGold(currentGold);
            }
        }

        public void RefreshOfferState(int index, int currentGold)
        {
            if (index < 0 || index >= _slotCache.Count)
                return;

            StageShopUIObject slot = _slotCache[index];
            if (slot == null)
                return;

            slot.Bind(GetOffer(index), index, currentGold, OnClickBuySlot);
        }

        public void OnClickExitShop()
        {
            System.Action callback = _onExit;
            _onExit = null;
            callback?.Invoke();
        }

        private async void RefreshSlotsAsync(int currentGold)
        {
            int version = ++_spawnVersion;
            HideAllSlots();

            if (_shopObjectRoot == null)
            {
                Debug.LogError("[StageShopUI] _shopObjectRoot가 없습니다.");
                return;
            }

            for (int i = 0; i < _offers.Count; i++)
            {
                if (version != _spawnVersion)
                    return;

                StageShopUIObject slot = await GetOrCreateSlotAsync(i);
                if (version != _spawnVersion)
                    return;

                if (slot == null)
                    continue;

                slot.gameObject.SetActive(true);
                slot.Bind(_offers[i], i, currentGold, OnClickBuySlot);
            }
        }

        private void OnClickBuySlot(int index)
        {
            _onBuy?.Invoke(index);
        }

        private StageShopOffer GetOffer(int index)
        {
            if (index < 0 || index >= _offers.Count)
                return null;

            return _offers[index];
        }

        private async System.Threading.Tasks.Task<StageShopUIObject> GetOrCreateSlotAsync(int index)
        {
            while (_slotCache.Count <= index)
            {
                StageShopUIObject created = await SpawnSlotAsync();
                if (created == null)
                    return null;

                _slotCache.Add(created);
            }

            return _slotCache[index];
        }

        private async System.Threading.Tasks.Task<StageShopUIObject> SpawnSlotAsync()
        {
            UIManager uiManager = GameManager.Instance?.UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[StageShopUI] UIManager를 찾을 수 없습니다.");
                return null;
            }

            GameObject go = await uiManager.CreateAsync(
                PublicVariable.Address.StageShopUIObjectPrefab,
                _shopObjectRoot);

            if (go == null)
            {
                Debug.LogError("[StageShopUI] StageShopUIObject 생성 실패");
                return null;
            }

            StageShopUIObject slot = go.GetComponent<StageShopUIObject>();
            if (slot == null)
                slot = go.GetComponentInChildren<StageShopUIObject>(true);

            if (slot == null)
            {
                Debug.LogError("[StageShopUI] StageShopUIObject 컴포넌트가 없습니다.");
                uiManager.ReleaseCreated(go);
                return null;
            }

            return slot;
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

            UIManager uiManager = GameManager.Instance?.UIManager;
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