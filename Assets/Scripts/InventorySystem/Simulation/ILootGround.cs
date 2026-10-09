using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The Run's ground as a discard needs it: somewhere to lay a Package. <see cref="LootFlow"/> is the
    /// real implementation (its one <see cref="GroundContainer"/>, wiped on Run end); a Run with no loot
    /// flow hands <see cref="DropTransaction"/> a <c>null</c> ground and the item stays where it was.
    /// </summary>
    public interface ILootGround
    {
        /// <summary>
        /// Lays <paramref name="package"/> on the ground, evicting the oldest when it is full.
        /// False, with nothing changed, for a Package larger than the whole ground.
        /// </summary>
        bool PlaceOnGround(Package package);
    }
}
