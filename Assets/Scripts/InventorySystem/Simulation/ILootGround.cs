using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The Run's ground as a corpse recovery needs it: somewhere to seat an item that the bag
    /// would not take. <see cref="LootFlow"/> is the real implementation (its one
    /// <see cref="LootFlow.GroundDrops"/> list, cleared on Run end); a Run with no loot flow
    /// hands <see cref="RunSettlement.Recover"/> a <c>null</c> ground and the overflow is
    /// re-buried instead.
    /// </summary>
    public interface ILootGround
    {
        /// <summary>Seat <paramref name="item"/> on the ground - picked back up, or seen stranded.</summary>
        void PlaceOnGround(ItemInstance item);
    }
}
