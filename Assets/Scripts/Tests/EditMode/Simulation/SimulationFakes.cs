using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// <see cref="IRollSource"/> that hands back a fixed script of rolls in order and throws
    /// once it runs dry — a test that miscounts how many rolls the Encounter consumes fails
    /// loudly. (Same shape as <c>InventorySystem.Items.Tests</c>' internal copy; each test
    /// assembly owns its own so neither depends on the other's test code.)
    /// </summary>
    internal sealed class QueuedRollSource : IRollSource
    {
        private readonly float[] _rolls;
        private int _next;

        public QueuedRollSource(params float[] rolls) => _rolls = rolls ?? Array.Empty<float>();

        public int Consumed => _next;

        public float Next()
        {
            if (_next >= _rolls.Length)
                throw new InvalidOperationException(
                    $"the roll script ran dry after {_rolls.Length} rolls — the path under test consumes more");
            return _rolls[_next++];
        }
    }

    /// <summary><see cref="IRollSource"/> backed by a seeded <see cref="Random"/> — repeatable.</summary>
    internal sealed class SeededRollSource : IRollSource
    {
        private readonly Random _rng;

        public SeededRollSource(int seed) => _rng = new Random(seed);

        public float Next() => (float)_rng.NextDouble();
    }

    /// <summary><see cref="IRollSource"/> that always returns the same value.</summary>
    internal sealed class ConstantRollSource : IRollSource
    {
        private readonly float _value;

        public ConstantRollSource(float value) => _value = value;

        public float Next() => _value;
    }

    /// <summary>
    /// A hand-tunable <see cref="IHeroCombatant"/>. Health and Resource are plain settable
    /// pools; <see cref="Regenerate"/> tops them up at the configured per-second rates and
    /// counts its calls; <see cref="ReceivePhysical"/> applies <see cref="ArmorPercent"/>.
    /// </summary>
    internal sealed class FakeHero : IHeroCombatant
    {
        public float MaxHealth { get; set; } = 1_000_000f;
        public float Health { get; set; } = 1_000_000f;
        public float MaxResource { get; set; } = 100f;
        public float Resource { get; set; }
        public float HealthRegenPerSecond { get; set; }
        public float ResourceRegenPerSecond { get; set; }
        public float ArmorPercent { get; set; }

        public float PhysicalDamage { get; set; } = 10f;
        public float AttackSpeed { get; set; } = 1f;
        public float MagicalDamage { get; set; } = 5f;
        public float CastCost { get; set; } = 16f;
        public int Level { get; set; } = 1;
        public float MagicFind { get; set; }
        public float IncreasedItemQuantity { get; set; }

        public float PhysicalDamageTaken { get; private set; }

        public float HealthFraction => MaxHealth <= 0f ? 0f : Clamp01(Health / MaxHealth);
        public float ResourceFraction => MaxResource <= 0f ? 0f : Clamp01(Resource / MaxResource);
        public bool IsDown => Health <= 0f;

        public void ReceivePhysical(float rawDamage)
        {
            if (rawDamage <= 0f) return;
            var dealt = rawDamage * (1f - ArmorPercent * 0.01f);
            PhysicalDamageTaken += dealt;
            Health = Math.Max(0f, Health - dealt);
        }

        public void ReceiveMagical(float rawDamage)
        {
            if (rawDamage <= 0f) return;
            Health = Math.Max(0f, Health - rawDamage);
        }

        public void SpendResource(float amount) => Resource = Math.Max(0f, Resource - amount);

        public void Regenerate(float deltaSeconds)
        {
            if (Health > 0f)
                Health = Math.Min(MaxHealth, Health + HealthRegenPerSecond * deltaSeconds);
            Resource = Math.Min(MaxResource, Resource + ResourceRegenPerSecond * deltaSeconds);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    internal static class Profiles
    {
        /// <summary>A single enemy of one archetype, no further spawns — isolates one target.</summary>
        public static EncounterProfile Solo(EnemyArchetype archetype, int sourceLevel = 5, LootTable table = null) =>
            Group(archetype, 1, sourceLevel, table);

        /// <summary>
        /// <paramref name="count"/> enemies of one archetype, all present at open, no further
        /// spawns — the Roster is spent the moment the Encounter begins. <paramref name="table"/>
        /// defaults to <see cref="FakeLootTable.ForCategory"/>'s Equipment table — most tests
        /// never look at loot, so they should not have to build one.
        /// </summary>
        public static EncounterProfile Group(EnemyArchetype archetype, int count, int sourceLevel = 5, LootTable table = null) => new(
            sourceLevel: sourceLevel,
            packed: archetype,
            rosterBrute: new IntRange(archetype == EnemyArchetype.Brute ? count : 0),
            rosterSkirmisher: new IntRange(archetype == EnemyArchetype.Skirmisher ? count : 0),
            packBatch: new IntRange(1),
            packedSpawnWeight: 1f,
            spawnInterval: 100f,
            table: table ?? FakeLootTable.ForCategory(ItemCategory.Equipment),
            spawnJitter: 0f,
            initialSpawn: count);
    }

    // ─── loot flow (issue #24) ─────────────────────────────────────────────

    /// <summary>
    /// A settable stand-in for <see cref="LootTable"/> — two probability vectors in enum order.
    /// (Same shape as <c>InventorySystem.Items.Tests</c>' internal copy; each test assembly
    /// owns its own so neither depends on the other's test code.)
    /// </summary>
    internal sealed class FakeLootTable : LootTable
    {
        public IReadOnlyList<float> CategoryOdds { get; set; } = CategoryVector(ItemCategory.Equipment);
        public IReadOnlyList<float> RarityOdds { get; set; } = AuthoredRarityOdds();

        /// <summary>All category mass on <paramref name="category"/>; rarity per the authored table.</summary>
        public static FakeLootTable ForCategory(ItemCategory category) => new()
        {
            CategoryOdds = CategoryVector(category),
            RarityOdds = AuthoredRarityOdds(),
        };

        /// <summary>All category mass on <paramref name="category"/>, all rarity mass on <paramref name="rarity"/>.</summary>
        public static FakeLootTable Fixed(ItemCategory category, ItemRarity rarity) => new()
        {
            CategoryOdds = CategoryVector(category),
            RarityOdds = RarityVector(rarity),
        };

        /// <summary>normalize(0, 160, 80, 40, 20) - the shipped Item Rarity Distribution weights.</summary>
        public static IReadOnlyList<float> AuthoredRarityOdds() => Normalized(0f, 160f, 80f, 40f, 20f);

        public static IReadOnlyList<float> CategoryVector(ItemCategory category) =>
            OneHot(Array.IndexOf((ItemCategory[])Enum.GetValues(typeof(ItemCategory)), category),
                Enum.GetValues(typeof(ItemCategory)).Length);

        public static IReadOnlyList<float> RarityVector(ItemRarity rarity) =>
            OneHot(Array.IndexOf((ItemRarity[])Enum.GetValues(typeof(ItemRarity)), rarity),
                Enum.GetValues(typeof(ItemRarity)).Length);

        public static IReadOnlyList<float> Normalized(params float[] weights)
        {
            var sum = 0f;
            foreach (var w in weights)
                sum += w > 0f ? w : 0f;

            var result = new float[weights.Length];
            if (sum <= 0f)
                return result;

            for (var i = 0; i < weights.Length; i++)
                result[i] = (weights[i] > 0f ? weights[i] : 0f) / sum;
            return result;
        }

        private static float[] OneHot(int index, int length)
        {
            var result = new float[length];
            if (index >= 0 && index < length)
                result[index] = 1f;
            return result;
        }
    }

    /// <summary>
    /// A settable stand-in for <see cref="ItemDefinition"/> — build one per item kind. (Same
    /// shape as <c>InventorySystem.Items.Tests</c>' internal copy.)
    /// </summary>
    internal sealed class FakeItemDefinition : ItemDefinition
    {
        public string Id { get; set; } = "fake.item";
        public ItemCategory Category { get; set; } = ItemCategory.Equipment;
        public ItemSize Footprint { get; set; } = ItemSize.OneByOne;
        public uint BaseStackLimit { get; set; } = 1u;
        public IReadOnlyList<AffixSlot> AffixPool { get; set; } = Array.Empty<AffixSlot>();
        public IReadOnlyList<CharacterStatModifier> ImplicitStats { get; set; } = Array.Empty<CharacterStatModifier>();
        public ItemRequirement Requirement { get; set; } = ItemRequirement.None;
        public bool IsUnique { get; set; }
        public IReadOnlyList<CharacterStatModifier> UniqueAffixes { get; set; } = Array.Empty<CharacterStatModifier>();
        public EquipmentType EquipmentType { get; set; } = EquipmentType.NONE;
        public ConsumableType ConsumableType { get; set; } = ConsumableType.NONE;
        public CurrencyType CurrencyType { get; set; } = CurrencyType.NONE;
    }

    /// <summary>An in-memory <see cref="IItemCatalog"/> — <c>ItemView.Catalog</c> for a test.</summary>
    internal sealed class InMemoryItemCatalog : IItemCatalog
    {
        private readonly Dictionary<string, ItemDefinition> byId = new();

        public InMemoryItemCatalog(params ItemDefinition[] definitions)
        {
            foreach (var definition in definitions)
                Add(definition);
        }

        public InMemoryItemCatalog Add(ItemDefinition definition)
        {
            byId[definition.Id] = definition;
            return this;
        }

        public ItemDefinition Definition(string id) =>
            byId.TryGetValue(id, out var definition) ? definition : throw new KeyNotFoundException(id);

        public IEnumerable<ItemDefinition> OfCategory(ItemCategory category)
        {
            foreach (var definition in byId.Values)
                if (definition.Category == category)
                    yield return definition;
        }
    }

    /// <summary>
    /// Mints a coin per denomination, ids resolved from the catalog it is handed — the test
    /// stand-in for <c>ItemProvider</c> on the <see cref="ICurrencyMinter"/> seam.
    /// </summary>
    internal sealed class FakeCurrencyMinter : ICurrencyMinter
    {
        private readonly IItemCatalog catalog;

        public FakeCurrencyMinter(IItemCatalog catalog) => this.catalog = catalog;

        public ItemInstance MintCurrency(CurrencyType type)
        {
            foreach (var definition in catalog.OfCategory(ItemCategory.Currency))
                if (definition.CurrencyType == type)
                    return new ItemInstance(definition.Id, ItemRarity.Common, 0, null);

            return null;
        }
    }

    /// <summary>
    /// A scripted <see cref="ICoinDropSource"/> — hands back a fixed queue of Piles in order,
    /// then an empty (no-coin) Pile once the queue runs dry.
    /// </summary>
    internal sealed class FakeCoinDropSource : ICoinDropSource
    {
        private readonly Queue<(CurrencyType Type, uint Amount)> _piles;

        public FakeCoinDropSource(params (CurrencyType Type, uint Amount)[] piles) =>
            _piles = new Queue<(CurrencyType, uint)>(piles);

        /// <summary>Every Pile this source has handed out, in order — for assertions on call count.</summary>
        public int Rolled { get; private set; }

        public (CurrencyType Type, uint Amount) RollPile()
        {
            Rolled++;
            return _piles.Count > 0 ? _piles.Dequeue() : (CurrencyType.NONE, 0u);
        }
    }
}
