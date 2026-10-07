using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Distributions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Character;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The authored data every service is built from: one root asset, loaded from
    /// <c>Resources</c> before the first scene by <see cref="GameBoot"/>, so it cannot differ per
    /// scene (ADR-0015). Read-only by construction - every property has a private setter -
    /// because with domain reload disabled a runtime write to a <see cref="ScriptableObject"/>
    /// survives Stop. Live, mutable values (the Behaviour Profile sliders, a Wallet) belong to the
    /// Hero and the World, never here; the simulation values below are only their <em>defaults</em>.
    ///
    /// Carries what the scene-scoped providers author today (issue #108): the item catalog, the
    /// category and rarity distributions, the currency distribution and drop table, the stat-icon
    /// data, the currency icons, the container sizes, the simulation tuning defaults and the
    /// Locations. The item data is live: <see cref="ItemService"/> is built from it (#109). So are
    /// the default hero, the container sizes and the Behaviour Profile defaults: <see cref="SessionBuilder"/>
    /// builds the Hero and the World from them (#112), and <see cref="SimulationService"/> reads the
    /// cast cost and the Death penalty fractions from it (#113). The Locations are still read from
    /// their providers' own copies until each is replaced, so a value changed here is not live
    /// until then.
    /// </summary>
    public sealed class GameConfig : ScriptableObject
    {
        /// <summary>The <c>Resources</c> key the boot loads: <c>Assets/Resources/GameConfig.asset</c>.</summary>
        public const string ResourceKey = "GameConfig";

        [field: Header("Hero")]
        [field: SerializeField, Tooltip("The template the boot builds the first Hero from, so a bare scene works with no menu.")]
        public HeroData DefaultHero { get; private set; }

        [SerializeField, Tooltip("The templates a new hero can be created from, in the order a selection screen offers them. The default hero is always offered first, listed or not.")]
        private HeroData[] heroes = System.Array.Empty<HeroData>();

        /// <summary>The templates a hero can be created from: the default hero first, then the listed ones, each once.</summary>
        public IReadOnlyList<HeroData> Heroes
        {
            get
            {
                var roster = new List<HeroData>();

                if (DefaultHero != null)
                    roster.Add(DefaultHero);

                foreach (var hero in heroes ?? System.Array.Empty<HeroData>())
                {
                    if (hero != null && !roster.Contains(hero))
                        roster.Add(hero);
                }

                return roster.AsReadOnly();
            }
        }

        /// <summary>
        /// The template whose <see cref="HeroData.Id"/> is <paramref name="id"/>, or the default hero when
        /// the id is empty or names a template that is no longer authored: a save must still load.
        /// </summary>
        public HeroData FindHero(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                foreach (var hero in Heroes)
                {
                    if (string.Equals(hero.Id, id, System.StringComparison.Ordinal))
                        return hero;
                }
            }

            return DefaultHero;
        }

        [field: Header("Items")]
        [field: SerializeField, Tooltip("Stat-icon lookup for the character and item-stat displays. Not on the roll path.")]
        public ItemTypeData ItemTypeData { get; private set; }

        [field: SerializeField] public ItemCatalogAsset Catalog { get; private set; }

        [field: Header("Loot table")]
        [field: SerializeField] public ItemCategoryDistribution ItemCategoryDistribution { get; private set; }
        [field: SerializeField] public ItemRarityDistribution ItemRarityDistribution { get; private set; }

        [field: Header("Currency")]
        [field: SerializeField] public CurrencyTypeDistribution CurrencyTypeDistribution { get; private set; }
        [field: SerializeField] public CurrencyDropTable CurrencyDropTable { get; private set; }

        // Order is the one ItemService.GetIcon indexes: Copper, Iron, Silver, Gold.
        // TODO: make it a serialized dictionary
        [SerializeField] private List<Sprite> currencyIcons = new();
        public IReadOnlyList<Sprite> CurrencyIcons => currencyIcons.AsReadOnly();

        [field: Header("Container sizes")]
        [field: SerializeField] public Vector2Int EquipmentSize { get; private set; } = new(14, 1);
        [field: SerializeField] public Vector2Int InventorySize { get; private set; } = new(10, 6);
        [field: SerializeField] public Vector2Int StashSize { get; private set; } = new(10, 13);

        [field: SerializeField, Tooltip("One size for every Supply shelf (the Vendor's and the Healer's).")]
        public Vector2Int SupplySize { get; private set; } = new(10, 7);

        [field: SerializeField, Tooltip("The Sold container (epic #124).")]
        public Vector2Int SoldSize { get; private set; } = new(10, 7);

        [field: Header("Simulation: Behaviour Profile defaults")]
        [field: SerializeField, Range(1f, 8f)] public float SimSpeed { get; private set; } = 1f;
        [field: SerializeField, Min(1)] public int Engagement { get; private set; } = 3;
        [field: SerializeField, Range(0f, 1f)] public float RetreatHealthFraction { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float RecallBagFillFraction { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f)] public float CastThreshold { get; private set; }
        [field: SerializeField] public ItemRarity LootFilterMinimum { get; private set; } = ItemRarity.Common;

        [field: Header("Simulation: tuning")]
        [field: SerializeField, Tooltip("MVP flat Cast cost the hero reports - no gear stat for it yet (ADR-0010).")]
        public float CastCost { get; private set; } = 16f;

        [field: SerializeField, Range(0f, 1f)] public float XpLossFraction { get; private set; } = 0.25f;
        [field: SerializeField, Range(0f, 1f)] public float CurrencyFeeFraction { get; private set; } = 0.5f;

        [field: Header("Locations")]
        [SerializeField] private LocationConfig[] locations = System.Array.Empty<LocationConfig>();
        public IReadOnlyList<LocationConfig> Locations => System.Array.AsReadOnly(locations);
    }
}
