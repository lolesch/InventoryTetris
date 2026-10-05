using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The Quick Move to the ground (issue #63): lifts the Package at a cell out of a container and
    /// lays it on the Run's ground (<see cref="ILootGround"/>), where the Ground Items List shows
    /// it. The inverse of <see cref="LootFlow.PickUpFromGround"/>. Lives beside
    /// <see cref="ILootGround"/> rather than with <see cref="PickUpTransaction"/> because
    /// Containers cannot see the ground.
    /// </summary>
    public static class DropTransaction
    {
        /// <summary>
        /// Moves the whole Package at <paramref name="cell"/> of <paramref name="source"/> to
        /// <paramref name="ground"/>. The ground is one slot per item, so a stack of <c>n</c> becomes
        /// <c>n</c> entries rather than losing all but one. Nothing moves with no ground or no item.
        /// </summary>
        /// <returns>Whether anything was dropped.</returns>
        public static bool Run(AbstractDimensionalContainer source, Vector2Int cell, ILootGround ground)
        {
            if (source == null || ground == null
                || !source.TryGetPackageAt(cell, out var stored) || !stored.IsValid)
                return false;

            _ = source.RemoveAtPosition(cell, stored);

            for (var i = 0u; i < stored.Amount; i++)
                ground.PlaceOnGround(stored.Item);

            return true;
        }
    }
}
