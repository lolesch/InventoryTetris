using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using Submodules.Utility.Extensions;
using Submodules.Utility.Tools;
using Submodules.Utility.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    // TODO: inherit AbstractDisplay
    [RequireComponent(typeof(RectTransform))]
    public class PreviewDisplay : MonoBehaviour, IView<(Package package, Package compareTo)>
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

        private void Awake()
        {
            gameObject.SetActive(false);
            itemStatPool = new(itemStatPrefab);
        }

        public void Refresh((Package package, Package compareTo) data) => Refresh(data.package, data.compareTo);
        public void Refresh(Package package, Package compareTo, float priceOverride = -1f)
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

            var view = ItemView.Of(package.Item);
            var rarityColor = ItemView.RarityColorOf(package.Item.Rarity);

            if (itemName)
                itemName.text = view.DisplayName.Colored(rarityColor);

            if (itemType)
                itemType.text = view.DisplayName;

            if (icon)
                icon.sprite = view.Icon;

            if (amount)
                amount.text = 1 < package.Amount ? $"{package.Amount}/{view.StackLimit}" : string.Empty;

            if (goldValue)
                goldValue.Refresh(0f <= priceOverride
                    ? new Currency(priceOverride)
                    : new Currency(view.SellValue));

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
                //TODO: extend prefabPool to support abstractDisplays that update the Display(newData) before activating the object

                var itemStat = itemStatPool.GetObject(false);

                itemStat.Refresh(new(stat, compareTo));

                itemStat.gameObject.SetActive(true);
            }

            gameObject.SetActive(true);
        }

        public void Refresh(Package package)
        {
            if (!package.IsValid)
            {
                gameObject.SetActive(false);
                return;
            }

            var view = ItemView.Of(package.Item);
            var rarityColor = ItemView.RarityColorOf(package.Item.Rarity);

            if (itemName)
                itemName.text = view.DisplayName.Colored(rarityColor);

            if (itemType)
                itemType.text = view.DisplayName;

            if (icon)
                icon.sprite = view.Icon;

            if (amount)
                amount.text = 1 < package.Amount ? $"{package.Amount}/{view.StackLimit}" : string.Empty;

            if (goldValue)
                goldValue.Refresh(new Currency(view.SellValue)); //? $"{package.Item.GoldValue}" : string.Empty;

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
                //TODO: extend prefabPool to support abstractDisplays that update the Display(newData) before activating the object

                var itemStat = itemStatPool.GetObject(false);

                itemStat.Refresh(new(stat));

                itemStat.gameObject.SetActive(true);
            }

            gameObject.SetActive(true);
        }
    }
}
