using System.Collections.Generic;
using System.Threading.Tasks;
using Michsky.UI.Heat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    public class InGamePlayerUI : MonoBehaviour
    {
        private static readonly Color TextCream = new Color(0.98f, 0.93f, 0.86f, 1f);
        private static readonly Color AccentScarlet = new Color(0.95f, 0.25f, 0.35f, 1f);

        [SerializeField]
        private Transform _handCardParent;

        [SerializeField]
        private float _handCardSpacing = 180f;

        [SerializeField]
        private float _handCardRightPadding = 36f;

        [Header("Draw Animation")]
        [SerializeField]
        [Tooltip("카드 한 장씩 UI에 올릴 때 간격(초)")]
        private float _drawCardDelay = 0.12f;

        [SerializeField]
        [Tooltip("목표 위치까지 이동하는 시간(초)")]
        private float _cardMoveDuration = 0.22f;

        [SerializeField]
        [Tooltip("드로우 시작 X 오프셋(목표 위치 기준, 오른쪽). Y는 목표와 동일하게 고정")]
        private float _drawSpawnOffsetX = 220f;

        [Header("HUD (Prefab)")]
        [SerializeField]
        private Image _costBadgeImage;

        [SerializeField]
        private TextMeshProUGUI _costText;

        [SerializeField]
        private TextMeshProUGUI _deckCountText;

        [SerializeField]
        private TextMeshProUGUI _discardCountText;

        [SerializeField]
        private TextMeshProUGUI _handCountText;

        [SerializeField]
        private TextMeshProUGUI _costWarningText;

        [SerializeField]
        private Button _endTurnButton;

        [Header("Character Status")]
        [SerializeField]
        private Button _characterStatusButton;

        [SerializeField]
        [Tooltip("사람 아이콘을 넣을 Image 슬롯 (비워두고 나중에 스프라이트 지정)")]
        private Image _characterStatusIconSlot;

        [Header("Player HP")]
        [SerializeField]
        private RectTransform _hpBarParent;

        [SerializeField]
        [Tooltip("몬스터와 동일: Heat Health Bar 프리팹")]
        private GameObject _healthBarPrefab;

        [SerializeField]
        private Color _hpBarColor = new Color(0.92f, 0.18f, 0.22f, 1f);

        [SerializeField]
        [Tooltip("HUD용 스케일 (원본 240x40 기준)")]
        private float _hpBarHudScale = 1.35f;

        private ProgressBar _hpProgressBar;
        private UnitInfo _currentUnitInfo;
        private readonly List<GameObject> _handCardObjects = new();
        private int _refreshVersion;
        private float _costWarningHideAt;
        private CardNameBannerUI _cardNameBanner;

        /// <summary>
        /// Addressables 생성 직후 한 프레임 노출을 막기 위한 비활성 부모.
        /// </summary>
        private Transform _cardSpawnRoot;

        public Transform HandCardParent => _handCardParent;
        public UnitInfo CurrentUnitInfo => _currentUnitInfo;

        private void Awake()
        {
            if (_endTurnButton == null)
                _endTurnButton = GetComponentInChildren<Button>(true);

            WireEndTurnButton();
            WireCharacterStatusButton();
            EnsurePlayerHealthBar();
            EnsureCardNameBanner();

            if (_costWarningText != null)
                _costWarningText.gameObject.SetActive(false);

            ApplyHudFonts();
        }

        private void Update()
        {
            if (_costWarningText == null || !_costWarningText.gameObject.activeSelf)
                return;

            if (Time.unscaledTime >= _costWarningHideAt)
                _costWarningText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 드로우 결과와 현재 손패를 UI에 반영합니다.
        /// 카드는 짧은 딜레이로 한 장씩 등장하며 목표 위치로 이동합니다.
        /// </summary>
        public void OnCardsDrawn(UnitInfo unitInfo, IReadOnlyList<CardData> drawnCards)
        {
            if (unitInfo == null)
            {
                Debug.LogError("[InGamePlayerUI] UnitInfo가 null입니다.");
                return;
            }

            _currentUnitInfo = unitInfo;
            RefreshHud();
            RefreshHandAsync(unitInfo.Hand, sequentialDraw: true);

            if (drawnCards == null || drawnCards.Count == 0)
            {
                Debug.LogWarning("[InGamePlayerUI] 이번에 뽑은 카드가 없습니다.");
                return;
            }

            Debug.Log($"[InGamePlayerUI] 드로우 {drawnCards.Count}장 / 손패 {unitInfo.Hand.Count}장");
        }

        private static void PlayCardDrawSe()
        {
            SoundManager soundManager = GameManager.Instance?.SoundManager;
            if (soundManager == null)
            {
                Debug.LogWarning("[InGamePlayerUI] SoundManager가 없어 카드 드로우 SE를 재생할 수 없습니다.");
                return;
            }

            soundManager.PlaySe(PublicVariable.Address.SeCardDraw);
        }

        public void RefreshHand(IReadOnlyList<CardData> hand)
        {
            if (hand == null)
            {
                Debug.LogWarning("[InGamePlayerUI] hand가 null입니다.");
                return;
            }

            if (_handCardParent == null)
            {
                Debug.LogError("[InGamePlayerUI] _handCardParent가 없습니다.");
                return;
            }

            RefreshHud();
            RefreshHandAsync(hand, sequentialDraw: false);
        }

        /// <summary>
        /// 코스트/덱/버림/HP 등 HUD만 갱신합니다.
        /// </summary>
        public void RefreshHud()
        {
            UnitInfo unit = ResolveHudUnitInfo();
            int current = unit != null ? unit.CurrentCardCost : 0;
            int max = unit != null ? unit.MaxCardCost : 0;
            RefreshCostDisplay(current, max);
            RefreshHpDisplay(unit);

            if (_deckCountText != null)
                _deckCountText.text = $"덱 {unit?.DrawPile?.Count ?? 0}";

            if (_discardCountText != null)
                _discardCountText.text = $"버림 {unit?.DiscardPile?.Count ?? 0}";

            if (_handCountText != null)
                _handCountText.text = $"손패 {unit?.Hand?.Count ?? _handCardObjects.Count}";
        }

        /// <summary>
        /// 플레이어 피해/회복 직후 HP HUD만 갱신합니다.
        /// </summary>
        public void RefreshHpUI()
        {
            RefreshHpDisplay(ResolveHudUnitInfo());
        }

        private void RefreshHpDisplay(UnitInfo unit)
        {
            EnsurePlayerHealthBar();
            if (_hpProgressBar == null)
                return;

            int maxHp = 0;
            int currentHp = 0;
            if (unit != null)
            {
                maxHp = Mathf.Max(1, unit.MaxHp);
                currentHp = Mathf.Clamp(unit.CurrentHp, 0, maxHp);
            }
            else
            {
                maxHp = 1;
            }

            _hpProgressBar.maxValue = maxHp;
            _hpProgressBar.maxValueLimit = maxHp;
            _hpProgressBar.minValue = 0f;
            _hpProgressBar.SetValue(currentHp);
        }

        private void EnsurePlayerHealthBar()
        {
            if (_hpProgressBar != null)
                return;

            if (_healthBarPrefab == null || _hpBarParent == null)
            {
                Debug.LogWarning("[InGamePlayerUI] Health Bar 프리팹 또는 부모가 없습니다.");
                return;
            }

            GameObject instance = Instantiate(_healthBarPrefab, _hpBarParent, false);
            instance.name = "HealthBar";

            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one * Mathf.Max(0.1f, _hpBarHudScale);
            }

            DisableRaycasts(instance);
            HideHealthBarIcons(instance);
            ApplyHealthBarAccentColor(instance, _hpBarColor);

            _hpProgressBar = instance.GetComponent<ProgressBar>();
            if (_hpProgressBar == null)
                _hpProgressBar = instance.GetComponentInChildren<ProgressBar>(true);

            if (_hpProgressBar == null)
            {
                Debug.LogError("[InGamePlayerUI] ProgressBar를 찾을 수 없습니다.");
                Destroy(instance);
                return;
            }

            _hpProgressBar.addPrefix = false;
            _hpProgressBar.addSuffix = false;
            _hpProgressBar.decimals = 0;
            _hpProgressBar.minValue = 0f;
            _hpProgressBar.Initialize();

            ApplyHpBarFonts();
        }

        private void ApplyHpBarFonts()
        {
            if (_hpProgressBar == null)
                return;

            // Heat 기본 폰트 대신 프로젝트 본문 폰트
            var texts = _hpProgressBar.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
                UiFont.ApplyBody(texts[i]);
        }

        private static void ApplyHealthBarAccentColor(GameObject root, Color accent)
        {
            if (root == null)
                return;

            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || !IsHeatAccentColor(image.color))
                    continue;

                Color c = accent;
                c.a = image.color.a;
                image.color = c;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || !IsHeatAccentColor(text.color))
                    continue;

                Color c = accent;
                c.a = text.color.a;
                text.color = c;
            }
        }

        private static bool IsHeatAccentColor(Color color)
        {
            // Heat Health Bar 기본 오렌지(1, 0.686, 0) 근처만 교체
            return color.r > 0.85f &&
                   color.g > 0.45f && color.g < 0.85f &&
                   color.b < 0.25f;
        }

        private static void DisableRaycasts(GameObject root)
        {
            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        private static void HideHealthBarIcons(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                string n = transforms[i].name;
                if (n.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    transforms[i].gameObject.SetActive(false);
            }
        }

        private UnitInfo ResolveHudUnitInfo()
        {
            if (_currentUnitInfo != null)
                return _currentUnitInfo;

            var inGame = GameManager.Instance?.InGameManager;
            if (inGame == null)
                return null;

            var current = inGame.CurrentActor;
            if (current?.UnitInfo != null &&
                current.UnitInfo.UnitType == UNIT_TYPE.PLAYER)
            {
                return current.UnitInfo;
            }

            var players = inGame.PlayerCharacters;
            if (players == null)
                return null;

            for (int i = 0; i < players.Count; i++)
            {
                var character = players[i];
                if (character?.UnitInfo != null && character.IsAlive)
                    return character.UnitInfo;
            }

            return players.Count > 0 ? players[0]?.UnitInfo : null;
        }

        public void RefreshCostUI()
        {
            RefreshHud();
        }

        public void ShowInsufficientCost(int need, int current, int max)
        {
            if (_costWarningText == null)
                return;

            _costWarningText.text = $"코스트 부족  (필요 {need} / 현재 {current}/{max})";
            _costWarningText.gameObject.SetActive(true);
            _costWarningHideAt = Time.unscaledTime + 1.6f;
            RefreshCostDisplay(current, max);
        }

        public void ShowCardName(string cardName)
        {
            EnsureCardNameBanner();
            _cardNameBanner?.Show(cardName);
        }

        public void HideCardName()
        {
            _cardNameBanner?.Hide();
        }

        private void EnsureCardNameBanner()
        {
            if (_cardNameBanner != null)
                return;

            _cardNameBanner = GetComponentInChildren<CardNameBannerUI>(true);
            if (_cardNameBanner == null)
                _cardNameBanner = CardNameBannerUI.Create(transform);
        }

        private void RefreshCostDisplay(int current, int max)
        {
            current = Mathf.Max(0, current);
            max = Mathf.Max(0, max);

            if (_costText != null)
                _costText.text = $"{current} / {max}";
        }

        private async void RefreshHandAsync(IReadOnlyList<CardData> hand, bool sequentialDraw)
        {
            int version = ++_refreshVersion;
            ClearHandObjects();

            if (hand.Count == 0)
            {
                RefreshHud();
                return;
            }

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (resourceManager == null)
            {
                Debug.LogError("[InGamePlayerUI] ResourceManager를 찾을 수 없습니다.");
                return;
            }

            EnsureCardSpawnRoot();

            for (int i = 0; i < hand.Count; i++)
            {
                if (version != _refreshVersion)
                    return;

                if (sequentialDraw && i > 0)
                {
                    if (!await WaitSecondsAsync(_drawCardDelay, version))
                        return;
                }

                CardData cardData = hand[i];
                if (cardData == null)
                {
                    Debug.LogWarning($"[InGamePlayerUI] hand[{i}] CardData가 null입니다.");
                    continue;
                }

                GameObject cardObject = await resourceManager.InstantiateAsync(
                    PublicVariable.Address.CardObjectPrefab,
                    _cardSpawnRoot);

                if (version != _refreshVersion)
                {
                    if (cardObject != null)
                        resourceManager.ReleaseInstance(cardObject);
                    return;
                }

                if (cardObject == null)
                {
                    Debug.LogError("[InGamePlayerUI] CardObject 생성 실패");
                    continue;
                }

                cardObject.SetActive(false);

                InGameCardObject cardView = cardObject.GetComponent<InGameCardObject>();
                if (cardView == null)
                    cardView = cardObject.GetComponentInChildren<InGameCardObject>(true);

                if (cardView == null)
                {
                    Debug.LogError("[InGamePlayerUI] InGameCardObject 컴포넌트가 없습니다.");
                    resourceManager.ReleaseInstance(cardObject);
                    continue;
                }

                cardView.SetData(cardData);

                if (sequentialDraw)
                    PlayCardDrawSe();

                RectTransform cardRect = cardObject.transform as RectTransform;
                PrepareCardRect(cardRect);
                Canvas.ForceUpdateCanvases();

                _handCardObjects.Add(cardObject);

                int visibleCount = _handCardObjects.Count;
                Vector2 target = GetRightAlignedPosition(cardRect, visibleCount - 1, visibleCount);
                float spawnOffsetX = sequentialDraw ? _drawSpawnOffsetX : _drawSpawnOffsetX * 0.35f;
                Vector2 start = new Vector2(target.x + spawnOffsetX, target.y);

                if (cardRect != null)
                    cardRect.anchoredPosition = start;

                cardObject.SetActive(true);

                if (!await AnimateHandLayoutAsync(visibleCount, version))
                    return;

                RefreshHud();
            }

            RefreshHud();
        }

        private void EnsureCardSpawnRoot()
        {
            if (_cardSpawnRoot != null)
                return;

            var go = new GameObject("CardSpawnRoot", typeof(RectTransform));
            Transform parent = _handCardParent != null ? _handCardParent.parent : transform;
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            _cardSpawnRoot = go.transform;
        }

        private void PrepareCardRect(RectTransform cardRect)
        {
            if (cardRect == null)
                return;

            cardRect.SetParent(_handCardParent, false);
            cardRect.anchorMin = new Vector2(1f, 0.5f);
            cardRect.anchorMax = new Vector2(1f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.localRotation = Quaternion.identity;
            cardRect.localScale = Vector3.one;
        }

        private Vector2 GetRightAlignedPosition(RectTransform cardRect, int index, int totalCount)
        {
            if (cardRect == null || totalCount <= 0)
                return Vector2.zero;

            float cardWidth = GetCardWidth(cardRect);
            float rightEdgeOffset = (cardWidth * 0.5f) + _handCardRightPadding;
            float step = Mathf.Max(_handCardSpacing, cardWidth + _handCardRightPadding);
            float x = -rightEdgeOffset - ((totalCount - 1 - index) * step);
            return new Vector2(x, 0f);
        }

        private async Task<bool> AnimateHandLayoutAsync(int totalCount, int version)
        {
            if (totalCount <= 0)
                return version == _refreshVersion;

            var moveTasks = new List<Task>(_handCardObjects.Count);
            for (int i = 0; i < _handCardObjects.Count; i++)
            {
                if (_handCardObjects[i] == null)
                    continue;

                RectTransform cardRect = _handCardObjects[i].transform as RectTransform;
                if (cardRect == null)
                    continue;

                Vector2 target = GetRightAlignedPosition(cardRect, i, totalCount);
                moveTasks.Add(MoveAnchoredPositionAsync(cardRect, target, version));
            }

            if (moveTasks.Count == 0)
                return version == _refreshVersion;

            await Task.WhenAll(moveTasks);
            return version == _refreshVersion;
        }

        private async Task MoveAnchoredPositionAsync(RectTransform rect, Vector2 target, int version)
        {
            if (rect == null)
                return;

            float duration = Mathf.Max(0.01f, _cardMoveDuration);
            Vector2 start = rect.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (version != _refreshVersion || rect == null)
                    return;

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                rect.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
                await Task.Yield();
            }

            if (version == _refreshVersion && rect != null)
                rect.anchoredPosition = target;
        }

        private async Task<bool> WaitSecondsAsync(float seconds, int version)
        {
            float end = Time.unscaledTime + Mathf.Max(0f, seconds);
            while (Time.unscaledTime < end)
            {
                if (version != _refreshVersion)
                    return false;

                await Task.Yield();
            }

            return version == _refreshVersion;
        }

        private static float GetCardWidth(RectTransform cardRect)
        {
            float width = Mathf.Abs(cardRect.rect.width);
            if (width > 0.01f)
                return width;

            width = Mathf.Abs(cardRect.sizeDelta.x);
            if (width > 0.01f)
                return width;

            return 100f;
        }

        private void ClearHandObjects()
        {
            var resourceManager = GameManager.Instance?.ResourceManager;

            for (int i = 0; i < _handCardObjects.Count; i++)
            {
                GameObject cardObject = _handCardObjects[i];
                if (cardObject == null)
                    continue;

                if (resourceManager != null)
                    resourceManager.ReleaseInstance(cardObject);
                else
                    Destroy(cardObject);
            }

            _handCardObjects.Clear();
        }

        public void ClearHandUI()
        {
            _refreshVersion++;
            _currentUnitInfo = null;
            ClearHandObjects();
            RefreshHud();
        }

        public void RemoveCardFromHand(InGameCardObject cardObject)
        {
            if (cardObject == null)
                return;

            int index = -1;
            for (int i = 0; i < _handCardObjects.Count; i++)
            {
                if (_handCardObjects[i] == null)
                    continue;

                InGameCardObject view = _handCardObjects[i].GetComponent<InGameCardObject>();
                if (view == null)
                    view = _handCardObjects[i].GetComponentInChildren<InGameCardObject>(true);

                if (view == cardObject || _handCardObjects[i] == cardObject.gameObject)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                Debug.LogWarning("[InGamePlayerUI] 제거할 카드 UI를 찾지 못했습니다.");
                return;
            }

            RemoveCardAtIndexAndCompact(index);
        }

        public void RemoveCardFromHand(CardData cardData)
        {
            if (cardData == null)
                return;

            for (int i = 0; i < _handCardObjects.Count; i++)
            {
                if (_handCardObjects[i] == null)
                    continue;

                InGameCardObject view = _handCardObjects[i].GetComponentInChildren<InGameCardObject>(true);
                if (view != null && view.CardData == cardData)
                {
                    RemoveCardAtIndexAndCompact(i);
                    return;
                }
            }

            Debug.LogWarning($"[InGamePlayerUI] CardData에 해당하는 UI를 찾지 못했습니다: {cardData.Tid}");
        }

        private void RemoveCardAtIndexAndCompact(int index)
        {
            if (index < 0 || index >= _handCardObjects.Count)
                return;

            GameObject cardObject = _handCardObjects[index];
            _handCardObjects.RemoveAt(index);

            var resourceManager = GameManager.Instance?.ResourceManager;
            if (cardObject != null)
            {
                if (resourceManager != null)
                    resourceManager.ReleaseInstance(cardObject);
                else
                    Destroy(cardObject);
            }

            int version = ++_refreshVersion;
            CompactHandLayoutAsync(version);
            RefreshHud();
        }

        private async void CompactHandLayoutAsync(int version)
        {
            if (_handCardObjects.Count == 0)
                return;

            await AnimateHandLayoutAsync(_handCardObjects.Count, version);
        }

        public void SetInteractable(bool interactable)
        {
            if (_endTurnButton != null)
                _endTurnButton.interactable = interactable;

            for (int i = 0; i < _handCardObjects.Count; i++)
            {
                if (_handCardObjects[i] == null)
                    continue;

                InGameCardObject cardView = _handCardObjects[i].GetComponent<InGameCardObject>();
                if (cardView == null)
                    cardView = _handCardObjects[i].GetComponentInChildren<InGameCardObject>(true);

                cardView?.SetInteractable(interactable);
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (visible)
                RefreshHud();
        }

        private void OnDestroy()
        {
            _refreshVersion++;
            ClearHandObjects();
        }

        private void WireEndTurnButton()
        {
            if (_endTurnButton == null)
                return;

            _endTurnButton.onClick.RemoveListener(OnClickEndTurn);
            _endTurnButton.onClick.AddListener(OnClickEndTurn);
        }

        private void WireCharacterStatusButton()
        {
            if (_characterStatusButton == null)
                return;

            _characterStatusButton.onClick.RemoveListener(OnClickCharacterStatus);
            _characterStatusButton.onClick.AddListener(OnClickCharacterStatus);
        }

        private void OnClickCharacterStatus()
        {
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[InGamePlayerUI] UIManager가 없습니다.");
                return;
            }

            GameManager.Instance?.SoundManager?.PlaySe(PublicVariable.Address.UiButtonClickSe);
            uiManager.Show(PublicVariable.Address.CharacterStatusUIPrefab, ui =>
            {
                if (ui is CharacterStatusUI statusUi)
                    statusUi.Refresh();
            });
        }

        private void OnClickEndTurn()
        {
            var inGame = GameManager.Instance?.InGameManager;
            if (inGame == null)
            {
                Debug.LogError("[InGamePlayerUI] InGameManager가 없습니다.");
                return;
            }

            inGame.EndTurn();
        }

        private void ApplyHudFonts()
        {
            // CostText는 프리팹 Cinzel(장식 숫자) 유지
            UiFont.ApplyBody(_deckCountText);
            UiFont.ApplyBody(_discardCountText);
            UiFont.ApplyBody(_handCountText);
            UiFont.ApplyBody(_costWarningText);
            ApplyHpBarFonts();

            if (_endTurnButton != null)
            {
                TextMeshProUGUI label = _endTurnButton.GetComponentInChildren<TextMeshProUGUI>(true);
                UiFont.ApplyBody(label);
            }
        }
    }
}
