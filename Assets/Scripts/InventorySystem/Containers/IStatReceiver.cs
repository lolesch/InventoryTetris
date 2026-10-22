using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The character an equipped item's affixes apply to and lift off of. The container
    /// core takes this at construction, so the assembly names no provider or locator.
    /// Implemented by <c>Hero</c>, whose change events the stat displays bind to.
    /// </summary>
    public interface IStatReceiver
    {
        void AddItemStats(IReadOnlyList<CharacterStatModifier> stats);
        void RemoveItemStats(IReadOnlyList<CharacterStatModifier> stats);
    }
}
