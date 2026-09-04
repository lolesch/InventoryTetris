using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// Shared builders for the item-contract fixtures. Prior art: <c>Probability/TestOutcomes.cs</c>.
    /// </summary>
    internal static class Sample
    {
        /// <summary>
        /// A <see cref="CharacterStatModifier"/> with the value clamped into
        /// <c>[min, max]</c> by <see cref="StatModifier"/> - keep call-site values inside the
        /// range if a round trip must come back unchanged.
        /// </summary>
        public static CharacterStatModifier Affix(
            StatName stat = StatName.Health, int min = 1, int max = 100, float value = 50f,
            StatModifierType type = StatModifierType.FlatAdd) =>
            new(stat, new StatModifier(new Vector2Int(min, max), value, type));

        /// <summary>One entry in a definition's affix pool, for the generator's roll tests.</summary>
        public static AffixSlot Slot(
            StatName stat = StatName.Health, int min = 10, int max = 20,
            StatModifierType type = StatModifierType.FlatAdd, float weight = 1f) =>
            new(stat, min, max, type, weight);
    }
}
