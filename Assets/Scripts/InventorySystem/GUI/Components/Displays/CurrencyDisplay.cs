using TMPro;
using ToolSmiths.InventorySystem.Data;
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
                totalText.text = $"({newData.Total})".Colored(UiColors.Muted);

            foreach (var coin in coinDisplays)
            { coin.gameObject.SetActive(true); }

            for (var i = 0; i < Currency.Denominations.Length; i++) // a short coinDisplays array throws, not hides a row
            {
                var type = Currency.Denominations[i]; // largest value first
                coinDisplays[i].Refresh((type, newData.CountOf(type)));
            }
        }
    }
}