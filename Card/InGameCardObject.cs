using TMPro;
using UnityEngine;

namespace SHIN
{
    public class InGameCardObject : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _cardNameText;
        [SerializeField]
        private TextMeshProUGUI _cardDescriptionText;

        private CardData _cardData;
        public CardData CardData => _cardData;

        public void SetData(CardData cardData)
        {
            _cardData = cardData;

            if (_cardNameText != null)
                _cardNameText.text = cardData != null ? cardData.Name : string.Empty;

            if (_cardDescriptionText != null)
                _cardDescriptionText.text = cardData != null ? cardData.Description : string.Empty;
        }
    }
}
