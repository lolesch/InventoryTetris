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

        private static InMemoryItemCatalog catalog;

        [SetUp]
        public void SetCatalog()
        {
            catalog = new InMemoryItemCatalog(
                new FakeItemDefinition { Id = EquipmentId },
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

        private static HeroBehaviour Admitting(ItemRarity minimum) => new() { LootFilterMinimum = minimum };

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
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet());

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
            _ = new LootFlow(sim, Admitting(ItemRarity.Common), items, new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet());

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
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet());

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
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet());

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
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet());

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
                new FakeCoinDropSource(), new BagItemReceiver(bag), NewWallet());

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
                new FakeCoinDropSource(), player, NewWallet());

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
                new FakeCoinDropSource((CurrencyType.Iron, 7u)), player, wallet);

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
                new FakeCoinDropSource(), player, NewWallet());

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
                new FakeCoinDropSource(), player, NewWallet());

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
                new FakeCoinDropSource(), new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), NewWallet());

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

            _ = new LootFlow(sim, Admitting(ItemRarity.Common), items, coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet);

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

            _ = new LootFlow(sim, Admitting(ItemRarity.Unique), items, coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet);

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

            _ = new LootFlow(sim, Admitting(ItemRarity.Common), items, coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet);

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
                new FakeCoinDropSource(), new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), NewWallet());

            sim.Advance(0.1f);
            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1));

            lootFlow.ClearGround();

            Assert.That(lootFlow.GroundDrops, Is.Empty);
        }

        // ─── the Corpserecovery seat ──────────────────────────────────────────
        // A Death's Corpse is laid back out on re-entry (issue #22 / ADR-0009): recovered
        // items go to the bag if they fit and to the ground otherwise. The ground half is a
        // PlaceOnGround — the same GroundDrops list a Run-end clears.

        [Test]
        public void PlaceOnGround_AddsTheItemToTheGroundDropsList()
        {
            var sim = NewEncounter(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher));
            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), new ItemGenerator(catalog, new SeededRollSource(1)),
                new FakeCoinDropSource(), new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), NewWallet());

            var recovered = new ItemInstance("fake.sword", ItemRarity.Common, 1, null);
            lootFlow.PlaceOnGround(recovered);

            Assert.That(lootFlow.GroundDrops, Has.Count.EqualTo(1));
            Assert.That(lootFlow.GroundDrops[0], Is.SameAs(recovered));
        }

        // ─── AC: the Run tracks what coins bank, so Death's fee reads the real take ──

        [Test]
        public void ACoinPile_ThatPassesTheFilter_RaisesCoinsBanked_WithItsBaseUnitTotal()
        {
            var sim = NewEncounter(OneShotHero(), Profiles.Solo(EnemyArchetype.Skirmisher));
            var wallet = NewWallet();
            var coins = new FakeCoinDropSource((CurrencyType.Iron, 7u)); // 7 base units
            var lootFlow = new LootFlow(sim, Admitting(ItemRarity.Common), new ItemGenerator(catalog, new SeededRollSource(1)),
                coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet);

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
                coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet);

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
                coins, new BagItemReceiver(new CharacterInventory(new Vector2Int(10, 10), catalog)), wallet);

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

            Assert.That(() => new LootFlow(null, behaviour, items, coins, player, wallet), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, null, items, coins, player, wallet), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, behaviour, null, coins, player, wallet), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, behaviour, items, null, player, wallet), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, behaviour, items, coins, null, wallet), Throws.ArgumentNullException);
            Assert.That(() => new LootFlow(sim, behaviour, items, coins, player, null), Throws.ArgumentNullException);
        }
    }
}
