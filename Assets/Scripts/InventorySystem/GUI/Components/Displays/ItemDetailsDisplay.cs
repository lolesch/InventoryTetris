using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Items;
using ToolSmiths.InventorySystem.Runtime.Pools;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    [RequireComponent(typeof(RectTransform))]
    public class ItemDetailsDisplay : MonoBehaviour, IDisplay<(Package package, List<CharacterStatModifier> compareTo)>, IDisplay<Package>
    {
        [SerializeField] private ItemDisplay itemDisplay;

        [SerializeField] private List<Image> horizontalLines;
        [SerializeField] private TextMeshProUGUI itemName;
        [SerializeField] private TextMeshProUGUI itemType;
        [SerializeField] private CurrencyDisplay goldValue;

        [SerializeField] private CharacterStatModifierDisplay itemStatPrefab;
        [SerializeField] private PrefabPool<CharacterStatModifierDisplay> itemStatPool;

        public void RefreshDisplay((Package package, List<CharacterStatModifier> compareTo) data) => RefreshDisplay(data.package, data.compareTo);
        public void RefreshDisplay(Package package) => RefreshDisplay(package, null);

        private void RefreshDisplay(Package package, List<CharacterStatModifier> compareTo)
        {
            if (!package.IsValid)
                return;

            itemDisplay.RefreshDisplay(package);

            var rarityColor = AbstractItem.GetRarityColor(package.Item.Rarity);

            if (itemName)
                itemName.text = package.Item.ToString().Colored(rarityColor);

            if (itemType)
                itemType.text = package.Item.ToString();

            if (goldValue)
                goldValue.RefreshDisplay(new Currency(package.Item.SellValue)); //? $"{package.Item.GoldValue}" : string.Empty;

            if (horizontalLines != null && 0 < horizontalLines.Count)
                for (var i = 0; i < horizontalLines.Count; i++)
                    horizontalLines[i].color = rarityColor;

            itemStatPool ??= new(itemStatPrefab);
            itemStatPool.ReleaseAll();

            foreach (var stat in package.Item.Affixes)
            {
                var itemStat = itemStatPool.GetObject();

                itemStat.RefreshDisplay(new(stat, compareTo));
            }

            //TODO:
            /*  durability?
             *  flavor text?
             */
        }
    }
}
