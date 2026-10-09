using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Probability;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The loot flow (issue #24; spec "Loot flow"). A kill rolls its item Drops against a
    /// <see cref="RollContext"/> built from the Location and the hero's live magic find; a Drop
    /// that passes <see cref="HeroBehaviour.AdmitsItem"/> <em>and</em> fits lands in the bag,
    /// everything else stays on the ground. A coin Pile banks to the wallet iff
    /// <see cref="HeroBehaviour.AdmitsCoin"/> passes. <see cref="LootFlow.ClearGround"/> is the
    /// Run-end rule (GLOSSARY.md "Drop").
    ///
    /// Real <see cref="CharacterInventory"/> and <see cref="Wallet"/> throughout, per the
    /// ticket's acceptance criteria — only the rolls (<see cref="IRollSource"/>,
    /// <see cref="ICoinDropSource"/>) are faked.
    /// </summary>
    [TestFixture]
    public sealed class LootFlowTests
    {
        private const string EquipmentId = "fake.sword";
        private const string PotionId = "fake.potion";
        private const string AxeId = "fake.axe";
        private const string HugeId = "fake.huge";

        private static InMemoryItemCatalog catalog;

        [SetUp]
        public void SetCatalog()
        {
            catalog = new InMemoryItemCatalog(
                new FakeItemDefinition { Id = EquipmentId },
                new FakeItemDefinition { Id = PotionId, Category = ItemCategory.Consumable, BaseStackLimit = 10u },
                new FakeItemDefinition { Id = AxeId, Category = ItemCategory.Consumable, Footprint = ItemSize.TwoByOne },
                new FakeItemDefinition { Id = HugeId, Category = ItemCategory.Consumable, Footprint = ItemSize.TwoByFour },
                Coin("fake.iron", CurrencyType.Iron),
                Coin("fake.copper", CurrencyType.Copper),
                Coin("fake.silver", CurrencyType.Silver),
                Coin("fake.gold", CurrencyType.Gold));


            static FakeItemDefinition Coin(string id, CurrencyType type) => new()
            {
                Id = id,
                Category = ItemCategory.Currency,
                CurrencyType = type,
                BaseStackLimit = 999u,
            };
        }

        [TearDown]
        public void ClearCatalog() => catalog = null;

        // ─── fixtures ───────────────────────────────────────────────────────

        /// <summary>A hero that one-shots the lone enemy on the first tick — isolates one kill.</summary>
        private static FakeHero OneShotHero() => new()
        {
            PhysicalDamage = 1_000_000f,
            AttackSpeed = 10f,
            MagicalDamage = 0f,
            Resource = 0f,
        };

        private static EncounterSimulation NewEncounter(FakeHero hero, EncounterProfile location) =>
            new(hero, location, new ConstantRollSource(0f), Behaviours.Engaging(5));

        private Wallet NewWallet(int width = 6, int height = 6) =>
            new(new CharacterInventory(new Vector2Int(width, height), catalog), new FakeCurrencyMinter(catalog));

        // The kill-time pick-up tests below are the AutoPickup-on path (issue #63's debug switch).
        private static HeroBehaviour Admitting(ItemRarity minimum) => new() { LootFilterMinimum = minimum, AutoPickup = true };

        // ─── AC: drop count against a RollContext (Location's table, source level, magic find) ──

        [Test]
        public void Kill_RollsTheArchetypesBaseCount_PlusTheHerosIncreasedItemQuantityBonus()
        {
            var hero = OneShotHero();
            hero.IncreasedItemQuantity = 200f; // +2 on top of the archetype's base of 1 => 3
            var location = Profiles.Solo(EnemyArchetype.Skirmisher, sourceLevel: 7,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(hero, location);

            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));
            var bag = new CharacterInventory(new Vector2Int(10, 10), catalog);
            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), items,
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet(), NewGround());

            sim.Advance(0.1f); // one tick — the lone Skirmisher dies

            Assert.That(bag.StoredPackages, Has.Count.EqualTo(3), "1 base roll + 2 from 200% IncreasedItemQuantity");
            foreach (var package in bag.StoredPackages.Values)
                Assert.That(package.Item.ItemLevel, Is.EqualTo(7), "RollContext.SourceLevel came from the Location");
        }

        [Test]
        public void Kill_UsesTheHerosLiveMagicFind_ToBiasTheRarityRoll()
        {
            var lootTable = FakeLootTable.ForCategory(ItemCategory.Equipment); // the authored rarity odds
            const float rarityRoll = 0.6f;

            var expectedAtZero = ProbabilityTable<ItemRarity>.Sample(lootTable.RarityOdds, rarityRoll);
            var expectedAtHighMagicFind = ProbabilityTable<ItemRarity>.Sample(
                RarityCascade.Apply(lootTable.RarityOdds, 300f), rarityRoll);
            Assert.That(expectedAtHighMagicFind, Is.Not.EqualTo(expectedAtZero),
                "the chosen roll must land on a different tier once magic find cascades — otherwise this test proves nothing");

            Assert.That(RollOneItemsRarity(magicFind: 0f, lootTable, rarityRoll), Is.EqualTo(expectedAtZero));
            Assert.That(RollOneItemsRarity(magicFind: 300f, lootTable, rarityRoll), Is.EqualTo(expectedAtHighMagicFind));
        }

        private ItemRarity RollOneItemsRarity(float magicFind, LootTable lootTable, float rarityRoll)
        {
            var hero = OneShotHero();
            hero.MagicFind = magicFind;
            var location = Profiles.Solo(EnemyArchetype.Skirmisher, sourceLevel: 1, table: lootTable);
            var sim = NewEncounter(hero, location);

            // category (one-hot, any value), PickDefinition (one candidate, any value), rarity.
            var items = new ItemGenerator(catalog, new QueuedRollSource(0f, 0f, rarityRoll));
            var bag = new CharacterInventory(new Vector2Int(10, 10), catalog);
            _ = new LootFlow(sim, Admitting(ItemRarity.Common), items, new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet(), NewGround());

            sim.Advance(0.1f);

            var stored = bag.StoredPackages.Values.Single();
            return stored.Item.Rarity;
        }

        // ─── AC: filter + fit decide bag vs ground ───────────────────────────

        [Test]
        public void AnItem_ThatPassesTheFilterAndFits_LandsInTheBag()
        {
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(OneShotHero(), location);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));
            var bag = new CharacterInventory(new Vector2Int(10, 10), catalog);

            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), items,
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet(), NewGround());

            sim.Advance(0.1f);

            Assert.That(bag.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(lootFlow.GroundDrops, Is.Empty);
        }

        [Test]
        public void AnItem_ThatFailsTheFilter_StaysOnTheGround_EvenWithRoomInTheBag()
        {
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(OneShotHero(), location);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));
            var bag = new CharacterInventory(new Vector2Int(10, 10), catalog);

            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Unique), items,
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet(), NewGround());

            sim.Advance(0.1f);

            Assert.That(bag.StoredPackages, Is.Empty);
            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1));
        }

        [Test]
        public void AnItem_ThatPassesTheFilterButDoesNotFit_StaysOnTheGround()
        {
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(OneShotHero(), location);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));

            var bag = new CharacterInventory(new Vector2Int(1, 1), catalog);
            var filler = new Package(bag, new ItemInstance(EquipmentId, ItemRarity.Common, 1, null), 1u);
            Assert.That(bag.TryAddToContainer(ref filler), Is.True, "test setup: the one cell must already be full");

            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), items,
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet(), NewGround());

            sim.Advance(0.1f);

            Assert.That(bag.StoredPackages, Has.Count.EqualTo(1), "still just the filler — the Drop had nowhere to go");
            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1));
        }

        [Test]
        public void OfSeveralDrops_ExactlyThoseThatPassTheFilterAndFit_LandInTheBag()
        {
            var hero = OneShotHero();
            hero.IncreasedItemQuantity = 300f; // 1 base + 3 bonus = 4 drops
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(hero, location);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));

            // Room for exactly 2 of the 4 Commons that pass the filter.
            var bag = new CharacterInventory(new Vector2Int(2, 1), catalog);

            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), items,
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet(), NewGround());

            sim.Advance(0.1f);

            Assert.That(bag.StoredPackages, Has.Count.EqualTo(2), "the bag only had room for 2");
            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(2), "the other 2 passed the filter but had nowhere to go");
        }

        // ─── AC (#103): placement is the player's entry point, so auto-equip applies ──

        [Test]
        public void AnItem_TheReceiverEquips_IsNeitherInTheBagNorOnTheGround()
        {
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(OneShotHero(), location);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));
            var bag = new CharacterInventory(new Vector2Int(10, 10), catalog);
            var player = new BagItemReceiver(bag) { Equips = _ => true }; // auto-equip on, slot empty

            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), items,
                new FakeCoinDropSource(), player, NewWallet(), NewGround());

            sim.Advance(0.1f);

            Assert.That(player.Equipped, Has.Count.EqualTo(1), "the Drop went to the equipment, not a raw bag add");
            Assert.That(bag.StoredPackages, Is.Empty);
            Assert.That(lootFlow.GroundDrops, Is.Empty, "equipped is a successful placement");
        }

        [Test]
        public void AReceiverThatThrows_DoesNotCrashTheEncounter_GroundsTheDrop_AndSurfacesTheFailure()
        {
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(OneShotHero(), location);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));
            var boom = new System.InvalidOperationException("equip blew up");
            var player = new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)) { Throws = boom };
            var wallet = NewWallet();

            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), items,
                new FakeCoinDropSource((CurrencyType.Iron, 7u)), player, wallet, NewGround());

            System.Exception reported = null;
            lootFlow.PlacementFailed += (_, exception) => reported = exception;

            Assert.That(() => sim.Advance(0.1f), Throws.Nothing,
                "an equip's engine-side effects must not abort the tick that killed the enemy");

            Assert.That(sim.EnemiesDefeated, Is.EqualTo(1), "the kill itself still resolves");
            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1), "the Drop is not lost");
            Assert.That(reported, Is.SameAs(boom), "the failure is reported, not swallowed");
            Assert.That(wallet.Balance.Iron, Is.EqualTo(7u), "the kill's coin Pile still banks");
        }

        [Test]
        public void AnItem_TheReceiverRefuses_StaysOnTheGround_WithNoOverflowElsewhere()
        {
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(OneShotHero(), location);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));
            var bag = new CharacterInventory(new Vector2Int(1, 1), catalog);
            var filler = new Package(bag, new ItemInstance(EquipmentId, ItemRarity.Common, 1, null), 1u);
            Assert.That(bag.TryAddToContainer(ref filler), Is.True, "test setup: the one cell must already be full");
            var player = new BagItemReceiver(bag);

            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), items,
                new FakeCoinDropSource(), player, NewWallet(), NewGround());

            sim.Advance(0.1f);

            Assert.That(player.Offered, Has.Count.EqualTo(1), "the Drop was offered to the receiver");
            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1), "a refusal is the ground's cue");
        }

        [Test]
        public void AnItem_ThatFailsTheFilter_IsNeverOfferedToTheReceiver()
        {
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(OneShotHero(), location);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));
            var player = new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)) { Equips = _ => true };

            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Unique), items,
                new FakeCoinDropSource(), player, NewWallet(), NewGround());

            sim.Advance(0.1f);

            Assert.That(player.Offered, Is.Empty, "the filter gates the pick-up - it must not auto-equip what the hero waved off");
            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1));
        }

        [Test]
        public void AKillsItemRoll_ThatThrows_DoesNotCrashTheEncounter()
        {
            // All category mass on the fail bucket (NONE) — ItemGenerator.Roll throws
            // InvalidOperationException ("the loot table's category odds carry no drop mass").
            var emptyTable = new FakeLootTable
            {
                CategoryOdds = FakeLootTable.CategoryVector(ItemCategory.NONE),
                RarityOdds = FakeLootTable.AuthoredRarityOdds(),
            };
            var location = Profiles.Solo(EnemyArchetype.Skirmisher, table: emptyTable);
            var sim = NewEncounter(OneShotHero(), location);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));

            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), items,
                new FakeCoinDropSource(), new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), NewWallet(), NewGround());

            Assert.That(() => sim.Advance(0.1f), Throws.Nothing,
                "a misconfigured loot table must not crash the Encounter — combat cannot depend on itemization content being well-formed");

            Assert.That(sim.EnemiesDefeated, Is.EqualTo(1), "the kill itself still resolves");
            Assert.That(lootFlow.GroundDrops, Is.Empty, "no items to show for the failed roll");
        }

        // ─── AC: coin Piles auto-bank iff the denomination's rarity passes the filter ────────

        [Test]
        public void ACoinPile_ThatPassesTheFilter_AutoBanksToTheWallet()
        {
            var sim = NewEncounter(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher));
            var items = new ItemGenerator(catalog, new SeededRollSource(1));
            var wallet = NewWallet();
            var coins = new FakeCoinDropSource((CurrencyType.Iron, 7u)); // iron is Common

            _ = new LootFlow(sim, Admitting(ItemRarity.Common), items, coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet, NewGround());

            sim.Advance(0.1f);

            Assert.That(wallet.Balance.Iron, Is.EqualTo(7u));
        }

        [Test]
        public void ACoinPile_ThatFailsTheFilter_IsNotBanked()
        {
            var sim = NewEncounter(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher));
            var items = new ItemGenerator(catalog, new SeededRollSource(1));
            var wallet = NewWallet();
            var coins = new FakeCoinDropSource((CurrencyType.Iron, 7u)); // iron is Common

            _ = new LootFlow(sim, Admitting(ItemRarity.Unique), items, coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet, NewGround());

            sim.Advance(0.1f);

            Assert.That(wallet.Balance.Total, Is.Zero);
        }

        [Test]
        public void NoCoinsFalling_IsNotAnError_AndBanksNothing()
        {
            var sim = NewEncounter(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher));
            var items = new ItemGenerator(catalog, new SeededRollSource(1));
            var wallet = NewWallet();
            var coins = new FakeCoinDropSource(); // dry — hands back (NONE, 0)

            _ = new LootFlow(sim, Admitting(ItemRarity.Common), items, coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet, NewGround());

            Assert.That(() => sim.Advance(0.1f), Throws.Nothing);
            Assert.That(wallet.Balance.Total, Is.Zero);
        }

        // ─── AC: ground Drops are cleared when the Run ends ──────────────────

        [Test]
        public void ClearGround_DiscardsEveryGroundDrop()
        {
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(OneShotHero(), location);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));

            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Unique), items,
                new FakeCoinDropSource(), new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), NewWallet(), NewGround());

            sim.Advance(0.1f);
            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1));

            lootFlow.ClearGround();

            Assert.That(lootFlow.GroundDrops, Is.Empty);
        }

        // ─── the floor (epic #214, issue #216): a stationary grid that evicts the oldest ───

        private static Vector2Int? CellOf(LootFlow lootFlow, ItemInstance item) =>
            lootFlow.Ground.StoredPackages.Where(entry => ReferenceEquals(entry.Value.Item, item))
                .Select(entry => (Vector2Int?)entry.Key).FirstOrDefault();

        private LootFlow NewFloor(int width, int height, params AbstractDimensionalContainer[] receiving) =>
            NewIdleLootFlow(new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), NewGround(width, height), receiving);

        [Test]
        public void ANewPackage_EvictsTheOldestFirst_UntilItFits()
        {
            var floor = NewFloor(3, 1);
            var oldest = Sword(1);
            var middle = Sword(2);
            var newest = Sword(3);
            var axe = Axe();
            _ = floor.PlaceOnGround(Pack(oldest));
            _ = floor.PlaceOnGround(Pack(middle));
            _ = floor.PlaceOnGround(Pack(newest));

            var placed = floor.PlaceOnGround(Pack(axe));

            Assert.That(placed, Is.True);
            Assert.That(floor.GroundDrops.Select(drop => drop.Item), Is.EqualTo(new[] { newest, axe }),
                "the two oldest made room, and the list reads oldest first");
        }

        [Test]
        public void AfterAnEviction_TheSurvivorsStayInTheirCells_AndHolesAreNotCompacted()
        {
            var bag = new CharacterInventory(new Vector2Int(10, 10), catalog);
            var floor = NewIdleLootFlow(new BagItemReceiver(bag), NewGround(3, 1), bag);
            var first = Sword(1);
            var second = Sword(2);
            var third = Sword(3);
            var axe = Axe();
            _ = floor.PlaceOnGround(Pack(first));
            _ = floor.PlaceOnGround(Pack(second));
            _ = floor.PlaceOnGround(Pack(third));
            _ = floor.PickUpFromGround(second); // a hole at (1,0)

            Assert.That(floor.PlaceOnGround(Pack(axe)), Is.True, "no two free cells in a row, so the oldest goes");

            Assert.That(CellOf(floor, first), Is.Null, "the oldest was evicted");
            Assert.That(CellOf(floor, third), Is.EqualTo(new Vector2Int(2, 0)), "nothing moved");
            Assert.That(CellOf(floor, axe), Is.EqualTo(new Vector2Int(0, 0)));
        }

        [Test]
        public void ANewPackage_TakesTheFirstHoleThatFits_WithoutEvictingAnything()
        {
            var bag = new CharacterInventory(new Vector2Int(10, 10), catalog);
            var floor = NewIdleLootFlow(new BagItemReceiver(bag), NewGround(3, 1), bag);
            var first = Sword(1);
            var second = Sword(2);
            var third = Sword(3);
            _ = floor.PlaceOnGround(Pack(first));
            _ = floor.PlaceOnGround(Pack(second));
            _ = floor.PlaceOnGround(Pack(third));
            _ = floor.PickUpFromGround(second);
            var fourth = Sword(4);

            _ = floor.PlaceOnGround(Pack(fourth));

            Assert.That(CellOf(floor, fourth), Is.EqualTo(new Vector2Int(1, 0)), "first-fit lands in the hole");
            Assert.That(CellOf(floor, first), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(CellOf(floor, third), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        public void AStack_ThatGainsItems_BecomesTheNewest()
        {
            var floor = NewFloor(2, 1);
            var firstPotions = new ItemInstance(PotionId, ItemRarity.Common, 1, null);
            var sword = Sword(1);
            _ = floor.PlaceOnGround(new Package(null, firstPotions, 2u));
            _ = floor.PlaceOnGround(Pack(sword));
            _ = floor.PlaceOnGround(new Package(null, new ItemInstance(PotionId, ItemRarity.Common, 1, null), 1u));
            var late = Sword(2);

            _ = floor.PlaceOnGround(Pack(late));

            Assert.That(floor.GroundDrops.Select(drop => (drop.Item, drop.Amount)),
                Is.EqualTo(new[] { (firstPotions, 3u), (late, 1u) }),
                "the merge refreshed the stack's age, so the sword was the oldest");
        }

        [Test]
        public void ADiscard_WhenTheFloorIsFull_EvictsTheOldest_AndAStackStaysOnePackage()
        {
            var bag = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var potions = new ItemInstance(PotionId, ItemRarity.Common, 1, null);
            var stack = new Package(bag, potions, 3u);
            Assert.That(bag.TryAddToContainer(ref stack), Is.True, "fixture: the stack fits");
            var floor = NewFloor(1, 1);
            var old = Sword(1);
            _ = floor.PlaceOnGround(Pack(old));
            var changes = 0;
            floor.GroundChanged += () => changes++;

            var dropped = DropTransaction.Run(bag, Vector2Int.zero, floor);

            Assert.That(dropped, Is.True);
            Assert.That(floor.GroundDrops.Select(drop => (drop.Item, drop.Amount)), Is.EqualTo(new[] { (potions, 3u) }),
                "three potions are one package, and the old sword made room");
            Assert.That(bag.TryGetPackageAt(Vector2Int.zero, out _), Is.False);
            Assert.That(changes, Is.EqualTo(1), "one change for the landing and its eviction");
        }

        [Test]
        public void APackage_LargerThanTheWholeFloor_IsRefused_AndEverythingStays()
        {
            var bag = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var huge = new ItemInstance(HugeId, ItemRarity.Common, 1, null);
            var held = new Package(bag, huge, 1u);
            Assert.That(bag.TryAddToContainer(ref held), Is.True, "fixture: the bag takes it");
            var floor = NewFloor(2, 2);
            var sword = Sword(1);
            _ = floor.PlaceOnGround(Pack(sword));
            var changes = 0;
            floor.GroundChanged += () => changes++;

            Assert.That(floor.PlaceOnGround(Pack(huge)), Is.False, "handed back");
            Assert.That(DropTransaction.Run(bag, Vector2Int.zero, floor), Is.False);

            Assert.That(floor.GroundDrops.Select(drop => drop.Item), Is.EqualTo(new[] { sword }), "nothing was evicted for it");
            Assert.That(bag.TryGetPackageAt(Vector2Int.zero, out var stillThere), Is.True, "the item stays where it was");
            Assert.That(stillThere.IsValid, Is.True);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void PickingUpAStack_ThatOnlyPartlyFits_LeavesTheFloorAndTheBagAsTheyWere()
        {
            var bag = new CharacterInventory(new Vector2Int(1, 1), catalog);
            var held = new Package(bag, new ItemInstance(PotionId, ItemRarity.Common, 1, null), 8u);
            Assert.That(bag.TryAddToContainer(ref held), Is.True, "fixture: 8 of 10, so room for 2");
            var floor = NewIdleLootFlow(new BagItemReceiver(bag), NewGround(), bag);
            var potions = new ItemInstance(PotionId, ItemRarity.Common, 1, null);
            _ = floor.PlaceOnGround(new Package(null, potions, 5u));
            var changes = 0;
            floor.GroundChanged += () => changes++;

            var picked = floor.PickUpFromGround(potions);

            Assert.That(picked, Is.False);
            Assert.That(floor.GroundDrops.Select(drop => drop.Amount), Is.EqualTo(new[] { 5u }), "still all five");
            Assert.That(bag.StoredPackages.Values.Single().Amount, Is.EqualTo(8u), "no two of them slipped into the bag");
            Assert.That(changes, Is.Zero);
        }

        // ─── AutoPickup off (the default, issue #63): a kill's Drops all lie on the ground ──

        [TestCase(ItemRarity.Common)]
        [TestCase(ItemRarity.Unique)]
        public void WithAutoPickupOff_AnItem_LandsOnTheGround_WhateverTheFilterSays(ItemRarity filterMinimum)
        {
            var (lootFlow, player, bag) = KillWithAutoPickupOff(filterMinimum);

            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1));
            Assert.That(bag.StoredPackages, Is.Empty, "nothing is picked up on the hero's behalf");
            Assert.That(player.Offered, Is.Empty, "and nothing is auto-equipped either");
        }

        [Test]
        public void WithAutoPickupOff_TheClickIsWhatPicksItUp_ThroughTheAcquisitionEntryPoint()
        {
            var (lootFlow, player, _) = KillWithAutoPickupOff(ItemRarity.Common);

            _ = lootFlow.PickUpFromGround(lootFlow.GroundDrops[0].Item);

            Assert.That(player.Equipped, Has.Count.EqualTo(1), "auto-equip applies to the click");
            Assert.That(lootFlow.GroundDrops, Is.Empty);
        }

        [Test]
        public void WithAutoPickupOff_EveryDropLiesOnTheGround_EvenWithRoomInTheBag()
        {
            var hero = OneShotHero();
            hero.IncreasedItemQuantity = 300f; // 1 base + 3 bonus = 4 drops

            var (lootFlow, _, bag) = KillWithAutoPickupOff(ItemRarity.Common, hero);

            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(4));
            Assert.That(bag.StoredPackages, Is.Empty);
        }

        [Test]
        public void AutoPickup_IsReadLive_PerKill()
        {
            var behaviour = new HeroBehaviour { LootFilterMinimum = ItemRarity.Common, AutoPickup = false };
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(OneShotHero(), location);
            var bag = new CharacterInventory(new Vector2Int(10, 10), catalog);
            var lootFlow = new LootFlow(sim, behaviour, new ItemGenerator(catalog, new ConstantRollSource(0f)),
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet(), NewGround());

            behaviour.AutoPickup = true; // flipped after the flow was built, before the kill

            sim.Advance(0.1f);

            Assert.That(bag.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(lootFlow.GroundDrops, Is.Empty);
        }

        private (LootFlow lootFlow, BagItemReceiver player, CharacterInventory bag) KillWithAutoPickupOff(
            ItemRarity filterMinimum, FakeHero hero = null)
        {
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(hero ?? OneShotHero(), location);
            var bag = new CharacterInventory(new Vector2Int(10, 10), catalog);
            var player = new BagItemReceiver(bag) { Equips = _ => true }; // auto-equip on, slot empty

            var lootFlow = new LootFlow(sim, new HeroBehaviour { LootFilterMinimum = filterMinimum, AutoPickup = false },
                new ItemGenerator(catalog, new ConstantRollSource(0f)), new FakeCoinDropSource(), player, NewWallet(), NewGround());

            sim.Advance(0.1f);
            return (lootFlow, player, bag);
        }

        // ─── the Ground Items List (issue #63): the list follows GroundChanged, picks up through the player ──

        private LootFlow NewIdleLootFlow(BagItemReceiver player, GroundContainer ground = null,
            params AbstractDimensionalContainer[] receiving)
        {
            var sim = NewEncounter(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher));
            return new LootFlow(sim, Admitting(ItemRarity.Common), new ItemGenerator(catalog, new SeededRollSource(1)),
                new FakeCoinDropSource(), player, NewWallet(), ground ?? NewGround(), receiving);
        }

        private static GroundContainer NewGround(int width = 10, int height = 13) => new(new Vector2Int(width, height), catalog);

        private static ItemInstance Sword(int seed = 1) => new("fake.sword", ItemRarity.Common, seed, null);

        private static ItemInstance Axe() => new(AxeId, ItemRarity.Common, 1, null);

        private static Package Pack(ItemInstance item) => new(null, item, 1u);

        [Test]
        public void GroundChanged_FiresWhenAnItemIsPlacedOnTheGround()
        {
            var lootFlow = NewIdleLootFlow(new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)));
            var changes = 0;
            lootFlow.GroundChanged += () => changes++;

            _ = lootFlow.PlaceOnGround(Pack(Sword()));

            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void GroundChanged_FiresWhenAKillGroundsADrop()
        {
            var location = Profiles.Solo(EnemyArchetype.Skirmisher,
                table: FakeLootTable.Fixed(ItemCategory.Equipment, ItemRarity.Common));
            var sim = NewEncounter(OneShotHero(), location);
            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Unique), new ItemGenerator(catalog, new ConstantRollSource(0f)),
                new FakeCoinDropSource(), new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), NewWallet(), NewGround());
            var changes = 0;
            lootFlow.GroundChanged += () => changes++;

            sim.Advance(0.1f);

            Assert.That(changes, Is.EqualTo(1), "the filtered-out Drop is a ground entry");
        }

        [Test]
        public void GroundChanged_FiresWhenTheGroundIsCleared_ButNotWhenItWasAlreadyEmpty()
        {
            var lootFlow = NewIdleLootFlow(new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)));
            var changes = 0;
            lootFlow.GroundChanged += () => changes++;

            lootFlow.ClearGround();
            Assert.That(changes, Is.Zero, "nothing to clear, nothing to repaint");

            _ = lootFlow.PlaceOnGround(Pack(Sword()));
            lootFlow.ClearGround();
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void PickUpFromGround_HandsTheItemToThePlayer_AndRemovesItFromTheGround()
        {
            var bag = new CharacterInventory(new Vector2Int(10, 10), catalog);
            var player = new BagItemReceiver(bag);
            var lootFlow = NewIdleLootFlow(player);
            var sword = Sword();
            _ = lootFlow.PlaceOnGround(Pack(sword));
            var changes = 0;
            lootFlow.GroundChanged += () => changes++;

            var picked = lootFlow.PickUpFromGround(sword);

            Assert.That(picked, Is.True);
            Assert.That(player.Offered, Is.EqualTo(new[] { sword }), "through the acquisition entry point");
            Assert.That(lootFlow.GroundDrops, Is.Empty);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void PickUpFromGround_WithNoRoom_LeavesTheItemOnTheGround()
        {
            var full = new CharacterInventory(new Vector2Int(1, 1), catalog);
            var filler = new Package(full, Sword(), 1u);
            Assert.That(full.TryAddToContainer(ref filler), Is.True);
            var lootFlow = NewIdleLootFlow(new BagItemReceiver(full));
            var sword = Sword();
            _ = lootFlow.PlaceOnGround(Pack(sword));
            var changes = 0;
            lootFlow.GroundChanged += () => changes++;

            var picked = lootFlow.PickUpFromGround(sword);

            Assert.That(picked, Is.False);
            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void PickUpFromGround_OfAnItemNotOnTheGround_DoesNotOfferItToThePlayer()
        {
            var player = new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog));
            var lootFlow = NewIdleLootFlow(player);

            var picked = lootFlow.PickUpFromGround(Sword());

            Assert.That(picked, Is.False, "a stale click on a slot that already left the list");
            Assert.That(player.Offered, Is.Empty);
        }

        [Test]
        public void PickUpFromGround_TakesTheClickedInstance_WhenEqualItemsLieOnTheGround()
        {
            var player = new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog));
            var lootFlow = NewIdleLootFlow(player);
            var first = Sword();
            var second = Sword();
            _ = lootFlow.PlaceOnGround(Pack(first));
            _ = lootFlow.PlaceOnGround(Pack(second));

            _ = lootFlow.PickUpFromGround(second);

            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1));
            Assert.That(lootFlow.GroundDrops[0].Item, Is.SameAs(first), "ItemInstance is value-equal; the ground removes by identity");
        }

        [Test]
        public void PickUpFromGround_WhenThePlayerThrows_LeavesTheItemOnTheGround_AndSurfacesTheFailure()
        {
            var boom = new System.InvalidOperationException("equip blew up");
            var player = new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)) { Throws = boom };
            var lootFlow = NewIdleLootFlow(player);
            var sword = Sword();
            _ = lootFlow.PlaceOnGround(Pack(sword));
            System.Exception reported = null;
            lootFlow.PlacementFailed += (_, exception) => reported = exception;

            var picked = lootFlow.PickUpFromGround(sword);

            Assert.That(picked, Is.False);
            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1));
            Assert.That(reported, Is.SameAs(boom));
        }

        // ─── AC: the Run tracks what coins bank, so Death's fee reads the real take ──

        [Test]
        public void ACoinPile_ThatPassesTheFilter_RaisesCoinsBanked_WithItsBaseUnitTotal()
        {
            var sim = NewEncounter(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher));
            var wallet = NewWallet();
            var coins = new FakeCoinDropSource((CurrencyType.Iron, 7u)); // 7 base units
            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), new ItemGenerator(catalog, new SeededRollSource(1)),
                coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet, NewGround());

            long banked = -1;
            lootFlow.CoinsBanked += amount => banked = amount;

            sim.Advance(0.1f);

            Assert.That(banked, Is.EqualTo(7L), "iron is the base unit — 7 coins bank 7 base units");
        }

        [Test]
        public void ACopperPile_ThatBanks_RaisesCoinsBanked_AtItsIronValue()
        {
            var sim = NewEncounter(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher));
            var wallet = NewWallet();
            var coins = new FakeCoinDropSource((CurrencyType.Copper, 3u)); // 3 × 5 iron = 15
            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), new ItemGenerator(catalog, new SeededRollSource(1)),
                coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet, NewGround());

            long banked = -1;
            lootFlow.CoinsBanked += amount => banked = amount;

            sim.Advance(0.1f);

            Assert.That(banked, Is.EqualTo(15L));
        }

        [Test]
        public void ACoinPile_ThatFailsTheFilter_DoesNotRaiseCoinsBanked()
        {
            var sim = NewEncounter(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher));
            var wallet = NewWallet();
            var coins = new FakeCoinDropSource((CurrencyType.Iron, 7u)); // iron is Common, filter is Unique
            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Unique), new ItemGenerator(catalog, new SeededRollSource(1)),
                coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet, NewGround());

            var raised = false;
            lootFlow.CoinsBanked += _ => raised = true;

            sim.Advance(0.1f);

            Assert.That(raised, Is.False, "a Pile the filter rejects is never banked, so it never counts toward the Run take");
        }

        // ─── constructor guards ───────────────────────────────────────────────

        [Test]
        public void Constructor_RejectsNullCollaborators()
        {
            var sim = NewEncounter(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher));
            var behaviour = Admitting(ItemRarity.Common);
            var items = new ItemGenerator(catalog, new ConstantRollSource(0f));
            var coins = new FakeCoinDropSource();
            var player = new BagItemReceiver(new CharacterInventory(new Vector2Int(4, 4), catalog));
            var wallet = NewWallet();
            var ground = NewGround();

            Assert.That(() => new LootFlow(null, behaviour, items, coins, player, wallet, ground), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, null, items, coins, player, wallet, ground), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, behaviour, null, coins, player, wallet, ground), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, behaviour, items, null, player, wallet, ground), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, behaviour, items, coins, null, wallet, ground), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, behaviour, items, coins, player, null, ground), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, behaviour, items, coins, player, wallet, null), Throws.ArgumentNullException);
        }
    }
}
