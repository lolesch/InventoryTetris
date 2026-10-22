using System.Collections.Generic;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>The hero's loot-filter slider: one position per <see cref="HeroBehaviour.RaritySteps"/>
    /// entry (Common..Unique, no NoDrop), so it has four positions and <see cref="EnumSlider{TEnum}.Selected"/>
    /// is the <see cref="ItemRarity"/> to hand to <see cref="HeroBehaviour.LootFilterMinimum"/>.</summary>
    public sealed class RarityFilterSlider : EnumSlider<ItemRarity>
    {
        protected override IReadOnlyList<ItemRarity> Members => HeroBehaviour.RaritySteps;
    }
}
