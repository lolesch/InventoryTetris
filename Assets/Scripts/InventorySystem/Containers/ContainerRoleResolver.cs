namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Which container a <see cref="ContainerRole"/> names: the pure half of how a display binds
    /// itself (<c>InventoryProvider.TryRegisterDisplay</c>). Held apart from the provider so the
    /// mapping is tested at the container seam - a missing arm leaves a display unbound with no
    /// error, because an unresolved role just returns no container.
    /// </summary>
    public static class ContainerRoleResolver
    {
        public static AbstractDimensionalContainer Resolve(ContainerRole role,
            AbstractDimensionalContainer equipment, AbstractDimensionalContainer inventory,
            AbstractDimensionalContainer stash, AbstractDimensionalContainer store,
            AbstractDimensionalContainer healerSupply, AbstractDimensionalContainer sold) => role switch
        {
            ContainerRole.Equipment => equipment,
            ContainerRole.Inventory => inventory,
            ContainerRole.Stash => stash,
            ContainerRole.VendorSupply => store,
            ContainerRole.HealerSupply => healerSupply,
            ContainerRole.Sold => sold,
            _ => null,
        };
    }
}
