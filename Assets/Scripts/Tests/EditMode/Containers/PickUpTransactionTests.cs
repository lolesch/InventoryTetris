using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// The one commit-or-rollback move through <see cref="IItemReceiver.PickUpItem"/>, shared
    /// by the shift-click-out-of-the-Stash retrieval (issue #86) and <see
    /// cref="VendorTransaction.Buy"/>'s player-path. <see cref="FakePlayer"/> mirrors
    /// <c>LocalPlayer.PickUpItem</c>'s own priority (equip, else bag) without the MonoBehaviour
    /// or the provider singleton, so this stays a pure container-seam test - prior art:
    /// <see cref="SellBasketQuickMoveTests"/>.
    /// </summary>
    [TestFixture]
    public sealed class PickUpTransactionTests
    {
        private const string SwordId = "test.sword";
        private const string HelmId = "test.helm";
        private const string PotionId = "test.potion";

        private TestCatalog catalog;

        [SetUp]
        public void SetCatalog()
        {
            catalog = new TestCatalog()
                // Sword gets two type-specific slots (dual-wield); Helm gets exactly one, so
                // it's the one to use when a test needs "the only slot is occupied" to mean it.
                .With(new TestDefinition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
                .With(new TestDefinition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
                .With(new TestDefinition { Id = PotionId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Potion, Footprint = ItemSize.OneByOne, BaseStackLimit = 5u });

            ItemView.Catalog = catalog;
        }

        [TearDown]
        public void ClearCatalog() => ItemView.Catalog = null;

        // ── fixtures ────────────────────────────────────────────────────────

        private static ItemInstance Sword() => new(SwordId, ItemRarity.Common, 1, null);
        private static ItemInstance Helm() => new(HelmId, ItemRarity.Common, 1, null);
        private static ItemInstance Potion() => new(PotionId, ItemRarity.Common, 1, null);

        private static CharacterInventory Stash(int width = 4, int height = 4) => new(new Vector2Int(width, height));
        private static CharacterInventory Inventory(int width = 4, int height = 4) => new(new Vector2Int(width, height));
        private static CharacterEquipment Equipment() => new(new Vector2Int(14, 1), null);

        /// <summary>Mirrors <c>LocalPlayer.PickUpItem</c>'s priority without the MonoBehaviour.</summary>
        private sealed class FakePlayer : IItemReceiver
        {
            private readonly CharacterEquipment equipment;
            private readonly CharacterInventory inventory;

            public FakePlayer(CharacterEquipment equipment, CharacterInventory inventory)
            {
                this.equipment = equipment;
                this.inventory = inventory;
            }

            public bool PickUpItem(ItemInstance item, uint amount)
            {
                var package = new Package(null, item, amount);

                if (ItemView.Of(item).Definition.Category == ItemCategory.Equipment
                    && equipment.autoEquip && equipment.AutoEquip(ref package))
                    return true;

                return inventory.TryAddToContainer(ref package);
            }
        }

        // ── non-equipment: lands in the Inventory, exactly as the plain move did ──

        [Test]
        public void Consumable_LandsInTheInventory()
        {
            var stash = Stash();
            var inventory = Inventory();
            var equipment = Equipment();
            _ = stash.AddAtPosition(new Vector2Int(0, 0), new Package(stash, Potion(), 1u));
            var potion = stash.StoredPackages[new Vector2Int(0, 0)].Item;

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0),
                new FakePlayer(equipment, inventory), inventory, equipment);

            Assert.That(moved, Is.True);
            Assert.That(stash.StoredPackages, Is.Empty, "the Stash cell was vacated");
            Assert.That(inventory.StoredPackages.Values, Has.Some.Matches<Package>(p => p.Item == potion), "the item is in the Inventory");
            Assert.That(equipment.StoredPackages, Is.Empty, "nothing was equipped");
        }

        // ── equipment, empty slot, auto-equip on: equips instead ──

        [Test]
        public void Equipment_EmptySlotAndAutoEquipOn_EquipsInstead()
        {
            var stash = Stash();
            var inventory = Inventory();
            var equipment = Equipment();
            equipment.autoEquip = true;
            _ = stash.AddAtPosition(new Vector2Int(0, 0), new Package(stash, Sword(), 1u));
            var sword = stash.StoredPackages[new Vector2Int(0, 0)].Item;

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0),
                new FakePlayer(equipment, inventory), inventory, equipment);

            Assert.That(moved, Is.True);
            Assert.That(stash.StoredPackages, Is.Empty, "the Stash cell was vacated");
            Assert.That(equipment.StoredPackages.Values, Has.Some.Matches<Package>(p => p.Item == sword), "the sword was equipped");
            Assert.That(inventory.StoredPackages, Is.Empty, "nothing landed in the Inventory");
        }

        // ── auto-equip off: lands in the Inventory even though the slot is empty ──

        [Test]
        public void Equipment_AutoEquipOff_LandsInTheInventory()
        {
            var stash = Stash();
            var inventory = Inventory();
            var equipment = Equipment();
            equipment.autoEquip = false;
            _ = stash.AddAtPosition(new Vector2Int(0, 0), new Package(stash, Sword(), 1u));
            var sword = stash.StoredPackages[new Vector2Int(0, 0)].Item;

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0),
                new FakePlayer(equipment, inventory), inventory, equipment);

            Assert.That(moved, Is.True);
            Assert.That(equipment.StoredPackages, Is.Empty, "auto-equip is off");
            Assert.That(inventory.StoredPackages.Values, Has.Some.Matches<Package>(p => p.Item == sword), "the sword landed in the Inventory instead");
        }

        // ── slot already occupied: auto-equip never force-swaps, falls to the Inventory ──

        [Test]
        public void Equipment_SlotAlreadyOccupied_LandsInTheInventory()
        {
            var stash = Stash();
            var inventory = Inventory();
            var equipment = Equipment();
            equipment.autoEquip = true;
            // Helm has exactly one type-specific slot (unlike Sword, which gets two for
            // dual-wield) - the one equipment type where "occupied" really means no room.
            var worn = new Package(null, Helm(), 1u);
            _ = equipment.TryAddToContainer(ref worn);

            _ = stash.AddAtPosition(new Vector2Int(0, 0), new Package(stash, Helm(), 1u));
            var incoming = stash.StoredPackages[new Vector2Int(0, 0)].Item;

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0),
                new FakePlayer(equipment, inventory), inventory, equipment);

            Assert.That(moved, Is.True);
            Assert.That(equipment.StoredPackages.Count, Is.EqualTo(1), "the worn helm was not swapped out");
            Assert.That(inventory.StoredPackages.Values, Has.Some.Matches<Package>(p => p.Item == incoming), "the second helm landed in the Inventory");
        }

        // ── nothing fits anywhere: rolls back, the item stays in the Stash ──

        [Test]
        public void NothingFitsAnywhere_ReturnsFalse_AndLeavesTheItemInTheStash()
        {
            var stash = Stash(1, 1);
            var inventory = Inventory(1, 1);
            var equipment = Equipment();
            equipment.autoEquip = true;

            // Fill the single Inventory cell with a non-stackable Sword so the incoming
            // potion can't just pile onto a matching stack - it truly has nowhere to land.
            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Sword(), 1u));
            _ = stash.AddAtPosition(new Vector2Int(0, 0), new Package(stash, Potion(), 1u));
            var potion = stash.StoredPackages[new Vector2Int(0, 0)].Item;

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0),
                new FakePlayer(equipment, inventory), inventory, equipment);

            Assert.That(moved, Is.False);
            Assert.That(stash.StoredPackages[new Vector2Int(0, 0)].Item, Is.SameAs(potion), "the item stayed in the Stash");
            Assert.That(inventory.StoredPackages.Count, Is.EqualTo(1), "the Inventory is unchanged");
        }

        // ── nothing stored at the cell ──────────────────────────────────────

        [Test]
        public void MissingSourcePackage_ReturnsFalse()
        {
            var stash = Stash();
            var inventory = Inventory();
            var equipment = Equipment();

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0),
                new FakePlayer(equipment, inventory), inventory, equipment);

            Assert.That(moved, Is.False);
            Assert.That(inventory.StoredPackages, Is.Empty);
        }

        [Test]
        public void NullPlayer_ReturnsFalse()
        {
            var stash = Stash();
            var inventory = Inventory();
            var equipment = Equipment();
            _ = stash.AddAtPosition(new Vector2Int(0, 0), new Package(stash, Potion(), 1u));

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0), null, inventory, equipment);

            Assert.That(moved, Is.False);
            Assert.That(stash.StoredPackages, Is.Not.Empty, "the item stayed put");
        }

        // ── stash fallback: handing the item back to its own source is not an acquisition ──

        /// <summary>A player whose bag is full and whose debug fallback drops into the Stash.</summary>
        private sealed class StashFallbackPlayer : IItemReceiver
        {
            private readonly CharacterInventory stash;

            public StashFallbackPlayer(CharacterInventory stash) => this.stash = stash;

            public bool PickUpItem(ItemInstance item, uint amount)
            {
                var package = new Package(null, item, amount);
                return stash.TryAddToContainer(ref package);
            }
        }

        [Test]
        public void StashFallbackBackIntoTheSource_RollsBackToTheOriginCell()
        {
            var stash = Stash();
            var inventory = Inventory();
            var equipment = Equipment();
            var origin = new Vector2Int(2, 2);
            _ = stash.AddAtPosition(origin, new Package(stash, Potion(), 1u));

            var moved = PickUpTransaction.Run(stash, origin, new StashFallbackPlayer(stash),
                inventory, equipment, stash);

            Assert.That(moved, Is.False);
            Assert.That(stash.StoredPackages.ContainsKey(origin), Is.True, "the item is back on its origin cell");
            Assert.That(stash.StoredPackages, Has.Count.EqualTo(1));
        }

        // ── onAcquired: runs only after a successful pick-up, before commit ──

        [Test]
        public void OnAcquired_RunsOnlyAfterASuccessfulPickUp()
        {
            var stash = Stash();
            var inventory = Inventory();
            var equipment = Equipment();
            _ = stash.AddAtPosition(new Vector2Int(0, 0), new Package(stash, Potion(), 1u));

            var invoked = false;

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0),
                new FakePlayer(equipment, inventory), inventory, equipment,
                onAcquired: _ => invoked = true);

            Assert.That(moved, Is.True);
            Assert.That(invoked, Is.True, "onAcquired ran once the pick-up succeeded");
        }

        [Test]
        public void OnAcquired_DoesNotRun_WhenThePickUpFails()
        {
            var stash = Stash(1, 1);
            var inventory = Inventory(1, 1);
            var equipment = Equipment();

            _ = inventory.AddAtPosition(new Vector2Int(0, 0), new Package(inventory, Sword(), 1u));
            _ = stash.AddAtPosition(new Vector2Int(0, 0), new Package(stash, Potion(), 1u));

            var invoked = false;

            var moved = PickUpTransaction.Run(stash, new Vector2Int(0, 0),
                new FakePlayer(equipment, inventory), inventory, equipment,
                onAcquired: _ => invoked = true);

            Assert.That(moved, Is.False);
            Assert.That(invoked, Is.False, "onAcquired never runs when the pick-up rolled back");
        }
    }
}
