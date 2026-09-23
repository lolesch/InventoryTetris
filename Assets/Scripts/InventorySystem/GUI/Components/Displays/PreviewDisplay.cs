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
    public class PreviewDisplay : SimplePanel, IView<(Package package, Package compareTo)>
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
        private PrefabPool<CharacterStatModifierDisplay> itemStatPool;
        private PrefabPool<CharacterStatModifierDisplay> ItemStatPool => itemStatPool ??= new(itemStatPrefab);

        /// True from the moment a hover starts fading in to the moment the next one starts
        /// fading out - matches the old activeSelf-based reading so PreviewProvider's per-frame
        /// cursor-follow keeps running through the fade instead of waiting for it to finish.
        private bool isPreviewing;
        public bool IsPreviewing => isPreviewing;

        protected override void BeforeAppear() => isPreviewing = true;

        protected override void BeforeDisappear()
        {
            isPreviewing = false;
            base.BeforeDisappear();
        }

        public void Refresh((Package package, Package compareTo) data) => Refresh(data.package, data.compareTo);
        public void Refresh(Package package, Package compareTo, float priceOverride = -1f)
        {
            if (!package.IsValid)
            {
                FadeOut();
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

            ItemStatPool.ReleaseAll();

            foreach (var stat in package.Item.Affixes)
            {
                //TODO: extend prefabPool to support abstractDisplays that update the Display(newData) before activating the object

                var itemStat = ItemStatPool.GetObject(false);

                itemStat.Refresh(new(stat, compareTo));

                itemStat.gameObject.SetActive(true);
            }

            FadeIn();
        }

        public void Refresh(Package package)
        {
            if (!package.IsValid)
            {
                FadeOut();
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

            ItemStatPool.ReleaseAll();

            foreach (var stat in package.Item.Affixes)
            {
                //TODO: extend prefabPool to support abstractDisplays that update the Display(newData) before activating the object

                var itemStat = ItemStatPool.GetObject(false);

                itemStat.Refresh(new(stat));

                itemStat.gameObject.SetActive(true);
            }

            FadeIn();
        }
    }
}
