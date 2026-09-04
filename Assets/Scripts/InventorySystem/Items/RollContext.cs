using System;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// Everything a <see cref="ItemGenerator.Roll"/> depends on beyond the definition
    /// (<c>CONTEXT.md</c> "Roll Context"): the source level the item rolls at, the magic
    /// find biasing its rarity, and the loot table in play. Passing this as a parameter -
    /// rather than reaching a singleton, as <c>new EquipmentItem(...)</c> did - is the whole
    /// point of the seam: per-source scaling and drop tables now have somewhere to plug in.
    /// </summary>
    public readonly struct RollContext
    {
        /// <summary>The authored, weighted outcome sets this roll draws from.</summary>
        public LootTable Table { get; }

        /// <summary>
        /// The level of the roll's source (a <c>Location</c>, an <c>Encounter</c>). Becomes
        /// <see cref="ItemInstance.ItemLevel"/>. Clamped to non-negative.
        /// </summary>
        public int SourceLevel { get; }

        /// <summary>
        /// The roller's <c>IncreasedItemRarity</c>, as a percentage. Biases the rarity roll
        /// toward rarer tiers without changing how often a drop happens. Clamped to
        /// non-negative; 0 reproduces the authored rarity table exactly.
        /// </summary>
        public float MagicFind { get; }

        public RollContext(LootTable table, int sourceLevel = 0, float magicFind = 0f)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            SourceLevel = sourceLevel < 0 ? 0 : sourceLevel;
            MagicFind = magicFind < 0f ? 0f : magicFind;
        }
    }
}
