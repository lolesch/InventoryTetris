using System.Collections.Generic;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// A destination that decides for itself how an item is placed - auto-equip, else the
    /// bag - rather than a plain container add. <c>false</c> means nothing had room and
    /// nothing moved; there is no overflow destination behind it, so a caller can leave the
    /// item where it was (a Buy or Stash retrieval rolls back, a Drop stays on the ground).
    /// Lives in Containers so it can name the containers it writes to; <c>Package</c> is
    /// Containers-resident too, so <see cref="PickUpItem"/> takes the item and amount apart.
    /// Implemented by <c>Hero</c>.
    /// </summary>
    public interface IItemReceiver
    {
        /// <summary>
        /// Every container <see cref="PickUpItem"/> may write to. <see cref="PickUpTransaction.Run"/>
        /// enrols them, so a pick-up that fails part-way rolls back in all of them.
        /// </summary>
        IReadOnlyList<AbstractDimensionalContainer> ReceivingContainers { get; }

        bool PickUpItem(ItemInstance item, uint amount);
    }
}
