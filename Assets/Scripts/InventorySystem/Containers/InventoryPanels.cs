using System;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The panels an <see cref="InventoryContext"/> derives (issue #83) - a set, deliberately
    /// a different type from the single-valued context it is derived from. The context can
    /// never itself name two Town Stops; this can name the Hero Panel alongside one.
    /// </summary>
    [Flags]
    public enum InventoryPanels
    {
        None = 0,
        Hero = 1 << 0,
        Stash = 1 << 1,
        Vendor = 1 << 2,
        Healer = 1 << 3,
    }
}
