using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The two ways an item reaches the Run's ground by the player's hand (issue #63): a Quick Move
    /// (<see cref="Run"/>, lifting the Package at a cell out of a container) and a drag released on
    /// the ground slot (<see cref="Place"/>, a Package already in hand). Either lays it on the
    /// ground (<see cref="ILootGround"/>), where the Ground Items List shows it. The inverse of
    /// <see cref="LootFlow.PickUpFromGround"/>. Lives beside
    /// <see cref="ILootGround"/> rather than with <see cref="PickUpTransaction"/> because
    /// Containers cannot see the ground.
    /// </summary>
    public static class DropTransaction
    {
        /// <summary>
        /// Moves the whole Package at <paramref name="cell"/> of <paramref name="source"/> to
        /// <paramref name="ground"/> as one Package, a stack of <c>n</c> staying one. A Package the
        /// ground refuses stays at <paramref name="cell"/>. Nothing moves with no ground or no item.
        /// </summary>
        /// <returns>Whether anything was dropped.</returns>
        public static bool Run(AbstractDimensionalContainer source, Vector2Int cell, ILootGround ground)
        {
            if (source == null || ground == null
                || !source.TryGetPackageAt(cell, out var stored) || !stored.IsValid)
                return false;

            using var transaction = new ItemTransaction(source);

            _ = source.RemoveAtPosition(cell, stored); // reduces its own copy; `stored` stays whole

            if (!Place(stored, ground))
                return false; // dispose rolls back - the item stays at the cell

            transaction.Commit();
            return true;
        }

        /// <summary>
        /// Lays <paramref name="package"/>, which is not in any container (the cursor's), on
        /// <paramref name="ground"/> as one Package. False with no ground, an empty Package or one
        /// the ground refuses tells the caller the item is still its to keep - the ground slot sends
        /// it back rather than deleting it.
        /// </summary>
        /// <returns>Whether it was placed.</returns>
        public static bool Place(Package package, ILootGround ground) =>
            ground != null && package.IsValid && ground.PlaceOnGround(package);
    }
}
