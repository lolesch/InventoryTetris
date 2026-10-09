using System;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What a Session carries around its Hero and never saves (ADR-0015): each Town Stop's Supply,
    /// the Sold container, the Inventory Context, and the Run with its ground Drops, which the
    /// simulation service builds on the first ask (#113). A plain object over containers it is
    /// handed, so a test builds one with no scene; <see cref="SessionBuilder"/> is where one is
    /// built for the game, after the Hero. A hero load replaces it together with the Hero.
    /// </summary>
    public sealed class World
    {
        public World(CharacterInventory vendorSupply, CharacterInventory healerSupply, SoldContainer sold,
            GroundContainer ground, InventoryContextState context)
        {
            VendorSupply = vendorSupply ?? throw new ArgumentNullException(nameof(vendorSupply));
            HealerSupply = healerSupply ?? throw new ArgumentNullException(nameof(healerSupply));
            Sold = sold ?? throw new ArgumentNullException(nameof(sold));
            Ground = ground ?? throw new ArgumentNullException(nameof(ground));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>The Vendor's Supply shelf.</summary>
        public CharacterInventory VendorSupply { get; }

        /// <summary>The Healer's Supply shelf, sized like the Vendor's: one size for every Supply.</summary>
        public CharacterInventory HealerSupply { get; }

        /// <summary>What the player sold, bought back like a Supply. Shown on each selling panel's Sold tab.</summary>
        public SoldContainer Sold { get; }

        /// <summary>Where the Run's Drops lie. One for the World, so a display can bind it; the live loot flow
        /// lays Drops on it and wipes it when the Run ends.</summary>
        public GroundContainer Ground { get; }

        /// <summary>
        /// The Run FSM of this World. <c>null</c> on a fresh World: the simulation service builds it
        /// when something first asks for it, and a World that is replaced takes its Run with it.
        /// Reach it through <see cref="ISimulationService.Run"/>, which never returns null.
        /// </summary>
        public RunState Run { get; internal set; }

        /// <summary>
        /// The live Encounter's loot flow, with the Drops still on the ground: <c>null</c> in Town
        /// and before the first Send. Owned by the simulation service, which retires it when the
        /// Run ends.
        /// </summary>
        public LootFlow LootFlow { get; internal set; }

        /// <summary>Which panels are up and where a Quick Move lands: the engine-free rule, held here
        /// so every panel and toggle reaches the one the current World owns.</summary>
        public InventoryContextState Context { get; }
    }
}
