using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Distributions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The Unity entry point to the loot roll. Since the Phase 1 cutover (issue #8) it owns
    /// three things - the authored <see cref="ItemCatalogAsset"/>, the two distribution
    /// <see cref="AbstractProbabilityDistribution"/>s that form the global loot table
    /// (<see cref="DistributionLootTable"/>), and the currency drop table - and delegates
    /// every roll to a pure <see cref="ItemGenerator"/>. The ~30 <c>GenerateRandomX</c> methods that
    /// were a hand-unrolled decision tree are gone: adding an item type is a new definition
    /// in the catalog, not another switch arm.
    ///
    /// It also publishes the catalog to <see cref="ItemView.Catalog"/> so the container core
    /// and the slot displays - still in <c>Assembly-CSharp</c> until the #15 extraction - can
    /// resolve a stored <see cref="ItemInstance"/> to its template.
    /// </summary>
    public class ItemProvider : AbstractProvider<ItemProvider>, ICurrencyMinter
    {
        [Tooltip("Stat-icon lookup for the character and item-stat displays. Not on the roll path.")]
        public ItemTypeData ItemTypeData;

        [Header("Catalog")]
        [SerializeField] private ItemCatalogAsset catalog;

        [Header("Loot table")]
        [SerializeField] private ItemCategoryDistribution itemCategoryDistribution;
        [SerializeField] private ItemRarityDistribution itemRarityDistribution;

        [Header("Currency")]
        [SerializeField] private CurrencyTypeDistribution currencyTypeDistribution;
        [SerializeField] private CurrencyDropTable currencyDropTable;
        // TODO: make it a serialized dictionary
        [SerializeField] private List<Sprite> CurrencyIcons = new();

        private ItemGenerator generator;
        private DistributionLootTable lootTable;

        private void Awake() => EnsureInitialized();

        /// <summary>
        /// The authored catalog, for the call sites that resolve a stored instance back to
        /// its definition. Same object as <see cref="ItemView.Catalog"/>.
        /// </summary>
        public IItemCatalog Catalog
        {
            get
            {
                EnsureInitialized();
                return catalog;
            }
        }

        private void EnsureInitialized()
        {
            if (generator != null)
                return;

            if (catalog == null)
            {
                Debug.LogError($"{nameof(ItemProvider)}: no {nameof(catalog)} assigned - no item can be rolled", this);
                return;
            }

            lootTable = new DistributionLootTable(itemCategoryDistribution, itemRarityDistribution);
            generator = new ItemGenerator(catalog, new UnityRollSource());
            ItemView.Catalog = catalog;
        }

        // ── loot ────────────────────────────────────────────────────────────

        /// <summary>
        /// Rolls <paramref name="amount"/> drops (plus the player's <c>IncreasedItemQuantity</c>
        /// bonus) against the global loot table. A currency drop comes back as a rolled coin
        /// pile; everything else is a single item. Was <c>GenerateRandomLoot</c>.
        /// </summary>
        public List<Package> RollLoot(uint amount = 1u)
        {
            EnsureInitialized();

            var loot = new List<Package>();
            if (generator == null)
                return loot;

            AddBonusDrops(ref amount);

            IReadOnlyList<ItemInstance> instances;
            try
            {
                instances = generator.RollLoot(LootContext(), (int)amount);
            }
            catch (InvalidOperationException e)
            {
                // "the loot table carries no drop mass" - a misconfigured distribution. Logged
                // rather than thrown so a kill does not break the death handler; a genuine bug
                // (NRE from the generator) still propagates loudly.
                Debug.LogError($"{nameof(ItemProvider)}: the loot table cannot roll - {e.Message}", this);
                return loot;
            }

            for (var i = 0; i < instances.Count; i++)
            {
                var package = ToPackage(instances[i]);
                if (package.IsValid)
                    loot.Add(package);
            }

            return loot;

            static void AddBonusDrops(ref uint amount)
            {
                var bonusDrops = CharacterProvider.Instance.Player.GetStatValue(StatName.IncreasedItemQuantity);
                amount += (uint)(bonusDrops / 100f); // TODO: requires a better formula
            }
        }

        /// <summary>
        /// Turns a rolled instance into a stored package. A currency instance is re-minted as
        /// a Common coin (loot coins never carry a rarity tint, as before) with a pile size
        /// from the drop table; anything else is one item.
        /// </summary>
        private Package ToPackage(ItemInstance instance)
        {
            var definition = catalog.Definition(instance.DefinitionId);

            if (definition.Category != ItemCategory.Currency)
                return new Package(null, instance, 1u);

            var pile = currencyDropTable != null ? currencyDropTable.RollAmount(definition.CurrencyType) : 1u;
            return pile == 0u
                ? default
                : new Package(null, MintCurrency(definition.CurrencyType), pile);
        }

        // ── currency ────────────────────────────────────────────────────────

        /// <summary>Rolls a coin type, then a pile size for it. Was <c>GenerateRandomCurrency</c>.</summary>
        public Package RollCurrency()
        {
            EnsureInitialized();

            if (currencyDropTable == null)
            {
                Debug.LogError($"{nameof(ItemProvider)}: {nameof(currencyDropTable)} is not assigned - no currency will drop", this);
                return default;
            }

            var type = currencyTypeDistribution.Roll();
            var amount = currencyDropTable.RollAmount(type);

            return amount == 0u ? default : new Package(null, MintCurrency(type), amount);
        }

        /// <summary>
        /// A single coin of <paramref name="type"/> as an <see cref="ItemInstance"/> - no
        /// affixes, Common, item level 0. Was <c>GenerateCurrency</c>; the callers that pay
        /// out change and sale proceeds mint their coins here.
        /// </summary>
        public ItemInstance MintCurrency(CurrencyType type)
        {
            EnsureInitialized();

            var definition = DefinitionOfCurrency(type);
            return definition == null
                ? null
                : new ItemInstance(definition.Id, ItemRarity.Common, 0, null);
        }

        // ── debug helpers (the InventoryProvider buttons) ───────────────────

        /// <summary>Rolls a random equipment item of any type. Was <c>GenerateRandomEquipment</c>.</summary>
        public ItemInstance RollEquipment() => RollFrom(PickDefinition(ItemCategory.Equipment, _ => true));

        /// <summary>
        /// Rolls a random equipment item of <paramref name="type"/>. <paramref name="type"/>
        /// may be a concrete type (<c>Belt</c>) or a category marker (<c>ONEHANDEDWEAPONS</c>),
        /// matching the old <c>GenerateRandomOfEquipmentType</c> switch.
        /// </summary>
        public ItemInstance RollEquipment(EquipmentType type) =>
            RollFrom(PickDefinition(ItemCategory.Equipment, d => EquipmentTypeMatches(type, d.EquipmentType)));

        /// <summary>Rolls a random consumable of <paramref name="type"/>. Was <c>GenerateRandomOfConsumableType</c>.</summary>
        public ItemInstance RollConsumable(ConsumableType type) =>
            RollFrom(PickDefinition(ItemCategory.Consumable, d => d.ConsumableType == type));

        private ItemInstance RollFrom(ItemDefinition definition)
        {
            EnsureInitialized();

            if (generator == null || definition == null)
                return null;

            var rarity = itemRarityDistribution.Roll(PlayerMagicFind());
            return rarity == ItemRarity.NoDrop ? null : generator.Roll(definition, rarity, 0);
        }

        // ── icons ───────────────────────────────────────────────────────────

        public Sprite GetIcon(CurrencyType currencyType) => currencyType switch
        {
            CurrencyType.Copper => CurrencyIcons.Count > 0 ? CurrencyIcons[0] : null,
            CurrencyType.Iron => CurrencyIcons.Count > 1 ? CurrencyIcons[1] : null,
            CurrencyType.Silver => CurrencyIcons.Count > 2 ? CurrencyIcons[2] : null,
            CurrencyType.Gold => CurrencyIcons.Count > 3 ? CurrencyIcons[3] : null,

            CurrencyType.NONE => null,
            _ => null,
        };

        // ── internals ───────────────────────────────────────────────────────

        private RollContext LootContext() => new(lootTable, sourceLevel: 0, magicFind: PlayerMagicFind());

        private static float PlayerMagicFind() =>
            CharacterProvider.Instance.Player.GetStatValue(StatName.IncreasedItemRarity);

        private ItemDefinition DefinitionOfCurrency(CurrencyType type)
        {
            foreach (var definition in catalog.OfCategory(ItemCategory.Currency))
                if (definition.CurrencyType == type)
                    return definition;

            Debug.LogError($"{nameof(ItemProvider)}: the catalog has no currency definition for {type}", this);
            return null;
        }

        /// <summary>
        /// Uniform reservoir sample of the catalog's definitions in a category that pass
        /// <paramref name="filter"/> - the debug-helper mirror of <c>ItemGenerator.PickDefinition</c>
        /// (base items and uniques both eligible).
        /// </summary>
        private ItemDefinition PickDefinition(ItemCategory category, Func<ItemDefinition, bool> filter)
        {
            if (catalog == null)
            {
                Debug.LogError($"{nameof(ItemProvider)}: no {nameof(catalog)} assigned - cannot pick a {category} definition", this);
                return null;
            }

            ItemDefinition chosen = null;
            var seen = 0;

            foreach (var candidate in catalog.OfCategory(category))
            {
                if (!filter(candidate))
                    continue;

                seen++;
                if (chosen == null || UnityEngine.Random.value * seen < 1f)
                    chosen = candidate;
            }

            if (chosen == null)
                Debug.LogWarning($"{nameof(ItemProvider)}: the catalog has no {category} definition matching the request");

            return chosen;
        }

        /// <summary>
        /// Whether a definition's <paramref name="have"/> type satisfies a requested
        /// <paramref name="want"/> that may be a category marker
        /// (<c>ARMAMENTS</c>/<c>ONEHANDEDWEAPONS</c>/<c>TWOHANDEDWEAPONS</c>/<c>OFFHANDS</c>/<c>JEWELRY</c>).
        /// The ranges are the ones the enum's own tooltips document.
        /// </summary>
        private static bool EquipmentTypeMatches(EquipmentType want, EquipmentType have) => want switch
        {
            EquipmentType.NONE => true,
            EquipmentType.ARMAMENTS => have > EquipmentType.ARMAMENTS && have < EquipmentType.ONEHANDEDWEAPONS,
            EquipmentType.ONEHANDEDWEAPONS => have > EquipmentType.ONEHANDEDWEAPONS && have < EquipmentType.TWOHANDEDWEAPONS,
            EquipmentType.TWOHANDEDWEAPONS => have > EquipmentType.TWOHANDEDWEAPONS && have < EquipmentType.OFFHANDS,
            EquipmentType.OFFHANDS => have > EquipmentType.OFFHANDS && have < EquipmentType.JEWELRY,
            EquipmentType.JEWELRY => have > EquipmentType.JEWELRY,
            _ => have == want,
        };
    }
}
