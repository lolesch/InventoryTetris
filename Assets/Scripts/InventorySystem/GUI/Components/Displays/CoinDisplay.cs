using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class CoinDisplay : MonoBehaviour, IDisplay<(CurrencyType type, uint amount)>
    {
        [SerializeField] private Image coinIcon;
        [SerializeField] private TextMeshProUGUI amountText;

        public void RefreshDisplay((CurrencyType type, uint amount) newData)
        {
            if (0 == newData.amount)
            {
                gameObject.SetActive(false);
                return;
            }

            if (coinIcon)
                coinIcon.sprite = ItemProvider.Instance.GetIcon(newData.type);

            if (amountText)
                amountText.text = $"{newData.amount}";
        }

        public void Display(CurrencyType type, uint amount) => RefreshDisplay((type, amount));
    }
}