namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Which panels are up and where a Quick Move lands - one answer to both questions
    /// (the Inventory Context of <c>CONTEXT.md</c>). Single-valued: exactly one is active, so a
    /// Stash-and-Vendor-at-once state does not exist to be a bug. <see cref="None"/> is a real
    /// state, not a missing one.
    ///
    /// <para>Entry points request it; every panel derives its own visibility from it through
    /// <see cref="InventoryContextState.PanelsFor"/>. Nothing announces a context from a panel's
    /// own appear or disappear hook - the Hero Panel belongs to every non-<c>None</c> context,
    /// so no panel can be the thing that names one (issue #85).</para>
    ///
    /// <para><see cref="InField"/> is deliberately not a member: the Hero Panel is openable
    /// during a Run, so phase reachability constrains which contexts are available
    /// (<see cref="InventoryContextState.SyncToPhase"/>) rather than being one itself.</para>
    /// </summary>
    public enum InventoryContext
    {
        None,
        Hero,
        Stash,
        Vendor,
        Healer,
    }
}
