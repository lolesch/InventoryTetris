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
        // A file-local ItemDefinition / IItemCatalog - the pure InventorySystem.Items.Tests
        // fakes are in a separate test asmdef, and the contract is an interface precisely
        // so a stand-in is a few lines.
        private sealed class Definition : ItemDefinition
        {
            public string Id { get; set; } = "test.item";
            public ItemCategory Category { get; set; } = ItemCategory.Equipment;
            public ItemSize Footprint { get; set; } = ItemSize.OneByOne;
            public uint BaseStackLimit { get; set; } = 1u;
            public IReadOnlyList<AffixSlot> AffixPool { get; set; } = System.Array.Empty<AffixSlot>();
            public IReadOnlyList<CharacterStatModifier> ImplicitStats { get; set; } = System.Array.Empty<CharacterStatModifier>();
            public ItemRequirement Requirement { get; set; } = ItemRequirement.None;
            public bool IsUnique { get; set; }
            public IReadOnlyList<CharacterStatModifier> UniqueAffixes { get; set; } = System.Array.Empty<CharacterStatModifier>();
            public EquipmentType EquipmentType { get; set; } = EquipmentType.NONE;
            public ConsumableType ConsumableType { get; set; } = ConsumableType.NONE;
            public CurrencyType CurrencyType { get; set; } = CurrencyType.NONE;
        }

        private sealed class Catalog : IItemCatalog
        {
            private readonly Dictionary<string, ItemDefinition> byId = new();
            public Catalog With(ItemDefinition definition) { byId[definition.Id] = definition; return this; }

            public ItemDefinition Definition(string id) =>
                byId.TryGetValue(id, out var definition) ? definition : throw new KeyNotFoundException(id);

            public IEnumerable<ItemDefinition> OfCategory(ItemCategory category)
            {
                foreach (var definition in byId.Values)
                    if (definition.Category == category)
                        yield return definition;
            }
        }

        /// <summary>Records what CharacterEquipment applies to / lifts off the character.</summary>
        private sealed class FakeStatReceiver : IStatReceiver
        {
            public readonly List<CharacterStatModifier> Added = new();
            public readonly List<CharacterStatModifier> Removed = new();
            public void AddItemStats(IReadOnlyList<CharacterStatModifier> stats) => Added.AddRange(stats);
            public void RemoveItemStats(IReadOnlyList<CharacterStatModifier> stats) => Removed.AddRange(stats);
        }

        /// <summary>Records the packages a swap could not re-home in a container.</summary>
        private sealed class FakeCursorSink : ICursorSink
        {
            public readonly List<Package> Replaced = new();
            public void ReplacePackage(Package package) => Replaced.Add(package);
        }

        private const string SwordId = "test.sword";
        private const string ArrowId = "test.arrow";
        private const string HelmId = "test.helm";
        private const string RingId = "test.ring";

        [SetUp]
        public void SetCatalog() => ItemView.Catalog = new Catalog()
            .With(new Definition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new Definition { Id = ArrowId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Arrow, Footprint = ItemSize.OneByOne, BaseStackLimit = 10u })
            .With(new Definition { Id = HelmId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new Definition { Id = RingId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Ring, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u });

        [TearDown]
        public void ClearCatalog() => ItemView.Catalog = null;

        private static CharacterStatModifier Affix(StatName stat, float value) =>
            new(stat, new StatModifier(new Vector2Int(0, 100), value, StatModifierType.FlatAdd));

        private static ItemInstance Sword() => new(SwordId, ItemRarity.Rare, 7, new[]
        {
            Affix(StatName.PhysicalDamage, 6f),
        });

        private static ItemInstance Arrows() => new(ArrowId, ItemRarity.Common, 1, null);

        private static ItemInstance Helm(float armor) => new(HelmId, ItemRarity.Rare, 5, new[] { Affix(StatName.Armor, armor) });
        private static ItemInstance Ring(float value) => new(RingId, ItemRarity.Magic, 3, new[] { Affix(StatName.Health, value) });

        [Test]
        public void CharacterInventory_NewlyConstructed_IsEmptyWithTheGivenCapacity()
        {
            var inventory = new CharacterInventory(new Vector2Int(4, 4));

            Assert.That(inventory.StoredPackages, Is.Empty);
            Assert.That(inventory.Capacity, Is.EqualTo(16));
        }

        [Test]
        public void CharacterInventory_AfterAddingAPackage_HoldsExactlyThatItem()
        {
            var inventory = new CharacterInventory(new Vector2Int(4, 4));
            var package = new Package(inventory, Sword(), 1u);

            var accepted = inventory.TryAddToContainer(ref package);

            Assert.That(accepted, Is.True);
            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(package.Amount, Is.EqualTo(0u));
        }

        [Test]
        public void CharacterInventory_TwoPlainConsumables_MergeIntoOneStack()
        {
            var inventory = new CharacterInventory(new Vector2Int(4, 4));

            var first = new Package(inventory, Arrows(), 4u);
            _ = inventory.TryAddToContainer(ref first);
            var second = new Package(inventory, Arrows(), 3u);
            _ = inventory.TryAddToContainer(ref second);

            Assert.That(inventory.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(inventory.StoredPackages.Values.Single().Amount, Is.EqualTo(7u));
        }

        [Test]
        public void Container_RoundTripsThroughThePersistableShape()
        {
            // The persistence constraint the foundational-rework spec bakes in now: a
            // container's state must be expressible as [{ x, y, definitionId + instance DTO,
            // amount }], with Package.Sender never serialized. No save file is written - this
            // asserts the shape is sufficient.
            var source = new CharacterInventory(new Vector2Int(4, 4));

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
            var restored = new CharacterInventory(new Vector2Int(4, 4));
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

        private static CharacterEquipment Equipment(IStatReceiver stats = null, ICursorSink cursor = null) =>
            new(new Vector2Int(14, 1), stats, cursor);

        [Test]
        public void CharacterEquipment_WithNoInjectedDeps_StillEquipsAndUnequips()
        {
            var equipment = Equipment();
            var sender = new CharacterInventory(new Vector2Int(4, 4));

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

            var package = new Package(new CharacterInventory(new Vector2Int(4, 4)), Helm(4f), 1u);
            _ = equipment.TryAddToContainer(ref package);

            Assert.That(stats.Added.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }));
            Assert.That(stats.Removed, Is.Empty);
        }

        [Test]
        public void CharacterEquipment_RemoveAtPosition_LiftsTheItemsAffixesBackOff()
        {
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats);

            var package = new Package(new CharacterInventory(new Vector2Int(4, 4)), Helm(4f), 1u);
            _ = equipment.TryAddToContainer(ref package);
            var stored = equipment.StoredPackages.Single();

            _ = equipment.RemoveAtPosition(stored.Key, stored.Value);

            Assert.That(stats.Removed.Select(a => a.Stat), Is.EquivalentTo(new[] { StatName.Armor }));
        }

        [Test]
        public void CharacterEquipment_EquippingIntoAnOccupiedSlot_SwapsAndReturnsTheOldItemToTheSender()
        {
            var stats = new FakeStatReceiver();
            var equipment = Equipment(stats, new FakeCursorSink());
            var sender = new CharacterInventory(new Vector2Int(4, 4));

            var first = new Package(sender, Helm(2f), 1u);
            _ = equipment.TryAddToContainer(ref first);

            var second = new Package(sender, Helm(9f), 1u);
            _ = equipment.TryAddToContainer(ref second);

            // The new helm is worn; the old one is back in the sender.
            Assert.That(equipment.StoredPackages, Has.Count.EqualTo(1));
            Assert.That(equipment.StoredPackages.Values.Single().Item.Affixes[0].Modifier.Value, Is.EqualTo(9f));
            Assert.That(sender.StoredPackages.Values.Select(p => p.Item.DefinitionId), Has.Some.EqualTo(HelmId));

            // Stats: both helms applied on equip, the displaced one lifted.
            Assert.That(stats.Added.Count, Is.EqualTo(2));
            Assert.That(stats.Removed.Count, Is.EqualTo(1));
        }

        [Test]
        public void CharacterEquipment_WhenTheSenderCannotReHomeTheDisplacedItem_HandsItToTheCursorSink()
        {
            var cursor = new FakeCursorSink();
            var equipment = Equipment(new FakeStatReceiver(), cursor);
            var fullSender = new CharacterInventory(new Vector2Int(1, 1));

            // Fill the sender's only cell so the displaced ring has nowhere to land.
            var filler = new Package(fullSender, Arrows(), 1u);
            _ = fullSender.TryAddToContainer(ref filler);

            var a = new Package(fullSender, Ring(1f), 1u);
            _ = equipment.TryAddToContainer(ref a);
            var b = new Package(fullSender, Ring(2f), 1u);
            _ = equipment.TryAddToContainer(ref b);
            var c = new Package(fullSender, Ring(3f), 1u);
            _ = equipment.TryAddToContainer(ref c);

            // Two ring slots, three rings equipped in turn - the third swaps one out, and the
            // full sender cannot take it, so it goes to the cursor.
            Assert.That(equipment.StoredPackages, Has.Count.EqualTo(2));
            Assert.That(cursor.Replaced, Has.Count.EqualTo(1));
            Assert.That(cursor.Replaced.Single().Item.DefinitionId, Is.EqualTo(RingId));
        }
    }
}
