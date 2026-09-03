using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The character an equipped item's affixes apply to and lift off of. The container
    /// core used to reach the player through <c>CharacterProvider.Instance.Player</c>
    /// directly; <see cref="CharacterEquipment"/> now takes this at construction so the
    /// assembly names no provider. Implemented by <c>LocalPlayer</c>.
    /// </summary>
    public interface IStatReceiver
    {
        void AddItemStats(IReadOnlyList<CharacterStatModifier> stats);
        void RemoveItemStats(IReadOnlyList<CharacterStatModifier> stats);
    }
}
