using System;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// The runtime reads a display needs about a stored item - footprint, stack limit,
    /// name, rarity colour, icon, sell value - resolved from an <see cref="ItemInstance"/>
    /// plus the catalog its definition lives in. The scattered <c>static</c> switches on
    /// <c>AbstractItem</c> (<c>GetRarityColor</c>, <c>GetDimensions</c>, <c>CalculateValue</c>)
    /// collapsed onto this one type when <c>AbstractItem</c> was deleted in the cutover
    /// (issue #8).
    ///
    /// This type deals in <see cref="Color"/>, <see cref="Vector2Int"/> and
    /// <see cref="Sprite"/> because it exists to feed the displays - the roll path
    /// (<see cref="ItemDefinition"/>, <see cref="ItemInstance"/>, the generator) does not
    /// go through here.
    /// </summary>
    public readonly struct ItemView
    {
        /// <summary>
        /// Ambient catalog for the call sites that cannot yet take one by constructor -
        /// <c>Package</c> (a serialized struct), the container core, the slot displays, all
        /// still in the predefined <c>Assembly-CSharp</c>. <c>ItemProvider.Awake</c> sets it
        /// from the wired catalog asset; an EditMode test sets a fake in <c>[SetUp]</c>. This
        /// mirrors how <c>AbstractItem</c>'s constructors reached <c>ItemProvider.Instance</c>
        /// ambiently before the cutover - the same coupling, made explicit and swappable.
        /// </summary>
        // TODO(#15): the InventorySystem.Containers extraction replaces this static with a
        // catalog injected into the container core and threaded to the displays.
        public static IItemCatalog Catalog { get; set; }

        private readonly ItemInstance instance;
        private readonly ItemDefinition definition;

        private ItemView(ItemInstance instance, ItemDefinition definition)
        {
            this.instance = instance;
            this.definition = definition;
        }

        /// <summary>
        /// Pairs an instance with its definition from <paramref name="catalog"/>. Throws
        /// <see cref="System.Collections.Generic.KeyNotFoundException"/> (via the catalog)
        /// when the instance's definition id is not in the catalog - a stored item whose
        /// template was deleted fails here, loudly, instead of rendering as a blank.
        /// </summary>
        public static ItemView Resolve(ItemInstance instance, IItemCatalog catalog)
        {
            if (instance is null)
                throw new ArgumentNullException(nameof(instance));
            if (catalog is null)
                throw new ArgumentNullException(nameof(catalog));

            return new ItemView(instance, catalog.Definition(instance.DefinitionId));
        }

        /// <summary>
        /// Resolves <paramref name="instance"/> against the ambient <see cref="Catalog"/> -
        /// the terse form for the runtime call sites. Throws
        /// <see cref="InvalidOperationException"/> when no catalog has been set (the game
        /// ran without an <c>ItemProvider</c>, or a test forgot its <c>[SetUp]</c>).
        /// </summary>
        public static ItemView Of(ItemInstance instance)
        {
            if (Catalog is null)
                throw new InvalidOperationException(
                    "ItemView.Catalog is not set - ItemProvider.Awake sets it at runtime; a test must set it in [SetUp]");

            return Resolve(instance, Catalog);
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

        /// <summary>
        /// The item's art. Lives on the authored adapter only - the roll-path
        /// <see cref="ItemDefinition"/> interface names no <see cref="Sprite"/> - so a test
        /// fake resolves to <c>null</c> here.
        /// </summary>
        public Sprite Icon => (definition as ItemDefinitionAsset)?.Icon;

        /// <summary>
        /// What a vendor pays for the instance, in base units. Carried verbatim from
        /// <c>AbstractItem.CalculateValue</c> / <c>CurrencyItem.CalculateValue</c>: a coin is
        /// worth its denomination, everything else is the sum of <c>|value * goldRatio|</c>
        /// over its affixes. User story 14 (a value model resting on a pinned affix system)
        /// is still deferred - this is the switch it replaces, moved not redesigned.
        /// </summary>
        public float SellValue => definition.Category == ItemCategory.Currency
            ? CurrencyValueOf(definition.CurrencyType)
            : AffixValueOf(instance);

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

        /// <summary>A coin's worth in base units. Was <c>CurrencyItem.CalculateValue</c>.</summary>
        private static float CurrencyValueOf(CurrencyType currency) => currency switch
        {
            CurrencyType.Iron => 1f,
            CurrencyType.Copper => Currency.ironToCopper,
            CurrencyType.Silver => Currency.ironToSilver,
            CurrencyType.Gold => Currency.ironToGold,

            CurrencyType.NONE => 0f,
            _ => 0f,
        };

        /// <summary>
        /// The affix-derived value. Was <c>AbstractItem.CalculateValue</c> - the
        /// per-<see cref="StatName"/> gold ratios and the <see cref="Mathf.Abs"/> are
        /// unchanged; a <c>NOTE</c> in the original says the ratios should vary by modifier
        /// type, which they still do not.
        /// </summary>
        private static float AffixValueOf(ItemInstance instance)
        {
            var amount = 0f;

            foreach (var affix in instance.Affixes)
            {
                var goldRatio = affix.Stat switch
                {
                    StatName.AttackSpeed => 25f,
                    StatName.PhysicalDamage => 35f,
                    StatName.MagicalDamage => 21.75f,
                    StatName.Health => 2.67f,
                    StatName.HealthRegeneration => 3f,
                    StatName.Armor => 20f,
                    StatName.MagicResist => 18f,
                    StatName.MovementSpeed => 12,
                    StatName.Resource => 1.4f,
                    StatName.ResourceRegeneration => 5f,

                    StatName.ArmorPenetration => 41.67f,
                    StatName.MagicPenetration => 54.33f,

                    StatName.Shield => 2.67f,
                    StatName.IncreasedItemRarity => 0f,
                    StatName.IncreasedItemQuantity => 0f,

                    StatName.Experience => 0f,
                    _ => 0f,
                };

                amount += Mathf.Abs(affix.Modifier.Value * goldRatio);
            }

            return amount;
        }
    }
}
