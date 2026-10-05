using Submodules.Utility.Services;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Distributions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Probability;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The item service (issue #109), built from <see cref="GameConfig"/> by <see cref="GameBoot"/>.
    /// It owns the authored catalog, the global loot table (<see cref="DistributionLootTable"/>) and
    /// the coin tables, and delegates every item roll to a pure <see cref="ItemGenerator"/>. Every
    /// other draw it makes - the rarity of a debug roll, a coin denomination, a pile size, a
    /// reservoir pick - comes off the same injected <see cref="IRollSource"/>, so a roll is a
    /// deterministic unit test and nothing here reaches <c>UnityEngine.Random</c>.
    ///
    /// It holds no reference to the Hero: the roll bonuses arrive as arguments. Replaces
    /// <c>ItemProvider</c> and the static <c>ItemView.Catalog</c>.
    /// </summary>
    public sealed class ItemService : IItemService
    {
        private readonly GameConfig config;
        private readonly IRollSource rolls;
        private readonly ItemGenerator generator;
        private readonly DistributionLootTable lootTable;

        /// <summary>Call sites at the Unity edge read the service as before: <c>ItemService.Instance.RollLoot(...)</c>.</summary>
        public static IItemService Instance => ServiceLocator.Get<IItemService>();

        /// <summary>Throws when <paramref name="config"/> lacks something a roll needs, so a mis-authored
        /// asset fails at boot rather than as a roll that quietly returns nothing.</summary>
        public ItemService(GameConfig config, IRollSource rolls)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
            this.rolls = rolls ?? throw new ArgumentNullException(nameof(rolls));

            Require(config.Catalog, nameof(GameConfig.Catalog));
            Require(config.ItemCategoryDistribution, nameof(GameConfig.ItemCategoryDistribution));
            Require(config.ItemRarityDistribution, nameof(GameConfig.ItemRarityDistribution));
            Require(config.CurrencyTypeDistribution, nameof(GameConfig.CurrencyTypeDistribution));
            Require(config.CurrencyDropTable, nameof(GameConfig.CurrencyDropTable));
            Require(config.ItemTypeData, nameof(GameConfig.ItemTypeData));

            lootTable = new DistributionLootTable(config.ItemCategoryDistribution, config.ItemRarityDistribution);
            generator = new ItemGenerator(config.Catalog, rolls);
        }

        private static void Require(UnityEngine.Object asset, string field)
        {
            if (asset == null)
                throw new InvalidOperationException(
                    $"{nameof(GameConfig)}.{field} is not assigned - {nameof(ItemService)} cannot roll without it.");
        }

        public IItemCatalog Catalog => config.Catalog;

        public ItemView View(ItemInstance item) => ItemView.Resolve(item, config.Catalog);

        // ── loot ────────────────────────────────────────────────────────────

        public List<Package> RollLoot(uint amount = 1u, float magicFind = 0f, float itemQuantity = 0f)
        {
            amount += (uint)(Mathf.Max(0f, itemQuantity) / 100f); // TODO: requires a better formula

            var loot = new List<Package>();

            IReadOnlyList<ItemInstance> instances;
            try
            {
                instances = generator.RollLoot(new RollContext(lootTable, sourceLevel: 0, magicFind), (int)amount);
            }
            catch (InvalidOperationException e)
            {
                // "the loot table carries no drop mass" - a misconfigured distribution. Logged
                // rather than thrown so a kill does not break the death handler; a genuine bug
                // (NRE from the generator) still propagates loudly.
                Debug.LogError($"{nameof(ItemService)}: the loot table cannot roll - {e.Message}");
                return loot;
            }

            for (var i = 0; i < instances.Count; i++)
            {
                var package = ToPackage(instances[i]);
                if (package.IsValid)
                    loot.Add(package);
            }

            return loot;
        }

        /// <summary>
        /// Turns a rolled instance into a stored package. A currency instance - already a single
        /// coin on its denomination's Rarity (<see cref="Currency.RarityOf"/>), because
        /// <see cref="ItemGenerator"/> stamps it - gets a pile size from the drop table; anything
        /// else is one item.
        /// </summary>
        private Package ToPackage(ItemInstance instance)
        {
            var definition = config.Catalog.Definition(instance.DefinitionId);

            if (definition.Category != ItemCategory.Currency)
                return new Package(null, instance, 1u);

            var pile = RollPileAmount(definition.CurrencyType);
            return pile == 0u
                ? default
                : new Package(null, instance, pile);
        }

        // ── currency ────────────────────────────────────────────────────────

        public Package RollCurrency()
        {
            var (type, amount) = RollPile();

            return amount == 0u ? default : new Package(null, MintCurrency(type), amount);
        }

        /// <summary>
        /// One coin Pile: the denomination off the authored odds, then its size off the drop table.
        /// <c>(NONE, 0)</c> when the odds' fail bucket came up, which <c>LootFlow</c> reads as "no
        /// coins this kill" - and which draws no amount.
        /// </summary>
        public (CurrencyType Type, uint Amount) RollPile()
        {
            var type = ProbabilityTable<CurrencyType>.Sample(config.CurrencyTypeDistribution.Probabilities, rolls.Next());

            return type == CurrencyType.NONE
                ? (CurrencyType.NONE, 0u)
                : (type, RollPileAmount(type));
        }

        private uint RollPileAmount(CurrencyType type)
        {
            var range = config.CurrencyDropTable.RangeFor(type);
            return range == Vector2Int.zero ? 0u : CurrencyDropRoll.Amount(range.x, range.y, rolls.Next());
        }

        /// <summary>
        /// A single coin of <paramref name="type"/> as an <see cref="ItemInstance"/> - no affixes, a
        /// rarity from the denomination ladder (<c>Currency.RarityOf</c>: iron Common, copper Magic,
        /// silver Rare, gold Unique), item level 0. The callers that pay out change and sale
        /// proceeds mint their coins here.
        /// </summary>
        public ItemInstance MintCurrency(CurrencyType type)
        {
            var definition = DefinitionOfCurrency(type);
            return definition == null
                ? null
                : ItemInstance.Coin(definition.Id, type);
        }

        private ItemDefinition DefinitionOfCurrency(CurrencyType type)
        {
            foreach (var definition in config.Catalog.OfCategory(ItemCategory.Currency))
                if (definition.CurrencyType == type)
                    return definition;

            Debug.LogError($"{nameof(ItemService)}: the catalog has no currency definition for {type}");
            return null;
        }

        // ── debug helpers (the DebugPanel buttons, the Restock) ──────

        public ItemInstance Create(string definitionId, ItemRarity rarity) =>
            generator.Roll(config.Catalog.Definition(definitionId), rarity, 0);

        public ItemInstance RollEquipment(float magicFind = 0f) =>
            RollFrom(PickDefinition(ItemCategory.Equipment, _ => true), magicFind);

        public ItemInstance RollEquipment(EquipmentType type, float magicFind = 0f) =>
            RollFrom(PickDefinition(ItemCategory.Equipment, d => EquipmentTypeMatches(type, d.EquipmentType)), magicFind);

        public ItemInstance RollConsumable(float magicFind = 0f) =>
            RollFrom(PickDefinition(ItemCategory.Consumable, _ => true), magicFind);

        public ItemInstance RollConsumable(ConsumableType type, float magicFind = 0f) =>
            RollFrom(PickDefinition(ItemCategory.Consumable, d => d.ConsumableType == type), magicFind);

        private ItemInstance RollFrom(ItemDefinition definition, float magicFind)
        {
            if (definition == null)
                return null;

            var odds = RarityCascade.Apply(lootTable.RarityOdds, Mathf.Max(0f, magicFind));
            var rarity = ProbabilityTable<ItemRarity>.Sample(odds, rolls.Next());
            return rarity == ItemRarity.NoDrop ? null : generator.Roll(definition, rarity, 0);
        }

        /// <summary>
        /// Uniform reservoir sample of the catalog's definitions in a category that pass
        /// <paramref name="filter"/> - the debug-helper mirror of <c>ItemGenerator.PickDefinition</c>
        /// (base items and uniques both eligible).
        /// </summary>
        private ItemDefinition PickDefinition(ItemCategory category, Func<ItemDefinition, bool> filter)
        {
            ItemDefinition chosen = null;
            var seen = 0;

            foreach (var candidate in config.Catalog.OfCategory(category))
            {
                if (!filter(candidate))
                    continue;

                seen++;
                var replace = rolls.Next() * seen < 1f;
                if (replace || chosen == null)
                    chosen = candidate;
            }

            if (chosen == null)
                Debug.LogWarning($"{nameof(ItemService)}: the catalog has no {category} definition matching the request");

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

        // ── icons ───────────────────────────────────────────────────────────

        // The order is the one GameConfig documents: Copper, Iron, Silver, Gold.
        public Sprite GetIcon(CurrencyType currencyType)
        {
            var icons = config.CurrencyIcons;

            return currencyType switch
            {
                CurrencyType.Copper => icons.Count > 0 ? icons[0] : null,
                CurrencyType.Iron => icons.Count > 1 ? icons[1] : null,
                CurrencyType.Silver => icons.Count > 2 ? icons[2] : null,
                CurrencyType.Gold => icons.Count > 3 ? icons[3] : null,

                CurrencyType.NONE => null,
                _ => null,
            };
        }

        public Sprite GetStatIcon(StatName stat) => config.ItemTypeData.GetStatIcon(stat);
    }
}
