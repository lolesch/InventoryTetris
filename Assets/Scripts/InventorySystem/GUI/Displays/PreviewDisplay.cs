
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
    // TODO: inherit AbstractDisplay
    public class PreviewDisplay : MonoBehaviour
    {
        public RectTransform ItemDisplay;
        [SerializeField] private Image icon;
        [SerializeField] private Image frame;
        [SerializeField] private List<Image> horizontalLines;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI itemName;
        [SerializeField] private TextMeshProUGUI itemType;
        [SerializeField] private TextMeshProUGUI amount;
        [SerializeField] private TextMeshProUGUI goldValue;

        [SerializeField] private CharacterStatModifierDisplay itemStatPrefab;
        [SerializeField] private PrefabPool<CharacterStatModifierDisplay> itemStatPool;

        public bool IsPreviewing => ItemDisplay.gameObject.activeSelf;

        private void Awake()
        {
            ItemDisplay.gameObject.SetActive(false);
            itemStatPool = new(itemStatPrefab);
        }

        public void SetDisplay(Package package, Package[] compareTo)
        {
            if (package.Item == null || package.Amount < 1)
            {
                ItemDisplay.gameObject.SetActive(false);
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
                amount.text = 1 < package.Amount /*&& package.Item is ConsumableItem*/ ? $"{package.Amount}/{(int)(package.Item as ConsumableItem).StackLimit}" : string.Empty;

            if (goldValue)
                goldValue.text = 0 < package.Item.GoldValue ? $"GoldValue: {package.Item.GoldValue}" : string.Empty;

            if (frame)
                frame.color = rarityColor;

            if (horizontalLines != null && 0 < horizontalLines.Count)
                for (var i = 0; i < horizontalLines.Count; i++)
                    horizontalLines[i].color = rarityColor;

            if (background)
                background.color = rarityColor * Color.gray * Color.gray;

            itemStatPool.ReleaseAll();

            foreach (var stat in package.Item.Affixes)
            {
                //TODO: inherit prefabPool to support abstractDisplays that update the Display(newData) before activating the object

                var itemStat = itemStatPool.GetObject(false);

                itemStat.Display(new(stat, compareTo));

                itemStat.gameObject.SetActive(true);
            }

            ItemDisplay.gameObject.SetActive(true);
        }
    }
}
