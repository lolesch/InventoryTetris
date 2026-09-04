using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// A settable stand-in for <see cref="ItemDefinition"/> - the contract is an interface
    /// precisely so a test needs no <c>ScriptableObject</c>. Every property has a benign
    /// default; a test sets only the ones it cares about.
    /// </summary>
    public sealed class FakeItemDefinition : ItemDefinition
    {
        public string Id { get; set; } = "fake.item";
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
}
