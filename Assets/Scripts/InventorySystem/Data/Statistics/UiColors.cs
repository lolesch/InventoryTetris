using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// Every colour (and see-through alpha) the UI paints with, so the look is one edit. Colours a
    /// prefab owns stay on the prefab; these are the ones set from code.
    /// </summary>
    public static class UiColors
    {
        /// <summary>A swap that raises a stat.</summary>
        public static readonly Color Better = Color.green;

        /// <summary>A swap that lowers a stat.</summary>
        public static readonly Color Worse = Color.red;

        /// <summary>A swap that changes nothing, or has nothing to compare.</summary>
        public static readonly Color Neutral = Color.white;

        /// <summary>Secondary text: a roll range, a stat breakdown, a currency total.</summary>
        public static readonly Color Muted = Color.gray;

        /// <summary>The drag scrim while the cursor is over a slot that would turn the drop away.</summary>
        public static readonly Color Forbidden = Color.red;

        /// <summary>A shelf slot the hero cannot pay for. See-through, so the slot underneath still reads.</summary>
        public static readonly Color Unaffordable = new(1f, 0f, 0f, 0.2f);

        /// <summary>Darkens a rarity colour into the item's background: it multiplies, so it keeps the hue.</summary>
        public static readonly Color SlotTint = Color.gray * Color.gray;

        /// <summary>
        /// Opacity of an item shown as a stand-in rather than held: the off-hand slot under a
        /// two-hander. Applied to the whole item display, so icon, frame and background fade together.
        /// </summary>
        public const float GhostedAlpha = 0.4f;

        public static Color Of(ComparisonVerdict verdict) => verdict switch
        {
            ComparisonVerdict.Better => Better,
            ComparisonVerdict.Worse => Worse,
            _ => Neutral,
        };

        /// <summary>The tint for a rarity tier.</summary>
        public static Color Rarity(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Magic => new Color(0f, 0.75f, 1f, 1f),  // blue
            ItemRarity.Rare => Color.yellow,
            ItemRarity.Unique => new Color(1f, 0.35f, 0f, 1f), // orange

            ItemRarity.NoDrop => Color.clear,
            _ => Color.clear,
        };

        /// <summary>The background an item of this rarity sits on.</summary>
        public static Color SlotBackground(Color rarityColor) => rarityColor * SlotTint;
    }
}
