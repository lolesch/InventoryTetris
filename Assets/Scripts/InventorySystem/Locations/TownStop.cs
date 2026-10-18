using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Locations
{
    /// <summary>
    /// A Town Stop authored as locked (ADR-0023): the Vendor. The Stash and the Healer stay plain and are not
    /// Unlockables. A Town Stop is only ever opened, never finished, so it names nothing it Unlocks when Complete.
    /// </summary>
    [CreateAssetMenu(fileName = "New Town Stop", menuName = AssetMenus.Root + "Town Stop")]
    public sealed class TownStop : Unlockable
    {
    }
}
