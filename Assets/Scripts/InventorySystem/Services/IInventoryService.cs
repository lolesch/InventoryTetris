using Submodules.Utility.Services;
using System;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The state service over the Hero's containers and the World's Supplies (ADR-0015): which
    /// container a role names, where a Quick Move lands, acquisition (equip, else bag, debug Stash
    /// overflow) and each Supply's Restock. It reads the current Hero and World off the
    /// <see cref="ISession"/> on every call and caches neither, so a hero load changes what it
    /// operates on without it noticing.
    /// </summary>
    public interface IInventoryService : IService
    {
        /// <summary>
        /// The Inventory Context's change event, stable across a hero load: it follows whichever World
        /// is current, so a panel or toggle that subscribes once is still subscribed after a swap. It
        /// fires for every change of the current World's context, and once more on a load with the new
        /// World's starting context (<see cref="InventoryContext.None"/>), so a panel that was up for
        /// the discarded World comes down. The discarded World's context reaches no one.
        /// </summary>
        event Action<InventoryContext> ContextChanged;

        /// <summary>The current World's active Inventory Context.</summary>
        InventoryContext ActiveContext { get; }

        /// <summary>The container <paramref name="role"/> names on the current Hero and World, or null for none.</summary>
        AbstractDimensionalContainer ContainerFor(ContainerRole role);

        /// <summary>Where a shift-click on <paramref name="source"/> sends its item in the active Inventory Context.</summary>
        QuickMoveIntent QuickMoveFor(AbstractDimensionalContainer source);

        /// <summary>
        /// The debug spawners' entry: the Hero's placement, plus - in a debug build - an overflow to
        /// the Stash so a spawn burst is not lost to a full bag. Anything that must treat "no room"
        /// as a fact (loot, Buy, Stash retrieval, Corpse recovery) goes through the Hero's
        /// <c>IItemReceiver</c> instead.
        /// </summary>
        bool PickUpOrStash(Package package);

        /// <summary>The Hero's magic find, handed to every roll. A hero whose template never authored the stat has none: 0, not an error.</summary>
        float MagicFind { get; }

        /// <summary>The Hero's item quantity bonus, handed to every loot roll. 0 for a hero that never authored the stat.</summary>
        float ItemQuantity { get; }

        /// <summary>The Vendor's Restock: refills its shelf, and clears the Sold container (#128).</summary>
        void RestockVendorSupply();

        /// <summary>The Healer's Restock: refills its shelf, and clears the Sold container (#128).</summary>
        void RestockHealerSupply();

        /// <summary>Both Town Stops' Restock, run when a Run is Recalled: both shelves, and the Sold container once.</summary>
        void RestockTownStops();
    }
}
