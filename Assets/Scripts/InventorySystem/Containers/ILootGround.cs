using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The World's ground as the player's hand needs it: somewhere to lay a Package, and to take one back
    /// from. The Run's loot flow is the implementation during a Run (it counts what banks toward the
    /// Run's take), the ground desk in Town; both work on the World's one <see cref="GroundContainer"/>.
    /// It lives with the containers so a <see cref="Sale"/> can lay its overflow on it.
    /// </summary>
    public interface ILootGround
    {
        /// <summary>
        /// Lays <paramref name="package"/> on the ground, evicting the oldest when it is full.
        /// False, with nothing changed, for a Package larger than the whole ground.
        /// </summary>
        bool PlaceOnGround(Package package);

        /// <summary>
        /// The player takes the Package of <paramref name="item"/> off the ground: coins bank to the Wallet,
        /// anything else goes through the acquisition entry point. What finds no room stays lying there.
        /// </summary>
        /// <returns>Whether the player took it. False also for an item no longer on the ground.</returns>
        bool PickUpFromGround(ItemInstance item);
    }
}
