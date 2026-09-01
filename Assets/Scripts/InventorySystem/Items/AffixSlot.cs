using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// One entry in a definition's <see cref="ItemDefinition.AffixPool"/>: a stat a roll
    /// may draw, the value range it rolls within, the modifier type it applies as, and a
    /// relative weight biasing which stats are picked. Replaces
    /// <c>ItemTypeData.StatRange</c>; the per-value roll <em>curve</em> that type also
    /// carried is a generator concern (issue #6), not part of the contract.
    /// </summary>
    public readonly struct AffixSlot
    {
        /// <summary>The stat this slot can add to an instance.</summary>
        public StatName Stat { get; }

        /// <summary>Inclusive min/max the rolled value falls within, before any rarity scaling.</summary>
        public Vector2Int Range { get; }

        /// <summary>How the rolled modifier applies (flat add, percent, ...).</summary>
        public StatModifierType ModifierType { get; }

        /// <summary>
        /// Relative likelihood this slot is chosen when the generator picks affixes from the
        /// pool. A value of zero or less counts as an equal share (weight 1).
        /// </summary>
        public float Weight { get; }

        public AffixSlot(StatName stat, Vector2Int range, StatModifierType modifierType, float weight = 1f)
        {
            Stat = stat;
            Range = range;
            ModifierType = modifierType;
            Weight = weight;
        }
    }
}
