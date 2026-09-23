namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Which player container a scene- or runtime-instanced
    /// <see cref="ToolSmiths.InventorySystem.GUI.InventoryDisplays.AbstractContainerDisplay"/>
    /// should bind to. Lets a display resolve its own container by asking
    /// <see cref="InventoryProvider"/> for it (<see cref="InventoryProvider.TryRegisterDisplay"/>),
    /// rather than the provider holding a hard scene reference to every display it owns - the
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
        Store,
        Basket,
    }
}
