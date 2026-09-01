using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// The authored, weighted outcome sets a <see cref="RollContext"/> carries - "the loot
    /// table in play" (<c>CONTEXT.md</c> "Roll Context"). It says <em>which category</em>
    /// drops and <em>at which rarity</em>; the generator then draws a concrete definition
    /// from the catalog and rolls its affixes.
    ///
    /// Both vectors are probability vectors in enum-declaration order summing to 1 - the
    /// shape <c>ProbabilityTable&lt;T&gt;.Probabilities</c> already produces, so the runtime
    /// adapter (issue #7) is a pass-through over the existing distribution
    /// <c>ScriptableObject</c>s and a test builds one from two float arrays.
    ///
    /// Per-source loot tables (one table per <c>Location</c>) are the seam this unblocks,
    /// not part of this contract yet - today the whole game shares one table.
    /// </summary>
    public interface LootTable
    {
        /// <summary>
        /// Odds per <see cref="ItemCategory"/>, in enum order
        /// (<c>NONE, Consumable, Equipment, Currency</c>), summing to 1. <c>NONE</c> is the
        /// fail bucket - a table that leaves it the whole mass can roll nothing.
        /// </summary>
        IReadOnlyList<float> CategoryOdds { get; }

        /// <summary>
        /// Odds per <see cref="ItemRarity"/>, in enum order
        /// (<c>NoDrop, Common, Magic, Rare, Unique</c>), summing to 1. Magic find cascades
        /// over this vector (<see cref="RarityCascade"/>); <c>NoDrop</c> is held out of the
        /// cascade so magic find never changes how often a drop happens.
        /// </summary>
        IReadOnlyList<float> RarityOdds { get; }
    }
}
