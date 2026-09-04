using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// The immutable template for a kind of item - what a Rare Chest <em>can be</em>,
    /// before anything is rolled (see <c>CONTEXT.md</c> "Item Definition"). The generator,
    /// the containers and the displays all read an item through this contract.
    ///
    /// It is an interface, not a class, so a test passes a fake and no
    /// <see cref="UnityEngine.ScriptableObject"/> is instantiated - the rule the
    /// probability rebuild already follows. The authored adapter
    /// (<c>ItemDefinitionAsset : ScriptableObject</c>, issue #7) holds one definition plus
    /// its art; art is deliberately absent here because the roll path never needs a sprite.
    /// </summary>
    public interface ItemDefinition
    {
        /// <summary>
        /// Stable identity that survives a rename or a move. An <em>explicit serialized
        /// string</em> - a slug or a GUID the author picks - never the Unity asset GUID
        /// (which regenerates when a <c>.meta</c> is lost) and never the asset name. A
        /// saved <see cref="ItemInstance"/> references its definition by this id.
        /// </summary>
        string Id { get; }

        /// <summary>Equipment, Consumable or Currency. Replaces the <c>is EquipmentItem</c> type check.</summary>
        ItemCategory Category { get; }

        /// <summary>The grid shape an instance of this definition occupies.</summary>
        ItemSize Footprint { get; }

        /// <summary>How many of this item fit in one grid cell before a second package is needed.</summary>
        uint BaseStackLimit { get; }

        /// <summary>
        /// The allowed stats, their roll ranges and their pool weighting - what a roll may
        /// draw affixes from. Was <c>ItemTypeData</c>.
        /// </summary>
        IReadOnlyList<AffixSlot> AffixPool { get; }

        /// <summary>
        /// Guaranteed modifiers that are not rolled (<c>CONTEXT.md</c> "Implicit stats").
        /// A roll merges these into the instance's combined affix list.
        /// </summary>
        IReadOnlyList<CharacterStatModifier> ImplicitStats { get; }

        /// <summary>What a character must meet to equip or use this item. Fills the empty <c>#region REQUIREMENTS</c>.</summary>
        ItemRequirement Requirement { get; }

        /// <summary>
        /// A Unique is an ordinary definition flagged unique with a fixed affix list, not a
        /// separate kind of thing (<c>CONTEXT.md</c> "Rarity").
        /// </summary>
        bool IsUnique { get; }

        /// <summary>The fixed affixes a roll merges in when <see cref="IsUnique"/>; empty otherwise.</summary>
        IReadOnlyList<CharacterStatModifier> UniqueAffixes { get; }

        /// <summary>The equipment slot this fills, or <see cref="EquipmentType.NONE"/> when <see cref="Category"/> is not Equipment.</summary>
        EquipmentType EquipmentType { get; }

        /// <summary>The consumable kind, or <see cref="ConsumableType.NONE"/> when <see cref="Category"/> is not Consumable.</summary>
        ConsumableType ConsumableType { get; }

        /// <summary>The coin denomination, or <see cref="CurrencyType.NONE"/> when <see cref="Category"/> is not Currency.</summary>
        CurrencyType CurrencyType { get; }
    }
}
