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
    /// The return-to-origin primitive (issue #29): a cancelled or interrupted drag hands its
    /// Package to <see cref="ReturnToOrigin.Return"/>, which tries the exact origin cell
    /// first, falls back to the backpack, and - if neither has room - hands the Package back
    /// to the caller unchanged so it stays on the cursor. Assertions are on container
    /// contents, item count and the character sheet, never on how a refresh fired. Prior art:
    /// <see cref="MovementMatrixTests"/>'s <c>PickUp</c> - a drag pick-up is a non-transactional
    /// <c>RemoveAtPosition</c>, exactly as the slot displays run it.
    /// </summary>
    [TestFixture]
    public sealed class ReturnToOriginTests
    {
        private const string SwordId = "test.sword";
        private const string HelmId = "test.helm";
        private const string RingId = "test.ring";
        private const string ArrowId = "test.arrow";

        [SetUp]
        public void SetCatalog() => ItemView.Catalog = new TestCatalog()
            .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = RingId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Ring, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new TestDefinition { Id = ArrowId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Arrow, Footprint = ItemSize.OneByOne, BaseStackLimit = 20u });

        [TearDown]
        public void ClearCatalog() => ItemView.Catalog = null;

        // ── fixtures ────────────────────────────────────────────────────────

        private static CharacterStatModifier Affix(StatName stat, float value) =>
            new(stat, new StatModifier(new Vector2Int(0, 100), value, StatModifierType.FlatAdd));

        private static ItemInstance Sword() => new(SwordId, ItemRarity.Rare, 7, new[] { Affix(StatName.PhysicalDamage, 6f) });
        private static ItemInstance Helm(float armor) => new(HelmId, ItemRarity.Rare, 5, new[] { Affix(StatName.Armor, armor) });
        private static ItemInstance Ring(float health) => new(RingId, ItemRarity.Magic, 3, new[] { Affix(StatName.Health, health) });
        private static ItemInstance Arrows() => new(ArrowId, ItemRarity.Common, 1, null);

        private static CharacterInventory Inventory(int width = 4, int height = 4) => new(new Vector2Int(width, height));
        private static CharacterEquipment Equipment(IStatReceiver stats = null) => new(new Vector2Int(14, 1), stats);
        private static Vector2Int SlotFor(EquipmentType type) => CharacterEquipment.GetTypeSpecificPositions(type).First();

        /// <summary>Drag pick-up: the slot display removes the item and the cursor now holds
        /// it - not transactional, exactly as <c>MovementMatrixTests.PickUp</c> runs it.</summary>
        private static Package PickUp(AbstractDimensionalContainer from, Vector2Int position)
        {
            Assert.That(from.TryGetPackageAt(position, out var stored), Is.True, $"nothing stored at {position}");
            _ = from.RemoveAtPosition(position, stored);
            return stored;
        }

        /// <summary>
        /// The drop, exactly as <c>MovementMatrixTests.Drop</c> runs it - and, transitively,
        /// exactly as <c>InventorySlotDisplay</c> / <c>EquipmentSlotDisplay</c> run it: the
        /// target's <see cref="AbstractDimensionalContainer.AddAtPosition"/> returns whatever
        /// it displaced (a plain container swap), or - for <see cref="CharacterEquipment"/> -
        /// has already re-homed it internally, in which case it comes back invalid and there
        /// is nothing left to do here.
        /// </summary>
        private static bool Drop(CursorHolder cursor, AbstractDimensionalContainer target, Vector2Int at, ref Package inHand,
            params AbstractDimensionalContainer[] reHome)
        {
            var enrolled = new List<AbstractDimensionalContainer> { target };
            enrolled.AddRange(reHome);

            using var transaction = new ItemTransaction(cursor, enrolled.ToArray()).ReHomeThrough(reHome);

            var displaced = target.AddAtPosition(at, inHand);

            if (displaced.IsValid)
                _ = transaction.TryReHomeToHandOrContainer(ref displaced, target, at);

            if (transaction.Aborted)
                return false;

            transaction.Commit();
            inHand = displaced;
            return true;
        }

        // ── origin first ───────────────────────────────────────────────────

        [Test]
        public void Return_ToAFreeOriginCell_PlacesItExactlyThereAndClearsTheCursor()
        {
            var inventory = Inventory();
            var backpack = Inventory();
            _ = inventory.AddAtPosition(new Vector2Int(2, 1), new Package(inventory, Sword(), 1u));
            var instance = inventory.StoredPackages[new Vector2Int(2, 1)].Item;

            var inHand = PickUp(inventory, new Vector2Int(2, 1));
            var leftOnCursor = ReturnToOrigin.Return(inHand, inventory, new Vector2Int(2, 1), backpack);

            Assert.That(leftOnCursor.IsValid, Is.False, "the return found a home");
            Assert.That(inventory.StoredPackages.Keys.Single(), Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(inventory.StoredPackages[new Vector2Int(2, 1)].Item, Is.SameAs(instance));
            Assert.That(backpack.StoredPackages, Is.Empty, "the origin took it - the backpack was never touched");
        }

        [Test]
        public void Return_ToAFreeOriginCellInEquipment_ReEquipsAndReappliesTheAffix()
        {
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);
            var backpack = Inventory();
            var worn = new Package(backpack, Helm(4f), 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var slot = equipment.StoredPackages.Keys.Single();
            var instance = equipment.StoredPackages[slot].Item;
            stats.Added.Clear();

            var inHand = PickUp(equipment, slot);
            Assert.That(stats.Removed.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }), "picking it up lifted the affix");

            var leftOnCursor = ReturnToOrigin.Return(inHand, equipment, slot, backpack);

            Assert.That(leftOnCursor.IsValid, Is.False);
            Assert.That(equipment.StoredPackages[slot].Item, Is.SameAs(instance), "re-equipped at the same slot");
            Assert.That(stats.Added.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }), "the affix is re-applied on return");
            Assert.That(backpack.StoredPackages, Is.Empty);
        }

        [Test]
        public void Return_APile_ReturnsTheWholeStackToTheOriginCell()
        {
            var inventory = Inventory();
            var backpack = Inventory();
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Arrows(), 12u));

            var inHand = PickUp(inventory, new Vector2Int(0, 0));
            var leftOnCursor = ReturnToOrigin.Return(inHand, inventory, new Vector2Int(0, 0), backpack);

            Assert.That(leftOnCursor.IsValid, Is.False);
            Assert.That(inventory.StoredPackages[new Vector2Int(0, 0)].Amount, Is.EqualTo(12u), "the full stack came back, not part of it");
        }

        // ── backpack fallback ──────────────────────────────────────────────

        [Test]
        public void Return_WhenTheOriginCellIsNowOccupied_FallsBackToTheBackpack()
        {
            var inventory = Inventory();
            var backpack = Inventory();
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Sword(), 1u));
            var swordInstance = inventory.StoredPackages[new Vector2Int(0, 0)].Item;

            var inHand = PickUp(inventory, new Vector2Int(0, 0));
            // Something else has since taken the vacated cell - the origin slot is gone.
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Helm(4f), 1u));

            var leftOnCursor = ReturnToOrigin.Return(inHand, inventory, new Vector2Int(0, 0), backpack);

            Assert.That(leftOnCursor.IsValid, Is.False);
            Assert.That(inventory.StoredPackages[new Vector2Int(0, 0)].Item.DefinitionId, Is.EqualTo(HelmId), "the occupying item is untouched");
            Assert.That(backpack.StoredPackages.Values.Single().Item, Is.SameAs(swordInstance), "the sword landed in the backpack instead");
        }

        [Test]
        public void Return_WhenTheOriginContainerIsGone_FallsBackToTheBackpack()
        {
            var backpack = Inventory();
            var sword = new Package(null, Sword(), 1u);

            var leftOnCursor = ReturnToOrigin.Return(sword, null, new Vector2Int(0, 0), backpack);

            Assert.That(leftOnCursor.IsValid, Is.False);
            Assert.That(backpack.StoredPackages.Values.Single().Item, Is.SameAs(sword.Item));
        }

        [Test]
        public void Return_WhenTheEquipmentSlotIsNowWornByAnotherItem_FallsBackToTheBackpackWithoutReapplyingStats()
        {
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);
            var backpack = Inventory();
            var worn = new Package(backpack, Helm(4f), 1u);
            _ = equipment.TryAddToContainer(ref worn);
            var slot = equipment.StoredPackages.Keys.Single();

            var inHand = PickUp(equipment, slot);
            var replacement = new Package(backpack, Helm(9f), 1u);
            _ = equipment.TryAddToContainer(ref replacement); // a different helm now worn at the same slot
            stats.Added.Clear();
            stats.Removed.Clear();

            var leftOnCursor = ReturnToOrigin.Return(inHand, equipment, slot, backpack);

            Assert.That(leftOnCursor.IsValid, Is.False);
            Assert.That(equipment.StoredPackages[slot].Item.DefinitionId, Is.EqualTo(HelmId), "the worn replacement is untouched");
            Assert.That(backpack.StoredPackages.Values.Single().Item, Is.SameAs(inHand.Item), "the original helm landed in the backpack");
            Assert.That(stats.Added, Is.Empty, "it never re-equipped, so no affix was re-applied");
            Assert.That(stats.Removed, Is.Empty, "the worn replacement was never touched");
        }

        [Test]
        public void Return_ToTheWrongEquipmentSlotType_FallsBackToTheBackpack()
        {
            // A ring can never return to a helm's slot - CanReturnTo rejects the type mismatch
            // before it ever looks at occupancy.
            var equipment = Equipment();
            var backpack = Inventory();
            var ring = new Package(backpack, Ring(10f), 1u);

            var leftOnCursor = ReturnToOrigin.Return(ring, equipment, SlotFor(EquipmentType.Helm), backpack);

            Assert.That(leftOnCursor.IsValid, Is.False);
            Assert.That(backpack.StoredPackages.Values.Single().Item, Is.SameAs(ring.Item));
        }

        // ── failure stays on the cursor ────────────────────────────────────

        [Test]
        public void Return_WhenNeitherOriginNorBackpackHaveRoom_LeavesThePackageOnTheCursorAndLosesNothing()
        {
            var inventory = Inventory(1, 1);
            var backpack = Inventory(1, 1);
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Sword(), 1u));

            var inHand = PickUp(inventory, new Vector2Int(0, 0));
            // The origin's only cell and the backpack's only cell are both taken before the cancel resolves.
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Helm(4f), 1u));
            _ = backpack.AddAtPosition(new Vector2Int(0, 0), new Package(backpack, Helm(4f), 1u));

            var leftOnCursor = ReturnToOrigin.Return(inHand, inventory, new Vector2Int(0, 0), backpack);

            Assert.That(leftOnCursor.IsValid, Is.True, "the package stays on the cursor");
            Assert.That(leftOnCursor.Item, Is.SameAs(inHand.Item));
            Assert.That(leftOnCursor.Amount, Is.EqualTo(inHand.Amount));
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1), "the occupying helm is untouched");
            Assert.That(backpack.StoredPackages, Has.Count.EqualTo(1), "the backpack filler is untouched");
        }

        [Test]
        public void Return_AnInvalidPackage_DoesNothing()
        {
            var backpack = Inventory();

            var leftOnCursor = ReturnToOrigin.Return(default, null, default, backpack);

            Assert.That(leftOnCursor.IsValid, Is.False);
            Assert.That(backpack.StoredPackages, Is.Empty);
        }

        // ── CanReturnTo ────────────────────────────────────────────────────

        [Test]
        public void CanReturnTo_OnInventory_TrueOnlyForAGenuinelyEmptyCell()
        {
            var inventory = Inventory();
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Sword(), 1u));

            Assert.That(inventory.CanReturnTo(new Vector2Int(1, 1), Helm(4f)), Is.True);
            Assert.That(inventory.CanReturnTo(new Vector2Int(0, 0), Helm(4f)), Is.False, "occupied");
        }

        [Test]
        public void CanReturnTo_OnEquipment_RequiresTheRightSlotTypeAndEmptiness()
        {
            var equipment = Equipment();
            var helmSlot = SlotFor(EquipmentType.Helm);
            var ringSlots = CharacterEquipment.GetTypeSpecificPositions(EquipmentType.Ring);

            Assert.That(equipment.CanReturnTo(helmSlot, Helm(4f)), Is.True);
            Assert.That(equipment.CanReturnTo(ringSlots[0], Helm(4f)), Is.False, "wrong slot for the type");

            var worn = new Package(equipment, Helm(9f), 1u);
            _ = equipment.TryAddToContainer(ref worn);

            Assert.That(equipment.CanReturnTo(helmSlot, Helm(4f)), Is.False, "the slot is occupied, not empty");
        }

        // ── mid-drag swap hands the cursor the displaced item's real origin (issue #29 follow-up) ──
        //
        // DragProvider.CancelDrag used to compute the return-to cell from the drag's own
        // pick-up (Origin/PositionOffset), set once at the start of the drag. A drop that
        // displaces a *different* item onto the cursor (ICursorSink.ReplacePackage) never
        // updated that: cancelling right after such a swap re-homed the swapped-out item
        // against the original pick-up's cell, not the slot it actually came from. These
        // tests drive the swap the way EquipmentSlotDisplay.DropItem does (CursorHolder +
        // ItemTransaction, no GUI) and check the origin ICursorSink is told.

        [Test]
        public void Drop_DisplacingAWornRing_TellsTheCursorSinkTheRingsRealSlot_NotTheDragsOwnPickupSlot()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment();
            var inventory = Inventory();

            var ringSlots = CharacterEquipment.GetTypeSpecificPositions(EquipmentType.Ring);
            var slotA = ringSlots[0];
            var slotB = ringSlots[1];

            var ringA = new Package(inventory, Ring(5f), 1u);
            _ = equipment.AddAtPosition(slotA, ringA);
            var ringB = new Package(inventory, Ring(8f), 1u);
            _ = equipment.AddAtPosition(slotB, ringB);
            var ringBInstance = equipment.StoredPackages[slotB].Item;

            // The drag starts by lifting ringA off slotA - a different cell from the ring
            // the drop is about to displace.
            var inHand = PickUp(equipment, slotA);
            var committed = Drop(cursor, equipment, slotB, ref inHand, inventory);

            Assert.That(committed, Is.True);
            Assert.That(sink.Replaced.Single().Item, Is.SameAs(ringBInstance), "ringB was displaced onto the cursor");

            var (origin, originPosition) = sink.Origins.Single();
            Assert.That(origin, Is.SameAs(equipment));
            Assert.That(originPosition, Is.EqualTo(slotB), "the sink must be told ringB's real slot");
            Assert.That(originPosition, Is.Not.EqualTo(slotA), "not the drag's own pick-up slot, which ringB never occupied");
        }

        [Test]
        public void CancelAfterMidDragRingSwap_UsesTheDisplacedRingsRealSlot_NotTheStaleDragOrigin()
        {
            var sink = new FakeCursorSink();
            var cursor = new CursorHolder(sink);
            var equipment = Equipment();
            var inventory = Inventory();

            var ringSlots = CharacterEquipment.GetTypeSpecificPositions(EquipmentType.Ring);
            var slotA = ringSlots[0];
            var slotB = ringSlots[1];

            var ringA = new Package(inventory, Ring(5f), 1u);
            _ = equipment.AddAtPosition(slotA, ringA);
            var ringB = new Package(inventory, Ring(8f), 1u);
            _ = equipment.AddAtPosition(slotB, ringB);
            var ringBInstance = equipment.StoredPackages[slotB].Item;

            var inHand = PickUp(equipment, slotA); // the drag's own origin: slotA, now empty
            var committed = Drop(cursor, equipment, slotB, ref inHand, inventory);
            Assert.That(committed, Is.True);

            var handed = sink.Replaced.Single();
            var (realOrigin, realOriginPosition) = sink.Origins.Single();

            // Sanity check the hazard the fix closes: the stale drag-origin slot is still a
            // structurally valid, empty ring slot, so a cancel using it would have silently
            // "succeeded" by re-equipping ringB somewhere it never lived.
            Assert.That(equipment.CanReturnTo(slotA, handed.Item), Is.True,
                "the stale origin still looks perfectly returnable - that's what made the old bug silent");

            // Cancelling with the real, captured origin correctly sees slotB is now taken by
            // ringA and falls back to the backpack instead of re-equipping at the stale slot.
            var leftOnCursor = ReturnToOrigin.Return(handed, realOrigin, realOriginPosition, inventory);

            Assert.That(leftOnCursor.IsValid, Is.False, "the cancel found a home");
            Assert.That(equipment.StoredPackages[slotB].Item, Is.SameAs(ringA.Item), "slotB still correctly holds ringA");
            Assert.That(equipment.StoredPackages.ContainsKey(slotA), Is.False, "the stale drag-origin slot never got ringB back");
            Assert.That(inventory.StoredPackages.Values.Single().Item, Is.SameAs(ringBInstance), "ringB safely landed in the backpack instead");
        }
    }
}
