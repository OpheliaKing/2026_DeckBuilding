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

        public void EnsureBuilt(Transform parent)
        {
            if (parent == null)
                return;

            if (transform.parent != parent)
                transform.SetParent(parent, false);

            EnsureLayout();
            transform.SetAsLastSibling();
        }

        public void Refresh()
        {
            EnsureLayout();

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

        private void EnsureLayout()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
                root = gameObject.AddComponent<RectTransform>();

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(0f, 96f);
            root.offsetMin = new Vector2(0f, root.offsetMin.y);
            root.offsetMax = new Vector2(0f, root.offsetMax.y);

            if (_backgroundImage == null)
            {
                _backgroundImage = GetComponent<Image>();
                if (_backgroundImage == null)
                    _backgroundImage = gameObject.AddComponent<Image>();
                _backgroundImage.color = new Color(1f, 0.92f, 0.95f, 0.78f);
                _backgroundImage.raycastTarget = false;
            }

            HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(20, 20, 12, 12);
                layout.spacing = 18f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
            }

            if (_portraitImage == null)
            {
                var portraitGo = CreateChild("Portrait", out RectTransform portraitRect);
                portraitRect.sizeDelta = new Vector2(72f, 72f);
                LayoutElement portraitLayout = portraitGo.AddComponent<LayoutElement>();
                portraitLayout.preferredWidth = 72f;
                portraitLayout.preferredHeight = 72f;
                portraitLayout.minWidth = 72f;
                portraitLayout.minHeight = 72f;
                _portraitImage = portraitGo.AddComponent<Image>();
                _portraitImage.color = Color.white;
                _portraitImage.preserveAspect = true;
                _portraitImage.enabled = false;
            }

            if (_nameText == null || _hpText == null)
            {
                var infoGo = CreateChild("PlayerInfo", out RectTransform infoRect);
                infoRect.sizeDelta = new Vector2(180f, 72f);
                LayoutElement infoLayout = infoGo.AddComponent<LayoutElement>();
                infoLayout.preferredWidth = 180f;
                infoLayout.minWidth = 140f;

                VerticalLayoutGroup infoGroup = infoGo.AddComponent<VerticalLayoutGroup>();
                infoGroup.childAlignment = TextAnchor.MiddleLeft;
                infoGroup.childControlHeight = true;
                infoGroup.childControlWidth = true;
                infoGroup.childForceExpandHeight = false;
                infoGroup.childForceExpandWidth = true;
                infoGroup.spacing = 2f;

                if (_nameText == null)
                    _nameText = CreateText(infoGo.transform, "NameText", 22f, TextAlignmentOptions.MidlineLeft);

                if (_hpText == null)
                    _hpText = CreateText(infoGo.transform, "HpText", 18f, TextAlignmentOptions.MidlineLeft);
            }

            if (_goldText == null)
            {
                _goldText = CreateText(transform, "GoldText", 22f, TextAlignmentOptions.MidlineLeft);
                LayoutElement goldLayout = _goldText.gameObject.AddComponent<LayoutElement>();
                goldLayout.preferredWidth = 140f;
                goldLayout.minWidth = 120f;
            }

            if (_stepText == null)
            {
                _stepText = CreateText(transform, "StepText", 22f, TextAlignmentOptions.MidlineLeft);
                LayoutElement stepLayout = _stepText.gameObject.AddComponent<LayoutElement>();
                stepLayout.preferredWidth = 140f;
                stepLayout.minWidth = 120f;
            }

            if (_deckText == null)
            {
                _deckText = CreateText(transform, "DeckText", 20f, TextAlignmentOptions.MidlineLeft);
                LayoutElement deckLayout = _deckText.gameObject.AddComponent<LayoutElement>();
                deckLayout.preferredWidth = 180f;
                deckLayout.minWidth = 140f;
                deckLayout.flexibleWidth = 1f;
            }
        }

        private GameObject CreateChild(string name, out RectTransform rect)
        {
            Transform existing = transform.Find(name);
            if (existing != null)
            {
                rect = existing as RectTransform;
                if (rect == null)
                    rect = existing.gameObject.AddComponent<RectTransform>();
                return existing.gameObject;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            rect = go.GetComponent<RectTransform>();
            return go;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                TextMeshProUGUI existingText = existing.GetComponent<TextMeshProUGUI>();
                if (existingText != null)
                    return existingText;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.42f, 0.28f, 0.36f, 1f);
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
            return text;
        }
    }
}
