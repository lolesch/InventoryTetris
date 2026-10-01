using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using Submodules.Utility.Extensions;
using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public sealed class CurrencyDisplay : MonoBehaviour, IDisplay<Currency>
    {
        [SerializeField] private CoinDisplay[] coinDisplays = new CoinDisplay[4];

        [SerializeField] private TextMeshProUGUI totalText;

        public void Refresh(Currency newData)
        {
            if (totalText)
                totalText.text = $"({newData.Total})".Colored(Color.gray);

            foreach (var coin in coinDisplays)
            { coin.gameObject.SetActive(true); }

            coinDisplays[0].Refresh((CurrencyType.Gold, newData.Gold));
            coinDisplays[1].Refresh((CurrencyType.Silver, newData.Silver));
            coinDisplays[2].Refresh((CurrencyType.Copper, newData.Copper));
            coinDisplays[3].Refresh((CurrencyType.Iron, newData.Iron));
        }
    }
}