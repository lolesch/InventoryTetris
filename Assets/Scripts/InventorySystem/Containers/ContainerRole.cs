namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Which player container a scene- or runtime-instanced
    /// <see cref="ToolSmiths.InventorySystem.GUI.InventoryDisplays.AbstractContainerDisplay"/>
    /// should bind to. Lets a display resolve its own container by asking the inventory service
    /// for it (<c>IInventoryService.ContainerFor</c>),
    /// rather than the service holding a hard scene reference to every display it owns - the
    /// direction that broke once the Vendor's slot grids started spawning at runtime instead of
    /// being hand-placed.
    /// </summary>
    public enum ContainerRole
    {
        /// <summary>The zero value, reserved so a never-configured serialized field reads as
        /// unwired rather than silently aliasing <see cref="Equipment"/> - the "wiring took"
        /// check in <c>AbstractContainerDisplay.OnValidate</c> depends on this being the default.</summary>
        Unassigned,
        Equipment,
        Inventory,
        Stash,
        /// <summary>The Vendor's Supply shelf.</summary>
        VendorSupply,
        /// <summary>The Healer's Supply shelf (issue #121): consumables, bought like the Vendor Supply's.
        /// The members are serialized by value in scenes and prefabs, so inserting or deleting a
        /// member shifts every one after it: re-serialize every display in the same change.</summary>
        HealerSupply,
        /// <summary>The Sold container (issues #124, #127): what the player sold, shown on each
        /// selling panel's Sold tab. Both panels' Sold grids bind the one container.</summary>
        Sold,
    }
}
