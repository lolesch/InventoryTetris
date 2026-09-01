using System;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// The runtime reads a display needs about a stored item - footprint, stack limit,
    /// name, rarity colour - resolved from an <see cref="ItemInstance"/> plus the catalog
    /// its definition lives in. The scattered <c>static</c> switches on <c>AbstractItem</c>
    /// (<c>GetRarityColor</c>, <c>GetDimensions</c>) collapse onto this one type.
    ///
    /// This is the only type in the assembly that names <see cref="UnityEngine"/> value
    /// types (<see cref="Color"/>, <see cref="Vector2Int"/>): it exists to feed the
    /// displays. The roll path - <see cref="ItemDefinition"/>, <see cref="ItemInstance"/>,
    /// the generator - does not go through here.
    /// </summary>
    public readonly struct ItemView
    {
        private readonly ItemInstance instance;
        private readonly ItemDefinition definition;

        private ItemView(ItemInstance instance, ItemDefinition definition)
        {
            this.instance = instance;
            this.definition = definition;
        }

        /// <summary>
        /// Pairs an instance with its definition from <paramref name="catalog"/>. Throws
        /// <see cref="KeyNotFoundException"/> (via the catalog) when the instance's
        /// definition id is not in the catalog - a stored item whose template was deleted
        /// fails here, loudly, instead of rendering as a blank.
        /// </summary>
        public static ItemView Resolve(ItemInstance instance, IItemCatalog catalog)
        {
            if (instance is null)
                throw new ArgumentNullException(nameof(instance));
            if (catalog is null)
                throw new ArgumentNullException(nameof(catalog));

            return new ItemView(instance, catalog.Definition(instance.DefinitionId));
        }

        /// <summary>The instance this view resolves.</summary>
        public ItemInstance Instance => instance;

        /// <summary>The definition the instance was rolled from.</summary>
        public ItemDefinition Definition => definition;

        /// <summary>The grid shape the item occupies, from its definition.</summary>
        public ItemSize Footprint => definition.Footprint;

        /// <summary>The footprint as a width/height cell count.</summary>
        public Vector2Int Dimensions => DimensionsOf(definition.Footprint);

        /// <summary>How many fit in one cell, from the definition.</summary>
        public uint StackLimit => definition.BaseStackLimit;

        /// <summary>The instance's rarity tier.</summary>
        public ItemRarity Rarity => instance.Rarity;

        /// <summary>The tint a display draws the item's name and border in.</summary>
        public Color RarityColor => RarityColorOf(instance.Rarity);

        /// <summary>A human-readable label - rarity plus the category-specific type.</summary>
        public string DisplayName => NameOf(instance, definition);

        /// <summary>The cell count for a footprint. Was <c>AbstractItem.GetDimensions</c>.</summary>
        public static Vector2Int DimensionsOf(ItemSize size) => size switch
        {
            ItemSize.NONE => Vector2Int.zero,

            ItemSize.OneByOne => new Vector2Int(1, 1),
            ItemSize.OneByTwo => new Vector2Int(1, 2),
            ItemSize.OneByThree => new Vector2Int(1, 3),
            ItemSize.OneByFour => new Vector2Int(1, 4),

            ItemSize.TwoByOne => new Vector2Int(2, 1),
            ItemSize.TwoByTwo => new Vector2Int(2, 2),
            ItemSize.TwoByThree => new Vector2Int(2, 3),
            ItemSize.TwoByFour => new Vector2Int(2, 4),

            _ => Vector2Int.zero,
        };

        /// <summary>The tint for a rarity tier. Was <c>AbstractItem.GetRarityColor</c> - same values.</summary>
        public static Color RarityColorOf(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Magic => new Color(0f, 0.75f, 1f, 1f),  // blue
            ItemRarity.Rare => Color.yellow,
            ItemRarity.Unique => new Color(1f, 0.35f, 0f, 1f), // orange

            ItemRarity.NoDrop => Color.clear,
            _ => Color.clear,
        };

        private static string NameOf(ItemInstance instance, ItemDefinition definition) => definition.Category switch
        {
            ItemCategory.Equipment => $"{instance.Rarity} {definition.EquipmentType}",
            ItemCategory.Consumable => $"{instance.Rarity} {definition.ConsumableType}",
            ItemCategory.Currency => definition.CurrencyType.ToString(),
            _ => definition.Id,
        };
    }
}
