using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Services;
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
    public sealed class PreviewDisplay : SimplePanel, IDisplay<(Package package, Package compareTo)>
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

        public void Refresh((Package package, Package compareTo) data) => Refresh(data.package, new[] { data.compareTo });

        /// <param name="compareTo">The worn items equipping this one would displace, judged together; empty
        /// for a free slot, where each row shows its full effect; null for an item that is
        /// not equipment, which has nothing to compare. The worn item beside it is drawn with
        /// <see cref="RefreshWorn"/>.</param>
        public void Refresh(Package package, IReadOnlyList<Package> compareTo, float priceOverride = -1f) =>
            Present(package, priceOverride, (stat, earlier) => compareTo == null ? new(stat) : new(stat, compareTo, earlier));

        /// <summary>A worn item shown beside the hovered one. When the hovered item would displace it, the
        /// stats the hovered item lacks carry what the unequip alone costs; every other row stays plain.</summary>
        public void RefreshWorn(Package worn, Package hovered, bool displaced) =>
            Present(worn, -1f, (stat, _) => displaced && hovered.IsValid ? new(stat, hovered.Item) : new(stat));

        public void Refresh(Package package) => Present(package, -1f, (stat, _) => new(stat));

        private void Present(Package package, float priceOverride,
            System.Func<CharacterStatModifier, IReadOnlyList<CharacterStatModifier>, CharacterStatModifierDisplay.CharacterStatModifierData> row)
        {
            if (!package.IsValid)
            {
                Collapse();
                return;
            }

            //TODO:
            /*  durability?
             *  flavor text?
             */

            var view = ItemService.Instance.View(package.Item);
            var rarityColor = UiColors.Rarity(package.Item.Rarity);

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
                background.color = UiColors.SlotBackground(rarityColor);

            ItemStatPool.ReleaseAll();

            var earlier = new List<CharacterStatModifier>();

            foreach (var stat in package.Item.Affixes)
            {
                //TODO: extend prefabPool to support abstractDisplays that update the Display(newData) before activating the object

                var itemStat = ItemStatPool.GetObject(false);

                itemStat.Refresh(row(stat, earlier));

                itemStat.gameObject.SetActive(true);

                earlier.Add(stat);
            }

            Expand();
        }

        /// <summary>Content (text, pooled stat rows) is set just before <see cref="SimplePanel.Expand"/>,
        /// but the layout group/content size fitter driven by that content only recomputes on
        /// Unity's next deferred layout pass. Since this panel stays enabled and only toggles
        /// its CanvasGroup alpha, that pass would otherwise land a frame late — sized for the
        /// *previous* hover instead of this one. Force it here, before the CanvasGroup starts
        /// fading in.</summary>
        protected override void BeforeAppear()
        {
            base.BeforeAppear();

            (transform as RectTransform).RefreshContentFitter();
        }
    }
}
