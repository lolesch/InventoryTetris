using System.Collections.Generic;
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
    /// The movement matrix (issue #10): every non-vendor move routed through
    /// <see cref="ItemTransaction"/> conserves value, applies affixes only while a thing is
    /// equipped, and leaves the right package (or none) on the cursor. Each test opens the
    /// transaction the way the slot displays do - a <see cref="CursorHolder"/>, the touched
    /// containers enrolled, the origin container as the re-home target - and drives the move
    /// through <see cref="AbstractDimensionalContainer.AddAtPosition"/> /
    /// <see cref="AbstractDimensionalContainer.RemoveAtPosition"/> / <c>Sort</c>. The item
    /// under a drag's drop point goes to the hand; a right-click swaps it back into the
    /// origin and only overflows to the hand; a 2H's collateral off-hand is container-only.
    /// </summary>
    [TestFixture]
    public sealed class MovementMatrixTests
    {
        private const string SwordId = "test.sword";     // 1H, dual-wield
        private const string GreatSwordId = "test.2h";   // 2H
        private const string ShieldId = "test.shield";   // off-hand
        private const string RingId = "test.ring";
        private const string HelmId = "test.helm";
        private const string ArrowId = "test.arrow";

        [SetUp]
        public void SetCatalog() => ItemView.Catalog = new TestCatalog()
            .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = GreatSwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.GreatSword, Footprint = ItemSize.TwoByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = ShieldId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Shield, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = RingId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Ring, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = ArrowId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Arrow, Footprint = ItemSize.OneByOne, BaseStackLimit = 20u });

        [TearDown]
        public void ClearCatalog() => ItemView.Catalog = null;

        // ── fixtures ────────────────────────────────────────────────────────

        private static CharacterStatModifier Affix(StatName stat, float value) =>
            new(stat, new StatModifier(new Vector2Int(0, 100), value, StatModifierType.FlatAdd));

        private static ItemInstance Sword(float damage) => new(SwordId, ItemRarity.Rare, 7, new[] { Affix(StatName.PhysicalDamage, damage) });
        private static ItemInstance GreatSword(float damage) => new(GreatSwordId, ItemRarity.Rare, 9, new[] { Affix(StatName.PhysicalDamage, damage) });
        private static ItemInstance Shield(float armor) => new(ShieldId, ItemRarity.Magic, 5, new[] { Affix(StatName.Armor, armor) });
        private static ItemInstance Ring(float health) => new(RingId, ItemRarity.Magic, 3, new[] { Affix(StatName.Health, health) });
        private static ItemInstance Helm(float armor) => new(HelmId, ItemRarity.Rare, 5, new[] { Affix(StatName.Armor, armor) });
        private static ItemInstance Arrows() => new(ArrowId, ItemRarity.Common, 1, null);

        private static CharacterInventory Inventory(int width = 4, int height = 4) => new(new Vector2Int(width, height));
        private static CharacterEquipment Equipment(IStatReceiver stats = null, ICursorSink cursor = null) => new(new Vector2Int(14, 1), stats, cursor);

        private static Vector2Int SlotFor(EquipmentType type) => CharacterEquipment.GetTypeSpecificPositions(type).First();

        /// <summary>Drag-start: the slot display removes the item and the cursor now holds it. Not transactional.</summary>
        private static Package PickUp(AbstractDimensionalContainer from, Vector2Int position)
        {
            Assert.That(from.TryGetPackageAt(position, out var stored), Is.True, $"nothing stored at {position}");
            _ = from.RemoveAtPosition(position, stored);
            return stored;
        }

        /// <summary>
        /// The drop, exactly as <c>InventorySlotDisplay</c> / <c>EquipmentSlotDisplay</c>
        /// run it. <paramref name="inHand"/> is cleared on a committed move and left intact
        /// (still held) on a rollback.
        /// </summary>
        private static bool Drop(CursorHolder cursor, AbstractDimensionalContainer target, Vector2Int at, ref Package inHand,
            params AbstractDimensionalContainer[] reHome)
        {
            var enrolled = new List<AbstractDimensionalContainer> { target };
            enrolled.AddRange(reHome);

            using var transaction = new ItemTransaction(cursor, enrolled.ToArray()).ReHomeThrough(reHome);

            var displaced = target.AddAtPosition(at, inHand);

            if (displaced.IsValid)
                _ = transaction.TryReHomeToHandOrContainer(ref displaced);

            if (transaction.Aborted)
                return false; // inHand is untouched - the dragged item stays in hand

            transaction.Commit();
            inHand = displaced; // default unless a partial stack was left, which the cursor then holds
            return true;
        }

        /// <summary>
        /// Right-click equip, exactly as <c>InventorySlotDisplay.MoveItem</c> runs it: remove
        /// from <paramref name="source"/>, equip as a "swap in place", and swap displaced gear
        /// back into <paramref name="source"/> - overflowing at most one item to the hand.
        /// A player-driven move always executes unless a second item is left homeless.
        /// </summary>
        private static bool RightClickEquip(CursorHolder cursor, CharacterInventory source, CharacterEquipment equipment,
            Vector2Int position, Package stored)
        {
            using var transaction = new ItemTransaction(cursor, source, equipment).ReHomeThrough(source).SwapInPlace();

            _ = source.RemoveAtPosition(position, stored);
            var package = new Package(source, stored.Item, stored.Amount);
            _ = equipment.TryAddToContainer(ref package);

            if (transaction.Aborted)
                return false;

            transaction.Commit();
            return true;
        }

        /// <summary>
        /// Right-click unequip, exactly as <c>EquipmentSlotDisplay.MoveItem</c> runs it: the
        /// item always comes off - into the inventory, or in hand if it is full.
        /// </summary>
        private static void RightClickUnequip(CursorHolder cursor, CharacterEquipment equipment, CharacterInventory inventory,
            Vector2Int slot, Package stored)
        {
            using var transaction = new ItemTransaction(cursor, equipment, inventory).ReHomeThrough(inventory);

            _ = equipment.RemoveAtPosition(slot, stored);
            var package = new Package(equipment, stored.Item, stored.Amount);
            _ = transaction.TryReHomeToContainerOrHand(ref package);

            transaction.Commit();
        }

        /// <summary>
        /// Shift quick-move, exactly as the slot displays run it: the item leaves
        /// <paramref name="source"/> and lands in <paramref name="target"/>, or - if that is
        /// full - in the hand. Always executes.
        /// </summary>
        private static void ShiftQuickMove(CursorHolder cursor, AbstractDimensionalContainer source,
            AbstractDimensionalContainer target, Vector2Int position, Package stored)
        {
            using var transaction = new ItemTransaction(cursor, source, target).ReHomeThrough(target);

            _ = source.RemoveAtPosition(position, stored);
            var package = new Package(source, stored.Item, stored.Amount);
            _ = transaction.TryReHomeToContainerOrHand(ref package);

            transaction.Commit();
        }

        /// <summary>
        /// Every stored unit as its definition id - one entry per item in a stack. A stack
        /// merge folds one instance into another, so conservation is counted by id + amount;
        /// the tests that care about instance identity assert <c>Is.SameAs</c> directly.
        /// </summary>
        private static List<string> Units(params IEnumerable<Package>[] sources)
        {
            var units = new List<string>();

            foreach (var source in sources)
                foreach (var package in source)
                    for (var i = 0; i < package.Amount; i++)
                        if (package.Item != null)
                            units.Add(package.Item.DefinitionId);

            return units;
        }

        private static void AssertConserved(List<string> before, List<string> after) =>
            Assert.That(after.OrderBy(x => x), Is.EqualTo(before.OrderBy(x => x)), "value was not conserved by the move");

        // ── Inventory -> Inventory ──────────────────────────────────────────

        [Test]
        public void InventoryToInventory_RepositionToEmpty_MovesTheItemAndClearsTheHand()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var inventory = Inventory();

            var seed = new Package(inventory, Sword(6f), 1u);
            _ = inventory.TryAddToContainer(ref seed);
            var from = inventory.StoredPackages.Keys.Single();
            var instance = inventory.StoredPackages[from].Item;
            var before = Units(inventory.StoredPackages.Values);

            var inHand = PickUp(inventory, from);
            var committed = Drop(cursor, inventory, new Vector2Int(2, 2), ref inHand, inventory);

            Assert.That(committed, Is.True);
            Assert.That(cursor.IsFree, Is.True, "a clean landing leaves the hand empty");
            Assert.That(sink.Replaced, Is.Empty);
            Assert.That(inventory.StoredPackages.Keys.Single(), Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(inventory.StoredPackages[new Vector2Int(2, 2)].Item, Is.SameAs(instance));
            AssertConserved(before, Units(inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
        }

        [Test]
        public void InventoryToInventory_SwapWithOneItem_PutsTheDisplacedItemOnTheCursor()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var inventory = Inventory();

            var stay = new Package(inventory, Sword(6f), 1u);
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), stay);
            var moving = new Package(inventory, Helm(4f), 1u);
            _ = inventory.AddAtPosition(new Vector2Int(3, 3), moving);

            var swordInstance = inventory.StoredPackages[new Vector2Int(0, 0)].Item;
            var helmInstance = inventory.StoredPackages[new Vector2Int(3, 3)].Item;
            var before = Units(inventory.StoredPackages.Values);

            var inHand = PickUp(inventory, new Vector2Int(3, 3)); // the helm
            var committed = Drop(cursor, inventory, new Vector2Int(0, 0), ref inHand, inventory);

            Assert.That(committed, Is.True);
            Assert.That(inventory.StoredPackages[new Vector2Int(0, 0)].Item, Is.SameAs(helmInstance));
            Assert.That(sink.Replaced.Single().Item, Is.SameAs(swordInstance), "the displaced sword is on the cursor");
            Assert.That(cursor.IsFree, Is.False);
            AssertConserved(before, Units(inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
        }

        [Test]
        public void InventoryToInventory_MergeIntoAStack_ConservesEveryUnitInOneCell()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var inventory = Inventory();

            var first = new Package(inventory, Arrows(), 8u);
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), first);
            var second = new Package(inventory, Arrows(), 5u);
            _ = inventory.AddAtPosition(new Vector2Int(3, 3), second);
            var before = Units(inventory.StoredPackages.Values);

            var inHand = PickUp(inventory, new Vector2Int(3, 3));
            var committed = Drop(cursor, inventory, new Vector2Int(0, 0), ref inHand, inventory);

            Assert.That(committed, Is.True);
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(inventory.StoredPackages.Values.Single().Amount, Is.EqualTo(13u));
            Assert.That(cursor.IsFree, Is.True);
            AssertConserved(before, Units(inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
        }

        [Test]
        public void InventoryToInventory_AFootprintStraddlingTwoItems_IsRejectedByCanPlaceAt()
        {
            var inventory = Inventory();

            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Sword(6f), 1u));
            _ = inventory.AddAtPosition(new Vector2Int(1, 0), new Package(inventory, Helm(4f), 1u));

            // A 2x1 dropped across (0,0) would overlap both stored items.
            Assert.That(inventory.CanPlaceAt(new Vector2Int(0, 0), new Vector2Int(2, 1)), Is.False);
        }

        // ── Inventory -> Equipment ─────────────────────────────────────────

        [Test]
        public void InventoryToEquipment_EquipToEmptySlot_AppliesTheAffixOnCommit()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory();

            var seed = new Package(inventory, Helm(4f), 1u);
            _ = inventory.TryAddToContainer(ref seed);
            var seedPosition = inventory.StoredPackages.Keys.Single();
            var before = Units(inventory.StoredPackages.Values);

            var inHand = PickUp(inventory, seedPosition);
            var committed = Drop(cursor, equipment, SlotFor(EquipmentType.Helm), ref inHand, inventory);

            Assert.That(committed, Is.True);
            Assert.That(equipment.StoredPackages.Values.Single().Item.DefinitionId, Is.EqualTo(HelmId));
            Assert.That(stats.Added.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }));
            Assert.That(stats.Removed, Is.Empty);
            Assert.That(cursor.IsFree, Is.True);
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
        }

        [Test]
        public void InventoryToEquipment_SwapOneHandForOneHand_SwapsTheAffixesAndHandsTheOldWeaponOver()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory();

            var worn = new Package(inventory, Sword(6f), 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var wornInstance = equipment.StoredPackages.Values.Single().Item;
            stats.Added.Clear();
            stats.Removed.Clear();

            var incoming = new Package(inventory, Sword(11f), 1u);
            _ = inventory.TryAddToContainer(ref incoming);
            var incomingInstance = inventory.StoredPackages.Values.Single().Item;
            var before = Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values);

            var inHand = PickUp(inventory, inventory.StoredPackages.Keys.Single());
            var committed = Drop(cursor, equipment, SlotFor(EquipmentType.Sword), ref inHand, inventory);

            Assert.That(committed, Is.True);
            Assert.That(equipment.StoredPackages.Values.Single().Item, Is.SameAs(incomingInstance));
            Assert.That(sink.Replaced.Single().Item, Is.SameAs(wornInstance), "the old weapon is on the cursor");
            Assert.That(stats.Added.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 11f }));
            Assert.That(stats.Removed.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 6f }));
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
        }

        [Test]
        public void InventoryToEquipment_DragTwoHandedOverASingleWeapon_PutsTheOldWeaponInHand()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory();

            var worn = new Package(inventory, Sword(6f), 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var wornInstance = equipment.StoredPackages.Values.Single().Item;
            stats.Added.Clear();
            stats.Removed.Clear();

            var incoming = new Package(inventory, GreatSword(15f), 1u);
            _ = inventory.TryAddToContainer(ref incoming);
            var before = Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values);

            var inHand = PickUp(inventory, inventory.StoredPackages.Keys.Single());
            var committed = Drop(cursor, equipment, SlotFor(EquipmentType.GreatSword), ref inHand, inventory);

            Assert.That(committed, Is.True);
            Assert.That(equipment.StoredPackages.Values.Single().Item.DefinitionId, Is.EqualTo(GreatSwordId));
            Assert.That(sink.Replaced.Single().Item, Is.SameAs(wornInstance), "the old weapon is in hand");
            Assert.That(inventory.StoredPackages, Is.Empty, "nothing went to the inventory");
            Assert.That(stats.Added.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 15f }));
            Assert.That(stats.Removed.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 6f }));
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
        }

        [Test]
        public void InventoryToEquipment_DragTwoHandedOverWeaponAndOffHand_WithRoom_WeaponToHandOffHandToOrigin()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory();

            var weapon = new Package(inventory, Sword(6f), 1u);
            _ = equipment.TryAddToContainer(ref weapon);
            var offHand = new Package(inventory, Shield(3f), 1u);
            _ = equipment.TryAddToContainer(ref offHand);
            var wornWeapon = equipment.StoredPackages[SlotFor(EquipmentType.Sword)].Item;
            stats.Added.Clear();
            stats.Removed.Clear();

            var incoming = new Package(inventory, GreatSword(15f), 1u);
            _ = inventory.TryAddToContainer(ref incoming);
            var incomingInstance = inventory.StoredPackages.Values.Single().Item;
            var before = Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values);

            var inHand = PickUp(inventory, inventory.StoredPackages.Keys.Single());
            var committed = Drop(cursor, equipment, SlotFor(EquipmentType.GreatSword), ref inHand, inventory);

            Assert.That(committed, Is.True);
            Assert.That(equipment.StoredPackages.Values.Single().Item, Is.SameAs(incomingInstance), "only the 2H is worn");
            Assert.That(sink.Replaced.Single().Item, Is.SameAs(wornWeapon), "the weapon under the drop point is in hand");
            Assert.That(inventory.StoredPackages.Values.Single().Item.DefinitionId, Is.EqualTo(ShieldId),
                "the collateral off-hand swapped into the origin inventory, never the hand");
            Assert.That(stats.Added.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 15f }));
            Assert.That(stats.Removed.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 6f, 3f }));
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
        }

        [Test]
        public void InventoryToEquipment_DragTwoHandedOverWeaponAndOffHand_WhenTheOffHandCannotSwapBack_RollsBack()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory(1, 1);

            var weapon = new Package(inventory, Sword(6f), 1u);
            _ = equipment.TryAddToContainer(ref weapon);
            var offHand = new Package(inventory, Shield(3f), 1u);
            _ = equipment.TryAddToContainer(ref offHand);
            var wornBefore = equipment.StoredPackages.Values.Select(p => p.Item).ToList();
            stats.Added.Clear();
            stats.Removed.Clear();

            var filler = new Package(inventory, Arrows(), 1u);
            _ = inventory.TryAddToContainer(ref filler);

            var inHand = new Package(inventory, GreatSword(15f), 1u);
            var before = Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, new[] { inHand });

            var committed = Drop(cursor, equipment, SlotFor(EquipmentType.GreatSword), ref inHand, inventory);

            Assert.That(committed, Is.False, "the collateral off-hand cannot swap into the full origin");
            Assert.That(equipment.StoredPackages.Values.Select(p => p.Item), Is.EquivalentTo(wornBefore), "gear unchanged");
            Assert.That(inHand.IsValid, Is.True, "the 2H is still in hand");
            Assert.That(stats.Added, Is.Empty);
            Assert.That(stats.Removed, Is.Empty, "the swap's stat churn was queued and dropped");
            Assert.That(sink.Replaced, Is.Empty, "the weapon under the drop never reached the hand - the move rolled back first");
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
        }

        [Test]
        public void InventoryToEquipment_WrongEquipmentType_IsRejectedBeforeTheSlotIsTouched()
        {
            // EquipmentSlotDisplay.DropItem's guard: a slot only accepts its own type.
            var ringSlot = SlotFor(EquipmentType.Ring);

            Assert.That(CharacterEquipment.GetTypeSpecificPositions(EquipmentType.Helm), Has.None.EqualTo(ringSlot));
            Assert.That(CharacterEquipment.GetTypeSpecificPositions(EquipmentType.GreatSword), Has.None.EqualTo(ringSlot));
        }

        // ── Equipment -> Inventory ─────────────────────────────────────────

        [Test]
        public void EquipmentToInventory_UnequipToEmpty_LandsInTheInventoryAndLiftsTheAffixOnCommit()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory();

            var worn = new Package(inventory, Helm(4f), 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var slot = equipment.StoredPackages.Keys.Single();
            var stored = equipment.StoredPackages[slot];
            stats.Added.Clear();
            stats.Removed.Clear();
            var before = Units(equipment.StoredPackages.Values);

            RightClickUnequip(cursor, equipment, inventory, slot, stored);

            Assert.That(equipment.StoredPackages, Is.Empty);
            Assert.That(inventory.StoredPackages.Values.Single().Item, Is.SameAs(stored.Item));
            Assert.That(sink.Replaced, Is.Empty, "the inventory had room - nothing went in hand");
            Assert.That(stats.Removed.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }));
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced));
        }

        [Test]
        public void EquipmentToInventory_UnequipWithAFullInventory_ComesOffIntoTheHand()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory(1, 1);

            var worn = new Package(inventory, Helm(4f), 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var slot = equipment.StoredPackages.Keys.Single();
            var stored = equipment.StoredPackages[slot];
            stats.Added.Clear();
            stats.Removed.Clear();

            var filler = new Package(inventory, Arrows(), 1u);
            _ = inventory.TryAddToContainer(ref filler);
            var before = Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values);

            RightClickUnequip(cursor, equipment, inventory, slot, stored);

            Assert.That(equipment.StoredPackages, Is.Empty, "the item always comes off");
            Assert.That(sink.Replaced.Single().Item, Is.SameAs(stored.Item), "a full inventory sends it to the hand");
            Assert.That(stats.Removed.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }), "unequipped - affix lifted");
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced));
        }

        [Test]
        public void RightClickEquip_TwoHandedFromInventory_SwapsTheDisplacedGearIntoTheVacatedCells()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory(4, 1);

            var weapon = new Package(inventory, Sword(6f), 1u);
            _ = equipment.TryAddToContainer(ref weapon);
            var offHand = new Package(inventory, Shield(3f), 1u);
            _ = equipment.TryAddToContainer(ref offHand);
            stats.Added.Clear();
            stats.Removed.Clear();

            // 2H occupies (0,0)-(1,0); the rest of the row is full.
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, GreatSword(15f), 1u));
            _ = inventory.AddAtPosition(new Vector2Int(2, 0), new Package(inventory, Arrows(), 1u));
            _ = inventory.AddAtPosition(new Vector2Int(3, 0), new Package(inventory, Arrows(), 1u));
            var twoHander = inventory.StoredPackages[new Vector2Int(0, 0)];
            var before = Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values);

            var committed = RightClickEquip(cursor, inventory, equipment, new Vector2Int(0, 0), twoHander);

            Assert.That(committed, Is.True);
            Assert.That(equipment.StoredPackages.Values.Single().Item.DefinitionId, Is.EqualTo(GreatSwordId), "the 2H is worn");
            Assert.That(sink.Replaced, Is.Empty, "the 2H vacated two cells - both displaced items re-fit, nothing went to the hand");
            Assert.That(inventory.StoredPackages.Values.Select(p => p.Item.DefinitionId),
                Is.EquivalentTo(new[] { SwordId, ShieldId, ArrowId, ArrowId }));
            Assert.That(stats.Added.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 15f }));
            Assert.That(stats.Removed.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 6f, 3f }));
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced));
        }

        [Test]
        public void RightClickEquip_OneHandWhileTwoHandedWorn_SendsTheDisplacedTwoHanderToHandWhenItCannotReFit()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory(2, 1);

            var worn = new Package(inventory, GreatSword(20f), 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var wornTwoHander = equipment.StoredPackages.Values.Single().Item;
            stats.Added.Clear();
            stats.Removed.Clear();

            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Sword(6f), 1u));
            _ = inventory.AddAtPosition(new Vector2Int(1, 0), new Package(inventory, Arrows(), 1u));
            var oneHand = inventory.StoredPackages[new Vector2Int(0, 0)];
            var before = Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values);

            var committed = RightClickEquip(cursor, inventory, equipment, new Vector2Int(0, 0), oneHand);

            Assert.That(committed, Is.True, "a player-driven equip is not refused for lack of inventory room");
            Assert.That(equipment.StoredPackages.Values.Single().Item.DefinitionId, Is.EqualTo(SwordId), "the 1H is worn");
            Assert.That(sink.Replaced.Single().Item, Is.SameAs(wornTwoHander), "the 2x1 will not fit the single freed cell - it overflows to the hand");
            Assert.That(stats.Added.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 6f }));
            Assert.That(stats.Removed.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 20f }));
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced));
        }

        [Test]
        public void RightClickEquip_WhenBothDisplacedItemsAreHomeless_RollsBackTheWholeEquip()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var source = new CappedInventory(new Vector2Int(4, 1), acceptLimit: 0);

            var weapon = new Package(source, Sword(6f), 1u);
            _ = equipment.TryAddToContainer(ref weapon);
            var offHand = new Package(source, Shield(3f), 1u);
            _ = equipment.TryAddToContainer(ref offHand);
            var wornBefore = equipment.StoredPackages.Values.Select(p => p.Item).ToList();
            stats.Added.Clear();
            stats.Removed.Clear();

            _ = source.AddAtPosition(new Vector2Int(0, 0), new Package(source, GreatSword(15f), 1u));
            var twoHander = source.StoredPackages[new Vector2Int(0, 0)];
            var before = Units(equipment.StoredPackages.Values, source.StoredPackages.Values);

            var committed = RightClickEquip(cursor, source, equipment, new Vector2Int(0, 0), twoHander);

            Assert.That(committed, Is.False, "two homeless displaced items - the equip is refused");
            Assert.That(equipment.StoredPackages.Values.Select(p => p.Item), Is.EquivalentTo(wornBefore), "gear unchanged");
            Assert.That(source.StoredPackages[new Vector2Int(0, 0)].Item, Is.SameAs(twoHander.Item), "the 2H is back in the source");
            Assert.That(stats.Added, Is.Empty);
            Assert.That(stats.Removed, Is.Empty);
            Assert.That(sink.Replaced, Is.Empty);
            AssertConserved(before, Units(equipment.StoredPackages.Values, source.StoredPackages.Values, sink.Replaced));
        }

        // ── Equipment -> Equipment ─────────────────────────────────────────

        [Test]
        public void EquipmentToEquipment_RingForRing_DisplacesTheWornRingOntoTheCursorAndSwapsAffixes()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory();

            var ringSlots = CharacterEquipment.GetTypeSpecificPositions(EquipmentType.Ring);
            var a = new Package(inventory, Ring(10f), 1u);
            _ = equipment.TryAddToContainer(ref a);
            var b = new Package(inventory, Ring(20f), 1u);
            _ = equipment.TryAddToContainer(ref b);
            var wornInstance = equipment.StoredPackages[ringSlots[0]].Item;
            var otherInstance = equipment.StoredPackages[ringSlots[1]].Item;
            stats.Added.Clear();
            stats.Removed.Clear();

            var incoming = new Package(inventory, Ring(30f), 1u);
            _ = inventory.TryAddToContainer(ref incoming);
            var incomingInstance = inventory.StoredPackages.Values.Single().Item;
            var before = Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values);

            var inHand = PickUp(inventory, inventory.StoredPackages.Keys.Single());
            var committed = Drop(cursor, equipment, ringSlots[0], ref inHand, equipment, inventory);

            Assert.That(committed, Is.True);
            Assert.That(equipment.StoredPackages[ringSlots[0]].Item, Is.SameAs(incomingInstance));
            Assert.That(equipment.StoredPackages[ringSlots[1]].Item, Is.SameAs(otherInstance), "the other ring slot is untouched");
            Assert.That(sink.Replaced.Single().Item, Is.SameAs(wornInstance));
            Assert.That(stats.Added.Select(x => x.Modifier.Value), Is.EquivalentTo(new[] { 30f }));
            Assert.That(stats.Removed.Select(x => x.Modifier.Value), Is.EquivalentTo(new[] { 10f }));
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
        }

        [Test]
        public void EquipmentToEquipment_OneHandForOneHandAcrossTheWeaponSlots_KeepsBothWeaponsAndSwapsAffixes()
        {
            var stats = new FakeStatReceiver();
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment(stats, sink);
            var inventory = Inventory();

            var weaponSlots = CharacterEquipment.GetTypeSpecificPositions(EquipmentType.Sword);
            var mainHand = new Package(inventory, Sword(6f), 1u);
            _ = equipment.TryAddToContainer(ref mainHand);
            var offHand = new Package(inventory, Sword(9f), 1u);
            _ = equipment.TryAddToContainer(ref offHand);

            var mainInstance = equipment.StoredPackages[weaponSlots[0]].Item;
            var offInstance = equipment.StoredPackages[weaponSlots[1]].Item;
            stats.Added.Clear();
            stats.Removed.Clear();

            var incoming = new Package(inventory, Sword(13f), 1u);
            _ = inventory.TryAddToContainer(ref incoming);
            var incomingInstance = inventory.StoredPackages.Values.Single().Item;
            var before = Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values);

            var inHand = PickUp(inventory, inventory.StoredPackages.Keys.Single());
            var committed = Drop(cursor, equipment, weaponSlots[0], ref inHand, inventory);

            Assert.That(committed, Is.True);
            Assert.That(equipment.StoredPackages[weaponSlots[0]].Item, Is.SameAs(incomingInstance));
            Assert.That(equipment.StoredPackages[weaponSlots[1]].Item, Is.SameAs(offInstance), "the other weapon slot is untouched");
            Assert.That(sink.Replaced.Single().Item, Is.SameAs(mainInstance));
            Assert.That(stats.Added.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 13f }));
            Assert.That(stats.Removed.Select(a => a.Modifier.Value), Is.EquivalentTo(new[] { 6f }));
            AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
        }

        // ── Shift quick-move ──────────────────────────────────────────────

        [Test]
        public void ShiftQuickMove_WhenTheTargetHasRoom_MovesTheItemAcross()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var source = Inventory();
            var target = Inventory();

            _ = source.AddAtPosition(new Vector2Int(0, 0), new Package(source, Helm(4f), 1u));
            var stored = source.StoredPackages[new Vector2Int(0, 0)];
            var before = Units(source.StoredPackages.Values);

            ShiftQuickMove(cursor, source, target, new Vector2Int(0, 0), stored);

            Assert.That(source.StoredPackages, Is.Empty);
            Assert.That(target.StoredPackages.Values.Single().Item, Is.SameAs(stored.Item));
            Assert.That(sink.Replaced, Is.Empty);
            AssertConserved(before, Units(source.StoredPackages.Values, target.StoredPackages.Values, sink.Replaced));
        }

        [Test]
        public void ShiftQuickMove_WhenTheTargetIsFull_PutsTheItemInHand()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var source = Inventory();
            var target = Inventory(1, 1);

            _ = target.AddAtPosition(new Vector2Int(0, 0), new Package(target, Arrows(), 1u));
            _ = source.AddAtPosition(new Vector2Int(0, 0), new Package(source, Helm(4f), 1u));
            var stored = source.StoredPackages[new Vector2Int(0, 0)];
            var before = Units(source.StoredPackages.Values, target.StoredPackages.Values);

            ShiftQuickMove(cursor, source, target, new Vector2Int(0, 0), stored);

            Assert.That(source.StoredPackages, Is.Empty, "the item always leaves the source");
            Assert.That(target.StoredPackages.Values.Single().Item.DefinitionId, Is.EqualTo(ArrowId), "the target is untouched");
            Assert.That(sink.Replaced.Single().Item, Is.SameAs(stored.Item), "a full target sends it to the hand");
            AssertConserved(before, Units(source.StoredPackages.Values, target.StoredPackages.Values, sink.Replaced));
        }

        // ── QA-4 regression pin (the give-up condition itself is #12) ───────

        [Test]
        public void TwoHandedOverWeaponAndOffHand_NeverThrowsAndNeverLosesGear_OnEitherBranch()
        {
            foreach (var inventorySize in new[] { new Vector2Int(4, 4), new Vector2Int(1, 1) })
            {
                var stats = new FakeStatReceiver();
                var sink = new FakeCursorSink();
                var cursor = new CursorHolder(sink);
                var equipment = Equipment(stats, sink);
                var inventory = new CharacterInventory(inventorySize);

                var weapon = new Package(inventory, Sword(6f), 1u);
                _ = equipment.TryAddToContainer(ref weapon);
                var offHand = new Package(inventory, Shield(3f), 1u);
                _ = equipment.TryAddToContainer(ref offHand);

                if (inventorySize == new Vector2Int(1, 1))
                {
                    var filler = new Package(inventory, Arrows(), 1u);
                    _ = inventory.TryAddToContainer(ref filler);
                }

                var inHand = new Package(inventory, GreatSword(15f), 1u);
                var before = Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, new[] { inHand });

                Assert.That(() => Drop(cursor, equipment, SlotFor(EquipmentType.GreatSword), ref inHand, inventory),
                    Throws.Nothing, $"inventory {inventorySize}");

                AssertConserved(before, Units(equipment.StoredPackages.Values, inventory.StoredPackages.Values, sink.Replaced, new[] { inHand }));
            }
        }

        // ── Sort ───────────────────────────────────────────────────────────

        [Test]
        public void Sort_RemoveAllThenReAdd_ConservesEveryItem()
        {
            var inventory = Inventory(6, 4);

            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Helm(4f), 1u));
            _ = inventory.AddAtPosition(new Vector2Int(2, 1), new Package(inventory, Sword(6f), 1u));
            _ = inventory.AddAtPosition(new Vector2Int(4, 3), new Package(inventory, Arrows(), 7u));
            _ = inventory.AddAtPosition(new Vector2Int(5, 0), new Package(inventory, Ring(10f), 1u));
            var before = Units(inventory.StoredPackages.Values);

            inventory.Sort();

            AssertConserved(before, Units(inventory.StoredPackages.Values));
        }

        [Test]
        public void Sort_WhenTheReAddedLayoutWillNotReFit_RollsBackRatherThanDroppingItems()
        {
            var inventory = new CappedInventory(new Vector2Int(4, 4), acceptLimit: 2);

            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Helm(1f), 1u));
            _ = inventory.AddAtPosition(new Vector2Int(1, 0), new Package(inventory, Helm(2f), 1u));
            _ = inventory.AddAtPosition(new Vector2Int(2, 0), new Package(inventory, Helm(3f), 1u));
            var before = Units(inventory.StoredPackages.Values);
            var positionsBefore = inventory.StoredPackages.Keys.OrderBy(p => p.x).ToList();

            inventory.Sort();

            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(3), "no item was dropped");
            Assert.That(inventory.StoredPackages.Keys.OrderBy(p => p.x), Is.EqualTo(positionsBefore), "the pre-sort layout is restored");
            AssertConserved(before, Units(inventory.StoredPackages.Values));
        }

        /// <summary>A <see cref="CharacterInventory"/> that refuses to re-accept items after
        /// <paramref name="acceptLimit"/> calls - to prove <c>Sort</c> rolls the whole re-add
        /// back rather than dropping what will not fit.</summary>
        private sealed class CappedInventory : CharacterInventory
        {
            private readonly int acceptLimit;
            private int reAdds;

            public CappedInventory(Vector2Int dimensions, int acceptLimit) : base(dimensions) => this.acceptLimit = acceptLimit;

            public override bool TryAddToContainer(ref Package package) =>
                reAdds++ < acceptLimit && base.TryAddToContainer(ref package);
        }
    }
}
