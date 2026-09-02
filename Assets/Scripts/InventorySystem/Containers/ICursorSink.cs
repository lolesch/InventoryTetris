using ToolSmiths.InventorySystem.Data;

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
        void ReplacePackage(Package package);
    }
}
