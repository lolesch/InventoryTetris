namespace ToolSmiths.InventorySystem.Locations
{
    /// <summary>
    /// How far a hero has got with an <see cref="Unlockable"/>. Ordered, so a rule that asks for "Active"
    /// is a <c>&gt;= Active</c> comparison and a Complete source never re-locks what it opened.
    /// </summary>
    public enum UnlockState
    {
        /// <summary>Not shown: nothing that names it is Active yet.</summary>
        Hidden,

        /// <summary>Shown greyed out: something that names it is Active, but no link to it is satisfied.</summary>
        Locked,

        /// <summary>Open.</summary>
        Active,

        /// <summary>Done, and forever so.</summary>
        Complete,
    }
}
