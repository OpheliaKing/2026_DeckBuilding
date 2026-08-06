using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// TEMP: 상점 UI 확인용. 나중에 이 파일과 Canvas/TextShop 연결을 삭제하세요.
    /// </summary>
    public partial class GameManager
    {
        private readonly List<StageShopOffer> _testShopOffers = new();
        private StageShopUI _testShopUI;

        private const int TestShopOfferCount = 3;

        /// <summary>
        /// Canvas/TextShop 버튼용. 임의의 아이템/카드 3개로 StageShopUI를 연다.
        /// </summary>
        public void OnClickTestShop()
        {
            OpenTestShopAsync();
        }

        private async void OpenTestShopAsync()
        {
            UIManager uiManager = UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[GameManager][TEMP TestShop] UIManager가 없습니다.");
                return;
            }

            if (_playerGold < 500)
                AddGold(500 - _playerGold);

            ItemDataSO itemDataSO = await GetSOAsync<ItemDataSO>(PublicVariable.Address.ItemDataSO);
            CardDataSO cardDataSO = await GetSOAsync<CardDataSO>(PublicVariable.Address.CardDataSO);

            if (itemDataSO != null && !itemDataSO.IsRewardIndexBuilt)
                itemDataSO.BuildRewardIndex();
            if (cardDataSO != null)
                cardDataSO.BuildIndex();

            _testShopOffers.Clear();
            BuildTestShopOffers(itemDataSO, cardDataSO);

            if (_testShopOffers.Count < TestShopOfferCount)
            {
                Debug.LogError("[GameManager][TEMP TestShop] 테스트 상품을 만들지 못했습니다.");
                return;
            }

            uiManager.Show(PublicVariable.Address.StageShopUIPrefab, uiBase =>
            {
                if (uiBase is not StageShopUI shopUI)
                {
                    Debug.LogError("[GameManager][TEMP TestShop] StageShopUI 컴포넌트가 없습니다.");
                    return;
                }

                _testShopUI = shopUI;
                shopUI.Setup(
                    _testShopOffers,
                    PlayerGold,
                    OnTestShopBuyRequested,
                    OnTestShopExit);
                Debug.Log($"[GameManager][TEMP TestShop] 상점 오픈 offers={_testShopOffers.Count} gold={PlayerGold}");
            });
        }

        private void BuildTestShopOffers(ItemDataSO itemDataSO, CardDataSO cardDataSO)
        {
            int[] prices = { 80, 100, 120 };
            int slot = 0;

            if (itemDataSO != null && itemDataSO.ItemDatas != null)
            {
                for (int i = 0; i < itemDataSO.ItemDatas.Count && slot < 2; i++)
                {
                    ItemData item = itemDataSO.ItemDatas[i];
                    if (item == null)
                        continue;

                    _testShopOffers.Add(new StageShopOffer
                    {
                        Reward = new StageRewardOffer
                        {
                            Kind = STAGE_REWARD_KIND.ITEM,
                            Tid = item.Tid,
                            ItemData = item,
                            Grade = item.ItemGrade,
                        },
                        Price = prices[slot],
                        IsSoldOut = false,
                    });
                    slot++;
                }
            }

            if (cardDataSO != null)
            {
                for (int i = 0; i < cardDataSO.Count && slot < TestShopOfferCount; i++)
                {
                    CardData card = cardDataSO.GetCardData(i);
                    if (card == null)
                        continue;

                    _testShopOffers.Add(new StageShopOffer
                    {
                        Reward = new StageRewardOffer
                        {
                            Kind = STAGE_REWARD_KIND.CARD,
                            Tid = card.Tid,
                            CardData = card,
                            Grade = card.CardGrade,
                        },
                        Price = prices[slot],
                        IsSoldOut = false,
                    });
                    slot++;
                }
            }

            // SO가 비어도 최소 3칸 UI 확인용 더미
            while (slot < TestShopOfferCount)
            {
                _testShopOffers.Add(new StageShopOffer
                {
                    Reward = new StageRewardOffer
                    {
                        Kind = STAGE_REWARD_KIND.ITEM,
                        Tid = $"TEST_ITEM_{slot}",
                        ItemData = null,
                        Grade = ITEM_GRADE.COMMON,
                    },
                    Price = prices[slot],
                    IsSoldOut = false,
                });
                slot++;
            }
        }

        private void OnTestShopBuyRequested(int index)
        {
            if (index < 0 || index >= _testShopOffers.Count)
                return;

            StageShopOffer offer = _testShopOffers[index];
            if (offer == null || offer.IsSoldOut)
                return;

            int price = Mathf.Max(0, offer.Price);
            if (!TrySpendGold(price))
            {
                _testShopUI?.RefreshGold(PlayerGold);
                Debug.Log($"[GameManager][TEMP TestShop] 골드 부족 index={index} need={price} have={PlayerGold}");
                return;
            }

            offer.IsSoldOut = true;
            _testShopOffers[index] = offer;
            _testShopUI?.RefreshOfferState(index, PlayerGold);
            _testShopUI?.RefreshGold(PlayerGold);
            Debug.Log($"[GameManager][TEMP TestShop] 구매(표시만) index={index} tid={offer.Reward?.Tid}");
        }

        private void OnTestShopExit()
        {
            UIManager uiManager = UIManager;
            if (uiManager != null && uiManager.Current is StageShopUI)
                uiManager.Close();

            _testShopUI = null;
            _testShopOffers.Clear();
            Debug.Log("[GameManager][TEMP TestShop] 상점 닫음");
        }
    }
}
