using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The drag cursor a displaced package is handed to when an equipment swap cannot
    /// re-home it in a container. Was <c>DragProvider.Instance.ReplacePackage(...)</c>
    /// reached from <see cref="CharacterEquipment.AddAtPosition"/>; injected now so the
    /// container assembly names no provider. Implemented by <c>DragProvider</c>.
    /// </summary>
    public interface ICursorSink
    {
        /// <param name="package">The item now on the cursor.</param>
        /// <param name="origin">The container <paramref name="package"/> was displaced
        /// from - the swap partner's real home, not necessarily the container the drag
        /// itself started at.</param>
        /// <param name="originPosition">The cell in <paramref name="origin"/>
        /// <paramref name="package"/> was displaced from.</param>
        void ReplacePackage(Package package, AbstractDimensionalContainer origin, Vector2Int originPosition);
    }
}
