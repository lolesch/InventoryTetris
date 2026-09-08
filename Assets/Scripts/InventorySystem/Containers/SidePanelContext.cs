namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Which town side panel the player currently has open (issue #54) - the Side Panel
    /// Context of the CONTEXT.md glossary. <see cref="None"/> is a real state, not a
    /// missing one: the side panels switch themselves off, so "no panel open" is distinct
    /// from a panel being up. Owned by the <c>InventoryProvider</c> - via
    /// <see cref="SidePanelState"/> - not by the UI toggles, which only announce
    /// transitions to it (#57). The trade flow reads it to route shift-clicks.
    /// </summary>
    public enum SidePanelContext
    {
        None,
        Stash,
        Vendor
    }
}
