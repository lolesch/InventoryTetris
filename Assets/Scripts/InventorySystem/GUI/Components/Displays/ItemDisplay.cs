using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    [RequireComponent(typeof(RectTransform))]
    public class ItemDisplay : MonoBehaviour, IDisplay<Package>
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image frame;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI amount;

        public void RefreshDisplay(Package package)
        {
            if (!package.IsValid)
                return;

            if (icon)
                icon.sprite = package.Item.Icon;

            if (amount)
                amount.text = 1 < package.Amount ? package.Amount.ToString() : string.Empty;

            var rarityColor = AbstractItem.GetRarityColor(package.Item.Rarity);

            if (frame)
                frame.color = rarityColor;

            if (background)
                background.color = rarityColor * Color.gray * Color.gray;
        }
    }
}