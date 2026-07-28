using System;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    [Serializable]
    public struct StageShopPriceTable
    {
        public int CardCommonPrice;
        public int CardRarePrice;
        public int ItemCommonPrice;
        public int ItemRarePrice;
    }

    public partial class StageManager
    {
        [Header("Stage Shop")]
        [SerializeField]
        private int _shopOfferCount = 6;

        [SerializeField]
        private StageShopPriceTable _shopPriceTable = new StageShopPriceTable
        {
            CardCommonPrice = 100,
            CardRarePrice = 180,
            ItemCommonPrice = 120,
            ItemRarePrice = 220,
        };

        private int _activeShopNodeId = -1;
        private StageShopUI _activeShopUI;
        private readonly List<StageShopOffer> _activeShopOffers = new();

        private void EnterShop(StageNodeData node)
        {
            if (node == null)
                return;

            _activeShopNodeId = node.NodeId;
            SetStageNodeUIVisible(false);
            StartShopFlowAsync();
        }

        private async void StartShopFlowAsync()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("[StageManager] GameManager가 없습니다.");
                AbortShopFlow();
                return;
            }

            List<StageRewardOffer> rewardOffers = await BuildRewardOffersAsync(_shopOfferCount);
            if (rewardOffers == null || rewardOffers.Count == 0)
            {
                Debug.LogWarning("[StageManager] 상점 상품 생성 실패로 맵으로 복귀합니다.");
                AbortShopFlow();
                return;
            }

            _activeShopOffers.Clear();
            for (int i = 0; i < rewardOffers.Count; i++)
            {
                StageRewardOffer reward = rewardOffers[i];
                if (reward == null)
                    continue;

                _activeShopOffers.Add(new StageShopOffer
                {
                    Reward = reward,
                    Price = ResolveShopPrice(reward),
                    IsSoldOut = false,
                });
            }

            if (_activeShopOffers.Count == 0)
            {
                Debug.LogWarning("[StageManager] 상점 상품이 비어 있어 맵으로 복귀합니다.");
                AbortShopFlow();
                return;
            }

            UIManager uiManager = ResolveUIManager();
            if (uiManager == null)
            {
                AbortShopFlow();
                return;
            }

            uiManager.Show(PublicVariable.Address.StageShopUIPrefab, uiBase =>
            {
                if (uiBase is not StageShopUI shopUI)
                {
                    Debug.LogError("[StageManager] StageShopUI 컴포넌트가 없습니다.");
                    AbortShopFlow();
                    return;
                }

                _activeShopUI = shopUI;
                shopUI.Setup(
                    _activeShopOffers,
                    gameManager.PlayerGold,
                    OnShopBuyRequested,
                    OnClickExitShop);
            });
        }

        private int ResolveShopPrice(StageRewardOffer reward)
        {
            if (reward == null)
                return 0;

            switch (reward.Kind)
            {
                case STAGE_REWARD_KIND.CARD:
                    return reward.Grade == ITEM_GRADE.RARE
                        ? _shopPriceTable.CardRarePrice
                        : _shopPriceTable.CardCommonPrice;

                case STAGE_REWARD_KIND.ITEM:
                    return reward.Grade == ITEM_GRADE.RARE
                        ? _shopPriceTable.ItemRarePrice
                        : _shopPriceTable.ItemCommonPrice;

                default:
                    return _shopPriceTable.ItemCommonPrice;
            }
        }

        private void OnShopBuyRequested(int index)
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null)
                return;

            if (index < 0 || index >= _activeShopOffers.Count)
                return;

            StageShopOffer offer = _activeShopOffers[index];
            if (offer == null || offer.IsSoldOut || offer.Reward == null)
                return;

            int price = Mathf.Max(0, offer.Price);
            if (!gameManager.TrySpendGold(price))
            {
                _activeShopUI?.RefreshGold(gameManager.PlayerGold);
                return;
            }

            bool purchaseSuccess = ApplyShopPurchase(offer);
            if (purchaseSuccess)
            {
                offer.IsSoldOut = true;
                _activeShopOffers[index] = offer;
            }
            else
            {
                gameManager.AddGold(price);
            }

            if (_activeShopUI != null)
            {
                _activeShopUI.RefreshOfferState(index, gameManager.PlayerGold);
                _activeShopUI.RefreshGold(gameManager.PlayerGold);
            }
        }

        private bool ApplyShopPurchase(StageShopOffer offer)
        {
            if (offer == null || offer.Reward == null)
                return false;

            var gameManager = GameManager.Instance;
            if (gameManager == null)
                return false;

            var players = gameManager.PlayerCharacters;
            if (players == null || players.Count == 0 || players[0] == null)
            {
                Debug.LogError("[StageManager] 플레이어 캐릭터가 없어 상점 구매를 적용할 수 없습니다.");
                return false;
            }

            UnitInfo player = players[0];
            StageRewardOffer reward = offer.Reward;
            switch (reward.Kind)
            {
                case STAGE_REWARD_KIND.CARD:
                    if (string.IsNullOrEmpty(reward.Tid))
                        return false;

                    gameManager.AddCard(player, reward.Tid);
                    return true;

                case STAGE_REWARD_KIND.ITEM:
                    if (reward.ItemData != null)
                    {
                        player.AddItem(reward.ItemData);
                        return true;
                    }

                    if (string.IsNullOrEmpty(reward.Tid))
                        return false;

                    return player.AddItem(reward.Tid);
            }

            return false;
        }

        public void OnClickExitShop()
        {
            FinishShopFlow();
        }

        private void FinishShopFlow()
        {
            if (_activeShopNodeId >= 0)
            {
                ApplyNodeCleared(_activeShopNodeId);
                SaveMapData();
            }

            UIManager uiManager = ResolveUIManager();
            if (uiManager != null && uiManager.Current is StageShopUI)
                uiManager.Close();

            _activeShopNodeId = -1;
            _activeShopUI = null;
            _activeShopOffers.Clear();
            ReturnToStageNodeUI();
        }

        private void AbortShopFlow()
        {
            UIManager uiManager = ResolveUIManager();
            if (uiManager != null && uiManager.Current is StageShopUI)
                uiManager.Close();

            _activeShopNodeId = -1;
            _activeShopUI = null;
            _activeShopOffers.Clear();
            ReturnToStageNodeUI();
        }
    }
}
