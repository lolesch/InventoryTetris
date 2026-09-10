using System.Collections.Generic;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The hero's bag as <see cref="RunSettlement"/> needs to see it (ADR-0009): read and empty
    /// its non-currency contents on a Death, and offer a single item back on a recovery. The
    /// engine binds this to the live <c>CharacterInventory</c>; a test binds an in-memory list.
    /// Coins and equipped gear are never this port's concern - a Death fee is the Wallet's
    /// (<see cref="ISettlementLedger"/>), equipped gear is never touched.
    /// </summary>
    public interface ISettlementBag
    {
        /// <summary>
        /// Collect the bag's non-currency contents and remove them - a stack of <c>N</c> yields
        /// <c>N</c> instances, so a later recovery re-stacks to the same count. Currency stays put.
        /// </summary>
        IReadOnlyList<ItemInstance> TakeNonCurrencyContents();

        /// <summary>
        /// Try to fit one recovered <paramref name="item"/> back into the bag. <c>false</c> when
        /// it does not fit - the caller then strands it on the ground or re-buries it.
        /// </summary>
        bool TryStore(ItemInstance item);
    }
}
