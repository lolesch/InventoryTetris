using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class CoinDisplay : AbstractDisplay<(CurrencyType type, uint amount)>
    {
        [SerializeField] private Image coinIcon;
        [SerializeField] private TextMeshProUGUI amountText;

        public override void Display((CurrencyType type, uint amount) newData)
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

        public void Display(CurrencyType type, uint amount) => Display((type, amount));
    }
}