using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// StageNodeUI 상단 런 정보 HUD.
    /// 초상 / HP / 골드 / 스텝 진행 / 덱 장수.
    /// </summary>
    public class StageMapHudUI : MonoBehaviour
    {
        [SerializeField]
        private Image _portraitImage;

        [SerializeField]
        private TextMeshProUGUI _nameText;

        [SerializeField]
        private TextMeshProUGUI _hpText;

        [SerializeField]
        private TextMeshProUGUI _goldText;

        [SerializeField]
        private TextMeshProUGUI _stepText;

        [SerializeField]
        private TextMeshProUGUI _deckText;

        [SerializeField]
        private Image _backgroundImage;

        private int _portraitLoadVersion;
        private string _lastPortraitKey;
        private bool _fontsApplied;

        private void Awake()
        {
            ApplyFonts();
        }

        public void EnsureBuilt(Transform parent)
        {
            if (parent == null)
                return;

            if (transform.parent != parent)
                transform.SetParent(parent, false);

            ApplyRootRect();
            ApplyFonts();
            transform.SetAsLastSibling();
        }

        public void Refresh()
        {
            ApplyFonts();

            GameManager gameManager = GameManager.Instance;
            UnitInfo player = GetPrimaryPlayer(gameManager);
            int gold = gameManager != null ? gameManager.PlayerGold : 0;

            StageManager stageManager = gameManager?.StageManager;
            int step = stageManager != null ? stageManager.CurrentStepIndex : 1;
            int maxStep = stageManager != null ? stageManager.MaxStepIndex : Mathf.Max(1, step);

            if (_nameText != null)
            {
                _nameText.text = player?.UnitData != null && !string.IsNullOrEmpty(player.UnitData.unitName)
                    ? player.UnitData.unitName
                    : (player?.UnitData?.unitTid ?? "-");
            }

            if (_hpText != null)
            {
                if (player != null)
                    _hpText.text = $"HP {player.CurrentHp}/{player.MaxHp}";
                else
                    _hpText.text = "HP -/-";
            }

            if (_goldText != null)
                _goldText.text = $"Gold {gold}";

            if (_stepText != null)
                _stepText.text = $"Step {step}/{Mathf.Max(1, maxStep)}";

            if (_deckText != null)
            {
                int deckCount = player?.DeckCardList != null ? player.DeckCardList.Count : 0;
                int itemCount = player?.Items != null ? player.Items.Count : 0;
                _deckText.text = $"Deck {deckCount}  Item {itemCount}";
            }

            UpdatePortraitAsync(player);
        }

        private void ApplyRootRect()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(0f, 108f);
        }

        private void ApplyFonts()
        {
            if (_fontsApplied)
                return;

            UiFont.ApplyBody(_nameText);
            UiFont.ApplyBody(_hpText);
            UiFont.ApplyBody(_goldText);
            UiFont.ApplyBody(_stepText);
            UiFont.ApplyBody(_deckText);
            _fontsApplied = true;
        }

        private async void UpdatePortraitAsync(UnitInfo player)
        {
            if (_portraitImage == null)
                return;

            int version = ++_portraitLoadVersion;
            string iconKey = await ResolvePortraitKeyAsync(player);
            if (version != _portraitLoadVersion)
                return;

            if (string.IsNullOrEmpty(iconKey))
            {
                _lastPortraitKey = null;
                _portraitImage.enabled = false;
                return;
            }

            if (_lastPortraitKey == iconKey && _portraitImage.sprite != null)
            {
                _portraitImage.enabled = true;
                return;
            }

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                _portraitImage.enabled = false;
                return;
            }

            Sprite sprite = await resourceManager.GetAtlasSpriteAsync(ATLAS_TYPE.UI, iconKey);
            if (version != _portraitLoadVersion)
                return;

            if (sprite == null)
            {
                _portraitImage.enabled = false;
                return;
            }

            _lastPortraitKey = iconKey;
            _portraitImage.sprite = sprite;
            _portraitImage.enabled = true;
            _portraitImage.preserveAspect = true;
        }

        private static async System.Threading.Tasks.Task<string> ResolvePortraitKeyAsync(UnitInfo player)
        {
            if (player?.UnitData == null)
                return null;

            if (!string.IsNullOrEmpty(player.UnitData.unitIcon))
                return player.UnitData.unitIcon;

            string unitTid = player.UnitData.unitTid;
            if (string.IsNullOrEmpty(unitTid))
                return null;

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
                return null;

            CharacterSelectDataSO selectSO =
                await gameManager.GetSOAsync<CharacterSelectDataSO>(PublicVariable.Address.CharacterSelectDataSO);
            if (selectSO == null)
                return null;

            IReadOnlyList<CharacterSelectData> list = selectSO.CharacterSelectDatas;
            if (list == null)
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                CharacterSelectData data = list[i];
                if (data == null)
                    continue;

                if (data.UnitDataSOTid == unitTid && !string.IsNullOrEmpty(data.Icon))
                    return data.Icon;
            }

            return null;
        }

        private static UnitInfo GetPrimaryPlayer(GameManager gameManager)
        {
            if (gameManager == null)
                return null;

            IReadOnlyList<UnitInfo> players = gameManager.PlayerCharacters;
            if (players == null || players.Count == 0)
                return null;

            return players[0];
        }
    }
}
