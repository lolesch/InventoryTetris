namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The layers a Cancel press passes through, top first: the first rank with a handler that consumes
    /// the press gets it and nothing below hears it. A layer registers under its rank and is not told
    /// about the others, so adding one is a new member here, never an edit to a neighbour.
    /// </summary>
    public enum CancelRank
    {
        /// <summary>A held Package returns to its origin.</summary>
        Drag = 0,

        /// <summary>The open menu screen closes to the one beneath it: Settings back to its menu, a confirmation dismissed, name entry back to the hero slots.</summary>
        Modal = 1,

        /// <summary>Any Inventory Context other than <c>None</c> returns to <c>None</c>.</summary>
        Context = 2,

        /// <summary>With nothing above, Cancel opens the Pause Menu; with it open, Cancel closes it.</summary>
        PauseMenu = 3,
    }
}
