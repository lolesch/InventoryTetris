using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components
{
    public sealed class VisibleCursorRequester : MonoBehaviour
    {
        [SerializeField] private bool hideCursorOnDisable = false;

        private void OnDisable() => Cursor.visible = hideCursorOnDisable;
        private void OnEnable() => Cursor.visible = true;
    }
}
