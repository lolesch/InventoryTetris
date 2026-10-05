using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The two ways an item reaches the Run's ground by the player's hand (issue #63): a Quick Move
    /// (<see cref="Run"/>, lifting the Package at a cell out of a container) and a drag released on
    /// the floor slot (<see cref="Place"/>, a Package already in hand). Either lays it on the
    /// ground (<see cref="ILootGround"/>), where the Ground Items List shows it. The inverse of
    /// <see cref="LootFlow.PickUpFromGround"/>. Lives beside
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

            return Place(stored, ground);
        }

        /// <summary>
        /// Lays <paramref name="package"/>, which is not in any container (the cursor's), on
        /// <paramref name="ground"/>, one entry per unit like <see cref="Run"/>. Nothing is placed
        /// with no ground or an empty Package, and the <c>false</c> tells the caller the item is
        /// still its to keep - the floor slot sends it back rather than deleting it.
        /// </summary>
        /// <returns>Whether anything was placed.</returns>
        public static bool Place(Package package, ILootGround ground)
        {
            if (ground == null || !package.IsValid)
                return false;

            for (var i = 0u; i < package.Amount; i++)
                ground.PlaceOnGround(package.Item);

            return true;
        }
    }
}
