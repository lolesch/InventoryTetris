using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using Submodules.Utility.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    
    public sealed class CoinDisplay : MonoBehaviour, IDisplay<(CurrencyType type, uint amount)>
    {
        [SerializeField] private Image coinIcon;
        [SerializeField] private TextMeshProUGUI amountText;

        public void Refresh((CurrencyType type, uint amount) newData)
        {
            if (0 == newData.amount)
            {
                gameObject.SetActive(false);
                return;
            }

            if (coinIcon)
                coinIcon.sprite = ItemService.Instance.GetIcon(newData.type);

            if (amountText)
                amountText.text = $"{newData.amount}";
        }
    }
}