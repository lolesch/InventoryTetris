namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Which panels are up and where a Quick Move lands - one answer to both questions
    /// (issue #83, the Inventory Context of <c>CONTEXT.md</c>). Single-valued: exactly one
    /// is active, so a Stash-and-Vendor-at-once state does not exist to be a bug.
    /// <see cref="None"/> is a real state, not a missing one.
    ///
    /// <para>Stands beside <see cref="SidePanelContext"/> for now (#83 is the expand step of
    /// an expand / migrate / contract sequence) - nothing reads this yet.
    /// <see cref="SidePanelContext"/> is retired once every path is migrated (#84, #85).</para>
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
