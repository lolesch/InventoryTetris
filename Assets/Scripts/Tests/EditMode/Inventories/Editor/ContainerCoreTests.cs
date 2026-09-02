using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Inventories
{
    /// <summary>
    /// The container core (<see cref="AbstractDimensionalContainer"/> and subclasses,
    /// <see cref="Package"/>) still lives in the predefined <c>Assembly-CSharp</c>, which an
    /// asmdef test assembly cannot reference. This file sits in a plain <c>Editor/</c> folder
    /// with no asmdef, so it compiles into the predefined <c>Assembly-CSharp-Editor</c> -
    /// which auto-references <c>Assembly-CSharp</c> and is scanned by the EditMode Test
    /// Runner. It is the Phase 0 test seam (issue #4); when <c>InventorySystem.Containers</c>
    /// is extracted (issue #15) these move into an <c>InventorySystem.Containers.Tests</c>
    /// asmdef and the catalog is injected rather than read off <see cref="ItemView.Catalog"/>.
    /// </summary>
    [TestFixture]
    public sealed class ContainerCoreTests
    {
        // A file-local ItemDefinition / IItemCatalog - the pure InventorySystem.Items.Tests
        // fakes are in a test asmdef this predefined assembly cannot reference, and the
        // contract is an interface precisely so a stand-in is a few lines.
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

        private const string SwordId = "test.sword";
        private const string ArrowId = "test.arrow";

        [SetUp]
        public void SetCatalog() => ItemView.Catalog = new Catalog()
            .With(new Definition { Id = SwordId, Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword, Footprint = ItemSize.OneByOne, BaseStackLimit = 1u })
            .With(new Definition { Id = ArrowId, Category = ItemCategory.Consumable, ConsumableType = ConsumableType.Arrow, Footprint = ItemSize.OneByOne, BaseStackLimit = 10u });

        [TearDown]
        public void ClearCatalog() => ItemView.Catalog = null;

        private static ItemInstance Sword() => new(SwordId, ItemRarity.Rare, 7, new[]
        {
            new CharacterStatModifier(StatName.PhysicalDamage, new StatModifier(new Vector2Int(3, 9), 6f, StatModifierType.FlatAdd)),
        });

        private static ItemInstance Arrows() => new(ArrowId, ItemRarity.Common, 1, null);

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
    }
}
