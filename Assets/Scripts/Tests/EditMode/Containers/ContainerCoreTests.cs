using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The container core - <see cref="AbstractDimensionalContainer"/> and subclasses,
    /// <see cref="Package"/> - lives in the <c>InventorySystem.Containers</c> assembly
    /// (issue #15), which this asmdef references directly. The provider singletons the
    /// core used to reach through <c>.Instance</c> are now the injected
    /// <see cref="IStatReceiver"/> / <see cref="ICursorSink"/> / <see cref="ICurrencyMinter"/>,
    /// so the equipment swap paths - unreachable from the Phase 0 <c>Assembly-CSharp-Editor</c>
    /// seam - are covered here with fakes.
    /// </summary>
    [TestFixture]
    public sealed class ContainerCoreTests
    {
        // The ItemDefinition / IItemCatalog stand-ins and the IStatReceiver / ICursorSink
        // fakes live in ContainerTestFixtures.cs, shared with the other tests in this asmdef.

        private const string SwordId = "test.sword";
        private const string ArrowId = "test.arrow";
        private const string HelmId = "test.helm";
        private const string RingId = "test.ring";
        private const string PlankId = "test.plank";
        private const string BowId = "test.bow";
        private const string ShieldId = "test.shield";
        private const string GreatSwordId = "test.greatsword";

        private static IItemCatalog catalog;

        [SetUp]
        public void SetCatalog() => catalog = new TestCatalog()
            .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = ArrowId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Arrow, Footprint = ItemSize.OneByOne, BaseStackLimit = 10u })
            .With(new TestDefinition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = RingId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Ring, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = PlankId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Arrow, Footprint = ItemSize.TwoByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = BowId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Bow, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = ShieldId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Shield, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = GreatSwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.GreatSword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u });

        [TearDown]
        public void ClearCatalog() => catalog = null;

        private static CharacterStatModifier Affix(StatName stat, float value) =>
            new(stat, new StatModifier(new Vector2Int(0, 100), value, StatModifierType.FlatAdd));

        private static ItemInstance Sword() => new(SwordId, ItemRarity.Rare, 7, new[]
        {
            Affix(StatName.PhysicalDamage, 6f),
        });

        private static ItemInstance Arrows() => new(ArrowId, ItemRarity.Common, 1, null);
        private static ItemInstance Plank() => new(PlankId, ItemRarity.Common, 1, null);

        private static ItemInstance Helm(float armor) => new(HelmId, ItemRarity.Rare, 5, new[] { Affix(StatName.Armor, armor) });
        private static ItemInstance Ring(float value) => new(RingId, ItemRarity.Magic, 3, new[] { Affix(StatName.Health, value) });

        [Test]
        public void CharacterInventory_NewlyConstructed_IsEmptyWithTheGivenCapacity()
        {
            var inventory = new CharacterInventory(new Vector2Int(4, 4), catalog);

            Assert.That(inventory.StoredPackages, Is.Empty);
            Assert.That(inventory.Capacity, Is.EqualTo(16));
        }

        [Test]
        public void CharacterInventory_AfterAddingAPackage_HoldsExactlyThatItem()
        {
            var inventory = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var package = new Package(inventory, Sword(), 1u);

            var accepted = inventory.TryAddToContainer(ref package);

            Assert.That(accepted, Is.True);
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(package.Amount, Is.EqualTo(0u));
        }

        [Test]
        public void CharacterInventory_TwoPlainConsumables_MergeIntoOneStack()
        {
            var inventory = new CharacterInventory(new Vector2Int(4, 4), catalog);

            var first = new Package(inventory, Arrows(), 4u);
            _ = inventory.TryAddToContainer(ref first);
            var second = new Package(inventory, Arrows(), 3u);
            _ = inventory.TryAddToContainer(ref second);

            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(inventory.StoredPackages.Values.Single().Amount, Is.EqualTo(7u));
        }

        [Test]
        public void TryFindEmptyCell_OnAnEmptyContainer_ReturnsTheTopLeftCell()
        {
            var inventory = new CharacterInventory(new Vector2Int(3, 3), catalog);

            var found = inventory.TryFindEmptyCell(new Vector2Int(1, 1), out var cell);

            Assert.That(found, Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2Int(0, 0)));
        }

        [Test]
        public void TryFindEmptyCell_WithTheFirstColumnFull_ReturnsTheNextColumnsTopCell()
        {
            var inventory = new CharacterInventory(new Vector2Int(2, 2), catalog);

            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Arrows(), 1u));
            _ = inventory.AddAtPosition(new Vector2Int(0, 1), new Package(inventory, Arrows(), 1u));

            var found = inventory.TryFindEmptyCell(new Vector2Int(1, 1), out var cell);

            Assert.That(found, Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void TryFindEmptyCell_WhenTheContainerIsFull_ReturnsFalse()
        {
            var inventory = new CharacterInventory(new Vector2Int(1, 1), catalog);

            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Helm(1f), 1u));

            var found = inventory.TryFindEmptyCell(new Vector2Int(1, 1), out _);

            Assert.That(found, Is.False);
        }

        [Test]
        public void TryAddAtEmpty_AfterABiggerItemSkippedAnEarlyGapItCouldNotFit_ALaterSmallerAddStillFindsThatGap()
        {
            // TryAddAtEmpty's placement loop resumes its scan from the last cell it placed
            // into (not from the origin) to stay O(cells) for a package that spans several
            // stacks - the classic risk with that "resume where I left off" shape is a
            // next-fit allocator bug: a big item's stopping point leaking into a later,
            // smaller item's search and hiding an early gap the big item couldn't use but the
            // small one could. The resume cursor is a local reset every call, not persisted
            // state, so this pins that it can't happen: cell 1 is a gap only a 1x1 fits,
            // the 2x1 plank has to skip it and land at cell 2-3, and a 1x1 added afterwards -
            // in its own, separate call - still finds cell 1, not cell 4 onward.
            var inventory = new CharacterInventory(new Vector2Int(5, 1), catalog);

            _ = inventory.AddAtPosition(new Vector2Int(1, 0), new Package(inventory, Helm(1f), 1u)); // occupies cell 1, isolating cell 0 as a 1-wide gap

            var plankPackage = new Package(inventory, Plank(), 1u);
            var plankAccepted = inventory.TryAddToContainer(ref plankPackage);

            Assert.That(plankAccepted, Is.True);
            Assert.That(inventory.StoredPackages.ContainsKey(new Vector2Int(2, 0)), Is.True, "the 2x1 plank had to skip the 1-wide gap at cell 0 and land at cells 2-3");

            var ringPackage = new Package(inventory, Ring(1f), 1u);
            var ringAccepted = inventory.TryAddToContainer(ref ringPackage);

            Assert.That(ringAccepted, Is.True);
            Assert.That(inventory.StoredPackages.ContainsKey(new Vector2Int(0, 0)), Is.True,
                "a later, independent add must still find the early gap the plank couldn't use - not resume past it");
        }

        [Test]
        public void CharacterInventory_AddingMoreThanOneStacksWorthOfAmount_SpreadsAcrossMultipleCells()
        {
            // Arrows stack to 10; 25 forces TryAddAtEmpty's placement loop to walk three
            // separate free cells in one call - regression coverage for its rewrite from an
            // advancing nested for-loop to a TryFindEmptyCell-driven while-loop.
            var inventory = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var package = new Package(inventory, Arrows(), 25u);

            var accepted = inventory.TryAddToContainer(ref package);

            Assert.That(accepted, Is.True);
            Assert.That(package.Amount, Is.EqualTo(0u));
            Assert.That(inventory.StoredPackages.Values.Select(p => p.Amount), Is.EquivalentTo(new uint[] { 10u, 10u, 5u }));
        }

        [Test]
        public void Container_RoundTripsThroughThePersistableShape()
        {
            // The persistence constraint the foundational-rework spec bakes in now: a
            // container's state must be expressible as [{ x, y, definitionId + instance DTO,
            // amount }], with Package.Sender never serialized. No save file is written - this
            // asserts the shape is sufficient.
            var source = new CharacterInventory(new Vector2Int(4, 4), catalog);

            var swordPackage = new Package(source, Sword(), 1u);
            _ = source.TryAddToContainer(ref swordPackage);
            var arrowPackage = new Package(source, Arrows(), 5u);
            _ = source.TryAddToContainer(ref arrowPackage);

            // Flatten to the persistable rows.
            var rows = source.StoredPackages
                .Select(entry => (
                    x: entry.Key.x,
                    y: entry.Key.y,
                    dto: entry.Value.Item.ToDto(),
                    amount: entry.Value.Amount))
                .ToList();

            Assert.That(rows, Has.Count.EqualTo(2));

            // Rebuild a fresh container from the rows alone.
            var restored = new CharacterInventory(new Vector2Int(4, 4), catalog);
            foreach (var row in rows)
                _ = restored.AddAtPosition(
                    new Vector2Int(row.x, row.y),
                    new Package(restored, ItemInstance.FromDto(row.dto), row.amount));

            Assert.That(restored.StoredPackages.Keys, Is.EquivalentTo(source.StoredPackages.Keys));

            foreach (var position in source.StoredPackages.Keys)
            {
                var before = source.StoredPackages[position];
                var after = restored.StoredPackages[position];

                Assert.That(after.Item, Is.EqualTo(before.Item), $"instance at {position}");
                Assert.That(after.Amount, Is.EqualTo(before.Amount), $"amount at {position}");
            }
        }

        // ── CharacterEquipment + the injected interfaces ─────────────────────

        private static CharacterEquipment Equipment(IStatReceiver stats = null) =>
            new(new Vector2Int(14, 1), catalog, stats);

        [Test]
        public void CharacterEquipment_WithNoInjectedDeps_StillEquipsAndUnequips()
        {
            var equipment = Equipment();
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);

            var package = new Package(sender, Helm(4f), 1u);
            var equipped = equipment.TryAddToContainer(ref package);

            Assert.That(equipped, Is.True);
            Assert.That(equipment.StoredPackages, Has.Count.EqualTo(1));

            var stored = equipment.StoredPackages.Single();
            _ = equipment.RemoveAtPosition(stored.Key, stored.Value);

            Assert.That(equipment.StoredPackages, Is.Empty);
        }

        [Test]
        public void CharacterEquipment_OnEquip_AppliesTheItemsAffixesThroughTheStatReceiver()
        {
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);

            var package = new Package(new CharacterInventory(new Vector2Int(4, 4), catalog), Helm(4f), 1u);
            _ = equipment.TryAddToContainer(ref package);

            Assert.That(stats.Added.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }));
            Assert.That(stats.Removed, Is.Empty);
        }

        [Test]
        public void CharacterEquipment_RemoveAtPosition_LiftsTheItemsAffixesBackOff()
        {
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);

            var package = new Package(new CharacterInventory(new Vector2Int(4, 4), catalog), Helm(4f), 1u);
            _ = equipment.TryAddToContainer(ref package);
            var stored = equipment.StoredPackages.Single();

            _ = equipment.RemoveAtPosition(stored.Key, stored.Value);

            Assert.That(stats.Removed.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }));
        }

        [Test]
        public void CharacterEquipment_EquippingIntoAnOccupiedSlot_HandsTheDisplacedItemBackThroughThePackage()
        {
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);

            var first = new Package(sender, Helm(2f), 1u);
            _ = equipment.TryAddToContainer(ref first);

            var second = new Package(sender, Helm(9f), 1u);
            _ = equipment.TryAddToContainer(ref second);

            // The new helm is worn; the displaced one comes back through `second` for the
            // caller's transaction to re-home. Outside a transaction there is no sender /
            // cursor fallback any more - that path was QA-4's recursion (issue #12).
            Assert.That(equipment.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(equipment.StoredPackages.Values.Single().Item.Affixes[0].Modifier.Value, Is.EqualTo(9f));
            Assert.That(second.Item?.DefinitionId, Is.EqualTo(HelmId), "the old helm was handed back");
            Assert.That(second.Item.Affixes[0].Modifier.Value, Is.EqualTo(2f));

            // Stats: both helms applied on equip, the displaced one lifted.
            Assert.That(stats.Added.Count, Is.EqualTo(2));
            Assert.That(stats.Removed.Count, Is.EqualTo(1));
        }

        [Test]
        public void CharacterEquipment_ShiftEquippingA1H_WithBowAndShieldWorn_SwapsTheShieldNotTheBow()
        {
            var equipment = Equipment();
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);

            var bow = new Package(sender, new ItemInstance(BowId, ItemRarity.Common, 1, null), 1u);
            _ = equipment.TryAddToContainer(ref bow);
            var shield = new Package(sender, new ItemInstance(ShieldId, ItemRarity.Common, 1, null), 1u);
            _ = equipment.TryAddToContainer(ref shield);

            var sword = new Package(sender, Sword(), 1u);
            _ = equipment.TryAddToContainer(ref sword, 1); // shift = the second slot

            Assert.That(equipment.StoredPackages[new Vector2Int(12, 0)].Item.DefinitionId, Is.EqualTo(BowId), "the bow stays");
            Assert.That(equipment.StoredPackages[new Vector2Int(13, 0)].Item.DefinitionId, Is.EqualTo(SwordId), "the sword took the shield's slot");
            Assert.That(sword.Item?.DefinitionId, Is.EqualTo(ShieldId), "the shield was handed back");
        }

        [Test]
        public void CharacterEquipment_ShiftEquippingA1H_WithTheFirstSlotEmpty_StillTargetsTheSecondSlot()
        {
            var equipment = Equipment();
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);

            var first = Sword();
            var worn = new Package(sender, first, 1u);
            _ = equipment.TryAddToContainer(ref worn, 1);   // second slot, empty -> lands there
            Assert.That(equipment.StoredPackages.Keys, Is.EquivalentTo(new[] { new Vector2Int(13, 0) }));

            var incoming = new Package(sender, Sword(), 1u);
            _ = equipment.TryAddToContainer(ref incoming, 1); // 12 is empty, but shift still means 13

            Assert.That(equipment.StoredPackages.Keys, Is.EquivalentTo(new[] { new Vector2Int(13, 0) }));
            Assert.That(incoming.Item, Is.SameAs(first), "the displaced sword was handed back");
        }

        [Test]
        public void CharacterEquipment_ShiftEquippingARing_WithBothSlotsWorn_SwapsTheSecondRing()
        {
            var equipment = Equipment();
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);

            var ringA = new Package(sender, Ring(1f), 1u);
            _ = equipment.TryAddToContainer(ref ringA);
            var ringB = new Package(sender, Ring(2f), 1u);
            _ = equipment.TryAddToContainer(ref ringB);

            var incoming = new Package(sender, Ring(3f), 1u);
            _ = equipment.TryAddToContainer(ref incoming, 1);

            Assert.That(equipment.StoredPackages[new Vector2Int(10, 0)].Item.Affixes[0].Modifier.Value, Is.EqualTo(1f));
            Assert.That(equipment.StoredPackages[new Vector2Int(11, 0)].Item.Affixes[0].Modifier.Value, Is.EqualTo(3f));
            Assert.That(incoming.Item.Affixes[0].Modifier.Value, Is.EqualTo(2f), "the displaced ring was handed back");
        }

        [Test]
        public void CharacterEquipment_ShiftEquippingASingleSlotType_LandsOnItsOwnSlot()
        {
            var equipment = Equipment();
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);

            var bow = new Package(sender, new ItemInstance(BowId, ItemRarity.Common, 1, null), 1u);
            _ = equipment.TryAddToContainer(ref bow, 1);
            var shield = new Package(sender, new ItemInstance(ShieldId, ItemRarity.Common, 1, null), 1u);
            _ = equipment.TryAddToContainer(ref shield, 1);

            Assert.That(equipment.StoredPackages[new Vector2Int(12, 0)].Item.DefinitionId, Is.EqualTo(BowId), "a bow never enters 13");
            Assert.That(equipment.StoredPackages[new Vector2Int(13, 0)].Item.DefinitionId, Is.EqualTo(ShieldId), "a shield never enters 12");
        }

        // ── Hover compare targets ────────────────────────────────────────────

        private static ItemInstance Gear(string id) => new(id, ItemRarity.Common, 1, null);

        private static CharacterEquipment Wearing(CharacterInventory sender, params string[] ids)
        {
            var equipment = Equipment();
            foreach (var id in ids)
            {
                var package = new Package(sender, Gear(id), 1u);
                _ = equipment.TryAddToContainer(ref package);
            }
            return equipment;
        }

        private static string[] Ids(System.Collections.Generic.IReadOnlyList<Package> packages) =>
            packages.Select(p => p.Item.DefinitionId).ToArray();

        [Test]
        public void CompareTargets_A1HAgainstAWorn2H_SeesTheTwoHanderFromEitherSlot()
        {
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var equipment = Wearing(sender, GreatSwordId);

            foreach (var shift in new[] { false, true })
            {
                var (shown, against) = equipment.CompareTargets(Sword(), shift);

                Assert.That(Ids(shown), Is.EqualTo(new[] { GreatSwordId }), $"shown once (shift {shift})");
                Assert.That(Ids(against), Is.EqualTo(new[] { GreatSwordId }), $"the stats are measured against it (shift {shift})");
            }
        }

        [Test]
        public void CompareTargets_AShieldAgainstAWorn2H_SeesTheTwoHanderCoveringTheOffhandCell()
        {
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var equipment = Wearing(sender, GreatSwordId);

            var (shown, against) = equipment.CompareTargets(Gear(ShieldId), false);

            Assert.That(Ids(shown), Is.EqualTo(new[] { GreatSwordId }));
            Assert.That(Ids(against), Is.EqualTo(new[] { GreatSwordId }));
        }

        [Test]
        public void CompareTargets_A2HAgainstWeaponAndOffhand_ShowsBothAndMeasuresAgainstBoth()
        {
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var equipment = Wearing(sender, SwordId, ShieldId);

            var (shown, against) = equipment.CompareTargets(Gear(GreatSwordId), false);
            Assert.That(Ids(shown), Is.EqualTo(new[] { SwordId, ShieldId }));
            Assert.That(Ids(against), Is.EqualTo(new[] { SwordId, ShieldId }));

            (shown, against) = equipment.CompareTargets(Gear(GreatSwordId), true);
            Assert.That(Ids(shown), Is.EqualTo(new[] { ShieldId, SwordId }), "shift flips the order");
            Assert.That(Ids(against), Is.EquivalentTo(new[] { SwordId, ShieldId }), "but it still replaces both");
        }

        [Test]
        public void CompareTargets_A1HAgainstBowAndShield_ShiftSwapsWhichSlotLeadsAndIsMeasured()
        {
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var equipment = Wearing(sender, BowId, ShieldId);

            var (shown, against) = equipment.CompareTargets(Sword(), false);
            Assert.That(Ids(shown), Is.EqualTo(new[] { BowId, ShieldId }));
            Assert.That(Ids(against), Is.EqualTo(new[] { BowId }));

            (shown, against) = equipment.CompareTargets(Sword(), true);
            Assert.That(Ids(shown), Is.EqualTo(new[] { ShieldId, BowId }));
            Assert.That(Ids(against), Is.EqualTo(new[] { ShieldId }), "shift targets the second slot");
        }

        [Test]
        public void CompareTargets_A1HWithABowAndAnEmptyOffhand_DisplacesNothing()
        {
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var equipment = Wearing(sender, BowId);

            // The default equip fills the free off-hand; the bow stays - so there is nothing to replace.
            var (shown, against) = equipment.CompareTargets(Sword(), false);
            Assert.That(Ids(shown), Is.EqualTo(new[] { BowId }), "the bow is still listed for reference");
            Assert.That(against, Is.Empty, "but the sword does not replace it");

            // Shift names the second slot, which is the same free off-hand.
            (_, against) = equipment.CompareTargets(Sword(), true);
            Assert.That(against, Is.Empty);
        }

        [Test]
        public void CompareTargets_AShiftEquipOntoAFreeSecondSlot_IgnoresAWornFirstSlot()
        {
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var equipment = Wearing(sender, SwordId);   // lands in the first slot

            var (_, against) = equipment.CompareTargets(Sword(), true);
            Assert.That(against, Is.Empty, "shift is the second slot, and it is free");

            (_, against) = equipment.CompareTargets(Sword(), false);
            Assert.That(against, Is.Empty, "the default equip also takes the free second slot");
        }

        [Test]
        public void CompareTargets_ASingleSlotType_IgnoresShiftAndShowsOnlyItsOwnSlot()
        {
            var sender = new CharacterInventory(new Vector2Int(4, 4), catalog);
            var equipment = Wearing(sender, BowId, ShieldId);

            foreach (var shift in new[] { false, true })
            {
                var (shown, against) = equipment.CompareTargets(Gear(ShieldId), shift);

                Assert.That(Ids(shown), Is.EqualTo(new[] { ShieldId }));
                Assert.That(Ids(against), Is.EqualTo(new[] { ShieldId }));
            }
        }

        [Test]
        public void CompareTargets_WithNothingWornInTheSlots_ShowsNothing()
        {
            var equipment = Wearing(new CharacterInventory(new Vector2Int(4, 4), catalog));

            var (shown, against) = equipment.CompareTargets(Sword(), true);

            Assert.That(shown, Is.Empty);
            Assert.That(against, Is.Empty);
        }

        [Test]
        public void CharacterEquipment_ForceSwap_GivesUpInsteadOfRecursing_WhenAReHomeRoutesBackIntoAFullEquipment()
        {
            // QA-4's StackOverflowException: a displaced item re-homed back into the
            // equipment, which force-swapped again, without end. The force-swap now gives up
            // on re-entry and the whole move rolls back (issue #12).
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);
            var bench = new CharacterInventory(new Vector2Int(4, 4), catalog);

            var ringA = new Package(bench, Ring(1f), 1u);
            _ = equipment.TryAddToContainer(ref ringA);
            var ringB = new Package(bench, Ring(2f), 1u);
            _ = equipment.TryAddToContainer(ref ringB); // both ring slots are now taken
            var wornBefore = equipment.StoredPackages.Values.Select(p => p.Item).ToList();

            var loose = new Package(bench, Ring(3f), 1u);
            _ = bench.TryAddToContainer(ref loose);
            var benchSlot = bench.StoredPackages.Keys.Single();
            var storedRing = bench.StoredPackages[benchSlot];
            stats.Added.Clear();
            stats.Removed.Clear();

            Assert.That(() =>
            {
                // The only re-home target is the (full) equipment itself: the displaced ring
                // cannot land, so the move aborts rather than swapping a second time.
                using var transaction = new ItemTransaction(equipment, bench).ReHomeThrough(equipment).SwapInPlace();

                _ = bench.RemoveAtPosition(benchSlot, storedRing);
                var incoming = new Package(bench, storedRing.Item, storedRing.Amount);
                _ = equipment.TryAddToContainer(ref incoming);

                if (!transaction.Aborted)
                    transaction.Commit();
            }, Throws.Nothing);

            Assert.That(equipment.StoredPackages.Values.Select(p => p.Item), Is.EquivalentTo(wornBefore), "gear unchanged - the move rolled back");
            Assert.That(bench.StoredPackages.Values.Single().Item, Is.SameAs(storedRing.Item), "the loose ring is back on the bench");
            Assert.That(stats.Added, Is.Empty);
            Assert.That(stats.Removed, Is.Empty);
        }
    }
}
