namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Which secondary panel the player currently has open, published by the Menu Context
    /// (issue #30). <see cref="None"/> is a real state - the menu lets the active toggle
    /// switch itself off, so "no panel open" is distinct from a panel being up. The kind
    /// is authored on each menu toggle (a <c>MenuPanelToggle</c>), never hard-coded in the
    /// adapter or the RadioGroup.
    /// </summary>
    public enum MenuContextKind
    {
        None = 0,
        Stash = 1,
        Store = 2,
    }
}