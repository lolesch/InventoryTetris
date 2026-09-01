using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using static ToolSmiths.InventorySystem.Tests.EditMode.Items.Sample;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// <see cref="ItemGenerator"/> is the <b>Roll</b> - a <see cref="RollContext"/> in, an
    /// <see cref="ItemInstance"/> out. These pin the roll shape the foundational-rework spec
    /// and issue #6 call out: magic-find-0 reproduces the authored rarity table, affix count
    /// follows the rarity map, a rolled affix comes from the pool and is never duplicated,
    /// a unique merges its fixed list, and <c>RollLoot</c> returns exactly the count asked.
    ///
    /// No <c>ScriptableObject</c>: a <see cref="FakeItemDefinition"/>, an
    /// <see cref="InMemoryItemCatalog"/>, a <see cref="FakeLootTable"/> and a scripted or
    /// seeded <see cref="IRollSource"/>.
    /// </summary>
    [TestFixture]
    public sealed class ItemGeneratorTests
    {
        private static ItemGenerator Generator(FakeItemDefinition definition, IRollSource rolls) =>
            new(new InMemoryItemCatalog(definition), rolls);

        private static FakeItemDefinition Equipment(string id, params AffixSlot[] pool) => new()
        {
            Id = id,
            Category = ItemCategory.Equipment,
            EquipmentType = EquipmentType.Chest,
            Footprint = ItemSize.TwoByThree,
            AffixPool = pool,
        };

        // ── construction guards ──────────────────────────────────────────────

        [Test]
        public void Constructor_NullCatalog_Throws() =>
            Assert.That(() => new ItemGenerator(null, new ConstantRollSource(0f)), Throws.ArgumentNullException);

        [Test]
        public void Constructor_NullRollSource_Throws() =>
            Assert.That(() => new ItemGenerator(new InMemoryItemCatalog(), null), Throws.ArgumentNullException);

        [Test]
        public void Roll_ContextWithoutALootTable_Throws()
        {
            var generator = Generator(Equipment("chest"), new ConstantRollSource(0f));

            Assert.That(() => generator.Roll(default), Throws.ArgumentException);
        }

        // ── the basics: id, level ────────────────────────────────────────────

        [Test]
        public void Roll_TakesItsDefinitionIdFromTheCatalog()
        {
            var generator = Generator(Equipment("chest.iron"), new ConstantRollSource(0f));

            var instance = generator.Roll(new RollContext(FakeLootTable.ForCategory(ItemCategory.Equipment)));

            Assert.That(instance.DefinitionId, Is.EqualTo("chest.iron"));
        }

        [Test]
        public void Roll_ItemLevelIsTheContextSourceLevel()
        {
            var generator = Generator(Equipment("chest"), new ConstantRollSource(0f));

            var instance = generator.Roll(new RollContext(FakeLootTable.ForCategory(ItemCategory.Equipment), sourceLevel: 42));

            Assert.That(instance.ItemLevel, Is.EqualTo(42));
        }

        [Test]
        public void Roll_RollSourceReturnsExactlyOne_StillPicksTheOnlyDefinition()
        {
            // UnityEngine.Random.value is [0,1] inclusive - a roll of exactly 1.0 must not
            // make a single-definition category roll nothing.
            var generator = Generator(Equipment("chest"), new QueuedRollSource(1f, 1f, 1f));

            var instance = generator.Roll(new RollContext(FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common)));

            Assert.That(instance.DefinitionId, Is.EqualTo("chest"));
        }

        [Test]
        public void Roll_DrawsDefinitionsUniformlyFromTheCategory()
        {
            var catalog = new InMemoryItemCatalog(
                Equipment("chest.a"), Equipment("chest.b"), Equipment("chest.c"));
            var generator = new ItemGenerator(catalog, new SeededRollSource(2024));
            var context = new RollContext(FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));

            var counts = new Dictionary<string, int> { ["chest.a"] = 0, ["chest.b"] = 0, ["chest.c"] = 0 };
            for (var i = 0; i < 30_000; i++)
                counts[generator.Roll(context).DefinitionId]++;

            foreach (var id in counts.Keys.ToArray())
                Assert.That(counts[id] / 30_000f, Is.EqualTo(1f / 3f).Within(0.03f), id);
        }

        // ── magic find: the shared invariant with the probability rebuild ────

        [Test]
        public void Roll_MagicFindZero_RarityDistributionMatchesTheAuthoredTable()
        {
            var generator = Generator(Equipment("chest"), new SeededRollSource(20260902));
            var context = new RollContext(FakeLootTable.ForCategory(ItemCategory.Equipment));
            var authored = FakeLootTable.AuthoredRarityOdds(); // [_, .533, .267, .133, .067]

            var counts = RollManyAndCountRarities(generator, context, 60_000);

            AssertShare(counts, ItemRarity.Common, authored[1], 60_000);
            AssertShare(counts, ItemRarity.Magic, authored[2], 60_000);
            AssertShare(counts, ItemRarity.Rare, authored[3], 60_000);
            AssertShare(counts, ItemRarity.Unique, authored[4], 60_000);
        }

        [Test]
        public void Roll_MagicFind_BiasesRarityTowardsRarerTiers()
        {
            var generator = Generator(Equipment("chest"), new SeededRollSource(20260902));
            var context = new RollContext(FakeLootTable.ForCategory(ItemCategory.Equipment), sourceLevel: 0, magicFind: 300f);

            var counts = RollManyAndCountRarities(generator, context, 60_000);
            var authored = FakeLootTable.AuthoredRarityOdds();

            Assert.That(counts[ItemRarity.Common] / 60_000f, Is.LessThan(authored[1] / 4f),
                "Common collapses under heavy magic find (ADR-0004: it reaches ~0 by 200%)");
            Assert.That(counts[ItemRarity.Unique] / 60_000f, Is.GreaterThan(authored[4]),
                "Unique share must exceed its authored 6.7%");
        }

        // ── affix count follows the rarity map ──────────────────────────────

        [TestCase(ItemRarity.Common, 1)]
        [TestCase(ItemRarity.Magic, 2)]
        [TestCase(ItemRarity.Rare, 3)]
        public void Roll_AffixCount_MatchesTheRarityMap(ItemRarity rarity, int expected)
        {
            var pool = new[]
            {
                Slot(StatName.Health), Slot(StatName.Armor), Slot(StatName.MagicResist), Slot(StatName.MovementSpeed),
            };
            var generator = Generator(Equipment("chest", pool), new SeededRollSource(7));
            var context = new RollContext(FakeLootTable.Fixed(ItemCategory.Equipment, rarity));

            var instance = generator.Roll(context);

            Assert.That(instance.Affixes, Has.Count.EqualTo(expected));
            Assert.That(instance.Affixes.Select(a => a.Stat).Distinct().Count(), Is.EqualTo(expected), "no stat rolled twice");
        }

        [Test]
        public void AffixCountFor_KeepsTheEquipmentMap()
        {
            Assert.That(ItemGenerator.AffixCountFor(ItemRarity.NoDrop), Is.EqualTo(0));
            Assert.That(ItemGenerator.AffixCountFor(ItemRarity.Common), Is.EqualTo(1));
            Assert.That(ItemGenerator.AffixCountFor(ItemRarity.Magic), Is.EqualTo(2));
            Assert.That(ItemGenerator.AffixCountFor(ItemRarity.Rare), Is.EqualTo(3));
            Assert.That(ItemGenerator.AffixCountFor(ItemRarity.Unique), Is.EqualTo(3));
        }

        // ── affixes come from the pool, never duplicated ────────────────────

        [Test]
        public void Roll_EveryRolledAffix_IsDrawnFromTheDefinitionsPool()
        {
            var poolStats = new[] { StatName.Health, StatName.Armor, StatName.PhysicalDamage };
            var pool = poolStats.Select(s => Slot(s)).ToArray();
            var generator = Generator(Equipment("chest", pool), new SeededRollSource(99));
            var context = new RollContext(FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Rare));

            for (var i = 0; i < 500; i++)
                foreach (var affix in generator.Roll(context).Affixes)
                    Assert.That(poolStats, Contains.Item(affix.Stat));
        }

        [Test]
        public void Roll_NeverRollsTheSameStatTwiceOnOneInstance()
        {
            var pool = new[]
            {
                Slot(StatName.Health), Slot(StatName.Armor), Slot(StatName.MagicResist),
                Slot(StatName.MovementSpeed), Slot(StatName.Resource),
            };
            var generator = Generator(Equipment("chest", pool), new SeededRollSource(123));
            var context = new RollContext(FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Rare));

            for (var i = 0; i < 500; i++)
            {
                var stats = generator.Roll(context).Affixes.Select(a => a.Stat).ToArray();
                Assert.That(stats, Is.Unique);
                Assert.That(stats, Has.Length.EqualTo(3));
            }
        }

        [Test]
        public void Roll_PoolSmallerThanTheAffixCount_StopsAtThePoolSize()
        {
            var generator = Generator(Equipment("chest", Slot(StatName.Health)), new SeededRollSource(1));
            var context = new RollContext(FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Rare)); // wants 3

            Assert.That(generator.Roll(context).Affixes, Has.Count.EqualTo(1));
        }

        [Test]
        public void Roll_HonoursAffixSlotWeight()
        {
            // Health weight 0 - the far more likely pick is Armor; over many rolls Health barely shows.
            var pool = new[] { Slot(StatName.Health, weight: 0.0001f), Slot(StatName.Armor, weight: 1000f) };
            var generator = Generator(Equipment("chest", pool), new SeededRollSource(55));
            var context = new RollContext(FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common)); // one affix

            var health = 0;
            for (var i = 0; i < 400; i++)
                if (generator.Roll(context).Affixes.Single().Stat == StatName.Health)
                    health++;

            Assert.That(health, Is.LessThan(10), "a near-zero-weight slot should almost never be picked");
        }

        // ── implicit stats, uniques, combining ─────────────────────────────

        [Test]
        public void Roll_ImplicitStats_AreAlwaysOnTheInstance()
        {
            var definition = Equipment("chest"); // empty pool
            definition.ImplicitStats = new[] { Affix(StatName.Armor, 5, 5, 5f) };
            var generator = Generator(definition, new ConstantRollSource(0f));
            var context = new RollContext(FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));

            var instance = generator.Roll(context);

            Assert.That(instance.Affixes, Has.Count.EqualTo(1));
            Assert.That(instance.Affixes.Single().Stat, Is.EqualTo(StatName.Armor));
        }

        [Test]
        public void Roll_Unique_MergesTheDefinitionsFixedAffixList()
        {
            var pool = new[] { Slot(StatName.Health), Slot(StatName.Armor), Slot(StatName.MagicResist) };
            var definition = Equipment("unique.the-gnasher", pool);
            definition.IsUnique = true;
            definition.UniqueAffixes = new[] { Affix(StatName.Experience, 1, 1, 1f), Affix(StatName.MovementSpeed, 2, 2, 2f) };
            var generator = Generator(definition, new SeededRollSource(3));
            var context = new RollContext(FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Unique)); // 3 rolled

            var instance = generator.Roll(context);

            var stats = instance.Affixes.Select(a => a.Stat).ToArray();
            Assert.That(stats, Contains.Item(StatName.Experience));
            Assert.That(stats, Contains.Item(StatName.MovementSpeed));
            Assert.That(instance.Affixes, Has.Count.EqualTo(5), "3 rolled + 2 fixed unique affixes");
        }

        [Test]
        public void Roll_CombinesModifiersOfTheSameStatAndType()
        {
            var definition = Equipment("chest", Slot(StatName.Health, 5, 5, StatModifierType.FlatAdd));
            definition.ImplicitStats = new[] { Affix(StatName.Health, 10, 10, 10f, StatModifierType.FlatAdd) };
            var generator = Generator(definition, new ConstantRollSource(0f));
            var context = new RollContext(FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common)); // one rolled Health

            var instance = generator.Roll(context);

            Assert.That(instance.Affixes, Has.Count.EqualTo(1), "the implicit and the rolled Health fold into one");
            Assert.That(instance.Affixes.Single().Stat, Is.EqualTo(StatName.Health));
            Assert.That(instance.Affixes.Single().Modifier.Value, Is.EqualTo(15f), "10 implicit + 5 rolled");
        }

        // ── RollLoot ───────────────────────────────────────────────────────

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void RollLoot_ReturnsExactlyTheCountAsked(int count)
        {
            var generator = Generator(Equipment("chest"), new SeededRollSource(count + 1));
            var context = new RollContext(FakeLootTable.ForCategory(ItemCategory.Equipment));

            Assert.That(generator.RollLoot(context, count), Has.Count.EqualTo(count));
        }

        [Test]
        public void RollLoot_NegativeCount_Throws()
        {
            var generator = Generator(Equipment("chest"), new SeededRollSource(1));
            var context = new RollContext(FakeLootTable.ForCategory(ItemCategory.Equipment));

            Assert.That(() => generator.RollLoot(context, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void RollLoot_RollsEachInstanceSeparately()
        {
            var generator = Generator(Equipment("chest"), new SeededRollSource(8));
            var context = new RollContext(FakeLootTable.ForCategory(ItemCategory.Equipment));

            var loot = generator.RollLoot(context, 3);

            Assert.That(loot[0], Is.Not.SameAs(loot[1]));
            Assert.That(loot[1], Is.Not.SameAs(loot[2]));
        }

        // ── loud failure, never silent ─────────────────────────────────────

        [Test]
        public void Roll_CatalogHasNoDefinitionInTheRolledCategory_Throws()
        {
            var generator = Generator(Equipment("chest"), new ConstantRollSource(0f)); // Equipment only
            var context = new RollContext(FakeLootTable.ForCategory(ItemCategory.Consumable));

            Assert.That(() => generator.Roll(context), Throws.InvalidOperationException);
        }

        [Test]
        public void Roll_CategoryOddsCarryNoDropMass_Throws()
        {
            var generator = Generator(Equipment("chest"), new ConstantRollSource(0f));
            var table = new FakeLootTable { CategoryOdds = new[] { 1f, 0f, 0f, 0f } }; // all mass on NONE

            Assert.That(() => generator.Roll(new RollContext(table)), Throws.InvalidOperationException);
        }

        [Test]
        public void Roll_RarityOddsCarryNoDropMass_Throws()
        {
            var generator = Generator(Equipment("chest"), new ConstantRollSource(0f));
            var table = new FakeLootTable
            {
                CategoryOdds = FakeLootTable.CategoryVector(ItemCategory.Equipment),
                RarityOdds = new[] { 1f, 0f, 0f, 0f, 0f }, // all mass on NoDrop
            };

            Assert.That(() => generator.Roll(new RollContext(table)), Throws.InvalidOperationException);
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static Dictionary<ItemRarity, int> RollManyAndCountRarities(
            ItemGenerator generator, RollContext context, int n)
        {
            var counts = new Dictionary<ItemRarity, int>();
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
                counts[rarity] = 0;

            for (var i = 0; i < n; i++)
                counts[generator.Roll(context).Rarity]++;

            return counts;
        }

        private static void AssertShare(Dictionary<ItemRarity, int> counts, ItemRarity rarity, float expected, int n) =>
            Assert.That(counts[rarity] / (float)n, Is.EqualTo(expected).Within(0.02f), rarity.ToString());
    }
}
