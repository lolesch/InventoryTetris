using System;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What a Session carries around its Hero and never saves (ADR-0015): each Town Stop's Supply,
    /// the Sold container and the Inventory Context. The Run and its ground Drops join it with the
    /// simulation service (#113). A plain object over containers it is handed, so a test builds
    /// one with no scene; <see cref="SessionBuilder"/> is where one is built for the game, after
    /// the Hero. A hero load replaces it together with the Hero.
    /// </summary>
    public sealed class World
    {
        public World(CharacterInventory vendorSupply, CharacterInventory healerSupply, SoldContainer sold,
            InventoryContextState context)
        {
            VendorSupply = vendorSupply ?? throw new ArgumentNullException(nameof(vendorSupply));
            HealerSupply = healerSupply ?? throw new ArgumentNullException(nameof(healerSupply));
            Sold = sold ?? throw new ArgumentNullException(nameof(sold));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>The Vendor's Supply shelf.</summary>
        public CharacterInventory VendorSupply { get; }

        /// <summary>The Healer's Supply shelf, sized like the Vendor's: one size for every Supply.</summary>
        public CharacterInventory HealerSupply { get; }

        /// <summary>What the player sold, bought back like a Supply. Shown on each selling panel's Sold tab.</summary>
        public SoldContainer Sold { get; }

        /// <summary>Which panels are up and where a Quick Move lands: the engine-free rule, held here
        /// so every panel and toggle reaches the one the current World owns.</summary>
        public InventoryContextState Context { get; }
    }
}
