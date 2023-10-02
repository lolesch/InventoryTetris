using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class CurrencyDisplay : AbstractDisplay<Currency>
    {
        [SerializeField] private CoinDisplay[] coinDisplays = new CoinDisplay[4];

        [SerializeField] private TextMeshProUGUI totalText;

        public override void Display(Currency newData)
        {
            if (totalText)
                totalText.text = $"({newData.Total})".Colored(Color.gray);

            foreach (var coin in coinDisplays)
            { coin.gameObject.SetActive(true); }

            coinDisplays[0].Display(CurrencyType.Gold, newData.Gold);
            coinDisplays[1].Display(CurrencyType.Silver, newData.Silver);
            coinDisplays[2].Display(CurrencyType.Copper, newData.Copper);
            coinDisplays[3].Display(CurrencyType.Iron, newData.Iron);
        }
    }
}