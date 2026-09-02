using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    // Shared stand-ins for the container tests in this asmdef. The pure
    // InventorySystem.Items.Tests fakes live in a separate test assembly, and the item
    // contract is an interface precisely so a stand-in here is a few lines.

    /// <summary>A mutable <see cref="ItemDefinition"/> for tests - build one per item kind.</summary>
    internal sealed class TestDefinition : ItemDefinition
    {
        public string Id { get; set; } = "test.item";
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

    /// <summary>An in-memory <see cref="IItemCatalog"/> - <c>ItemView.Catalog</c> for a test.</summary>
    internal sealed class TestCatalog : IItemCatalog
    {
        private readonly Dictionary<string, ItemDefinition> byId = new();

        public TestCatalog With(ItemDefinition definition)
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

    /// <summary>Records what <see cref="CharacterEquipment"/> applies to / lifts off the character.</summary>
    internal sealed class FakeStatReceiver : IStatReceiver
    {
        public readonly List<CharacterStatModifier> Added = new();
        public readonly List<CharacterStatModifier> Removed = new();
        public void AddItemStats(IReadOnlyList<CharacterStatModifier> stats) => Added.AddRange(stats);
        public void RemoveItemStats(IReadOnlyList<CharacterStatModifier> stats) => Removed.AddRange(stats);
    }

    /// <summary>Records the packages a move handed to the drag cursor.</summary>
    internal sealed class FakeCursorSink : ICursorSink
    {
        public readonly List<Package> Replaced = new();
        public void ReplacePackage(Package package) => Replaced.Add(package);
    }
}
