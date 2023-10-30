using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Pools;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    [RequireComponent(typeof(RectTransform))]
    public class PreviewDisplay : MonoBehaviour, IDisplay<(Package package, Package compareTo)>
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image frame;
        [SerializeField] private List<Image> horizontalLines;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI itemName;
        [SerializeField] private TextMeshProUGUI itemType;
        [SerializeField] private TextMeshProUGUI amount;
        [SerializeField] private CurrencyDisplay goldValue;

        [SerializeField] private CharacterStatModifierDisplay itemStatPrefab;
        [SerializeField] private PrefabPool<CharacterStatModifierDisplay> itemStatPool;

        public bool IsPreviewing => gameObject.activeSelf;

        private void Start() =>
            //itemStatPool = new(itemStatPrefab);
            gameObject.SetActive(false);

        public void RefreshDisplay((Package package, Package compareTo) data) => RefreshDisplay(data.package, data.compareTo);
        private void RefreshDisplay(Package package, Package compareTo)
        {
            if (!package.IsValid)
            {
                gameObject.SetActive(false);
                return;
            }

            //TODO:
            /*  durability?
             *  flavor text?
             */

            var rarityColor = AbstractItem.GetRarityColor(package.Item.Rarity);

            if (itemName)
                itemName.text = package.Item.ToString().Colored(rarityColor);

            if (itemType)
                itemType.text = package.Item.ToString();

            if (icon)
                icon.sprite = package.Item.Icon;

            if (amount)
                amount.text = 1 < package.Amount ? $"{package.Amount}/{(int)package.Item.StackLimit}" : string.Empty;

            if (goldValue)
                goldValue.RefreshDisplay(new Currency(package.Item.SellValue)); //? $"{package.Item.GoldValue}" : string.Empty;

            if (frame)
                frame.color = rarityColor;

            if (horizontalLines != null && 0 < horizontalLines.Count)
                for (var i = 0; i < horizontalLines.Count; i++)
                    horizontalLines[i].color = rarityColor;

            if (background)
                background.color = rarityColor * Color.gray * Color.gray;

            itemStatPool ??= new(itemStatPrefab);
            itemStatPool.ReleaseAll();

            foreach (var stat in package.Item.Affixes)
            {
                //TODO: extend prefabPool to support abstractDisplays that update the Display(newData) before activating the object

                var itemStat = itemStatPool.GetObject(false);

                if (compareTo.IsValid)
                    itemStat.RefreshDisplay(new(stat, compareTo));
                else
                    itemStat.RefreshDisplay(new(stat));

                itemStat.gameObject.SetActive(true);
            }

            gameObject.SetActive(true);
        }
    }
}
