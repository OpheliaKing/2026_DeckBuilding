#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN.EditorTools
{
    /// <summary>
    /// CharacterStatusUI 프리팹 생성 + Addressables 등록 + PlayerUI 열기 버튼 배치.
    /// </summary>
    public static class CreateCharacterStatusUIPrefab
    {
        private const string PrefabPath = "Assets/Addressables/Prefab/UI/CharacterStatusUI.prefab";
        private const string PlayerUiPath = "Assets/Addressables/Prefab/UI/PlayerUI.prefab";
        private const string PanelSpritePath = "Assets/Art/Image/UI/scarlet_option_panel.png";
        private const string CloseSpritePath = "Assets/Art/Image/UI/scarlet_inventory_close_pink.png";

        [MenuItem("SHIN/UI/Create CharacterStatusUI Prefab")]
        public static void Create()
        {
            EnsureFolder("Assets/Addressables/Prefab/UI");

            GameObject root = BuildStatusUiRoot();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            RegisterAddressable(PrefabPath);
            AddOpenButtonToPlayerUi();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CreateCharacterStatusUIPrefab] 생성 완료: {PrefabPath}");
        }

        // Unity batchmode: -executeMethod SHIN.EditorTools.CreateCharacterStatusUIPrefab.Create
        private static GameObject BuildStatusUiRoot()
        {
            var root = new GameObject("CharacterStatusUI", typeof(RectTransform), typeof(CharacterStatusUI));
            var rootRect = root.GetComponent<RectTransform>();
            StretchFull(rootRect);

            var status = root.GetComponent<CharacterStatusUI>();

            // Dim 배경 (클릭으로 닫기 가능)
            var dim = CreateUiObject("Dim", root.transform);
            StretchFull(dim.GetComponent<RectTransform>());
            var dimImage = dim.AddComponent<Image>();
            dimImage.color = new Color(0.05f, 0.02f, 0.04f, 0.55f);
            var dimButton = dim.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            // 패널 (네이티브 1536x1024)
            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
            var panel = CreateUiObject("Panel", root.transform);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = panelSprite != null
                ? new Vector2(panelSprite.rect.width, panelSprite.rect.height)
                : new Vector2(1536f, 1024f);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = panelSprite;
            panelImage.preserveAspect = true;
            panelImage.raycastTarget = true;

            // 닫기 버튼 (우상단, 패널 테두리 걸침)
            Sprite closeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CloseSpritePath);
            var closeGo = CreateUiObject("CloseButton", panel.transform);
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-70f, -70f);
            closeRect.sizeDelta = new Vector2(120f, 120f);
            var closeImage = closeGo.AddComponent<Image>();
            closeImage.sprite = closeSprite;
            closeImage.preserveAspect = true;
            var closeButton = closeGo.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;

            // 왼쪽 스탯
            var left = CreateUiObject("StatColumn", panel.transform);
            var leftRect = left.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0.48f, 1f);
            leftRect.offsetMin = new Vector2(120f, 140f);
            leftRect.offsetMax = new Vector2(-20f, -120f);
            var statText = CreateTmp("StatText", left.transform, 42f, TextAlignmentOptions.TopLeft);
            StretchFull(statText.rectTransform);
            statText.text = "atk : 10(+1)\ndef : 5\nhp : 30/40\nspd : 10\ncost : 3";
            statText.color = SoftPalette.TextPrimary;
            AssignBodyFont(statText);

            // 오른쪽 버프
            var right = CreateUiObject("BuffColumn", panel.transform);
            var rightRect = right.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.52f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.offsetMin = new Vector2(20f, 140f);
            rightRect.offsetMax = new Vector2(-120f, -120f);

            var title = CreateTmp("BuffListTitle", right.transform, 48f, TextAlignmentOptions.TopLeft);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(0f, 70f);
            title.text = "버프 리스트";
            title.color = SoftPalette.TextPrimary;
            AssignTitleFont(title);

            var buffList = CreateTmp("BuffListText", right.transform, 36f, TextAlignmentOptions.TopLeft);
            var buffRect = buffList.rectTransform;
            buffRect.anchorMin = Vector2.zero;
            buffRect.anchorMax = Vector2.one;
            buffRect.offsetMin = new Vector2(0f, 0f);
            buffRect.offsetMax = new Vector2(0f, -80f);
            buffList.text = "공격증가";
            buffList.color = SoftPalette.TextPrimary;
            AssignBodyFont(buffList);

            // SerializeField 연결
            var so = new SerializedObject(status);
            so.FindProperty("_closeButton").objectReferenceValue = closeButton;
            so.FindProperty("_dimButton").objectReferenceValue = dimButton;
            so.FindProperty("_statText").objectReferenceValue = statText;
            so.FindProperty("_buffListTitleText").objectReferenceValue = title;
            so.FindProperty("_buffListText").objectReferenceValue = buffList;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Dim 클릭 닫기는 CharacterStatusUI._dimButton 직렬화로 연결
            return root;
        }

        private static void AddOpenButtonToPlayerUi()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(PlayerUiPath);
            try
            {
                var playerUi = playerRoot.GetComponent<InGamePlayerUI>();
                if (playerUi == null)
                {
                    Debug.LogError("[CreateCharacterStatusUIPrefab] InGamePlayerUI 없음");
                    return;
                }

                Transform costBadge = FindChildByName(playerRoot.transform, "CostBadge");
                if (costBadge == null)
                {
                    Debug.LogError("[CreateCharacterStatusUIPrefab] CostBadge 없음");
                    return;
                }

                Transform existing = FindChildByName(playerRoot.transform, "CharacterStatusButton");
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                Sprite closeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CloseSpritePath);
                var buttonGo = CreateUiObject("CharacterStatusButton", costBadge.parent);
                var buttonRect = buttonGo.GetComponent<RectTransform>();
                buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0f, 0f);
                buttonRect.pivot = new Vector2(0f, 0f);
                // CostBadge(240) 바로 위
                buttonRect.anchoredPosition = new Vector2(52f, 280f);
                buttonRect.sizeDelta = new Vector2(110f, 110f);

                var bg = buttonGo.AddComponent<Image>();
                bg.sprite = closeSprite;
                bg.preserveAspect = true;
                bg.raycastTarget = true;

                var button = buttonGo.AddComponent<Button>();
                button.targetGraphic = bg;

                // 사람 아이콘 자리 (비어 있는 Image)
                var iconGo = CreateUiObject("PersonIconSlot", buttonGo.transform);
                var iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(56f, 56f);
                var iconImage = iconGo.AddComponent<Image>();
                iconImage.color = new Color(1f, 1f, 1f, 0.15f); // 자리 표시용 약한 슬롯
                iconImage.raycastTarget = false;

                var so = new SerializedObject(playerUi);
                so.FindProperty("_characterStatusButton").objectReferenceValue = button;
                so.FindProperty("_characterStatusIconSlot").objectReferenceValue = iconImage;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerUiPath);
                Debug.Log("[CreateCharacterStatusUIPrefab] PlayerUI에 CharacterStatusButton 추가");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void RegisterAddressable(string assetPath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[CreateCharacterStatusUIPrefab] Addressable settings 없음");
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return;

            var group = settings.DefaultGroup;
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            entry.SetAddress(assetPath);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMeshProUGUI CreateTmp(
            string name,
            Transform parent,
            float fontSize,
            TextAlignmentOptions align)
        {
            var go = CreateUiObject(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        private static void AssignBodyFont(TextMeshProUGUI text)
        {
            if (text == null)
                return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Addressables/Font/GowunDodum-Regular SDF.asset");
            if (font != null)
                text.font = font;
        }

        private static void AssignTitleFont(TextMeshProUGUI text)
        {
            if (text == null)
                return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Addressables/Font/GowunBatang-Bold SDF.asset");
            if (font != null)
                text.font = font;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildByName(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
