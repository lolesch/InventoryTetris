using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components
{
    public class VisibleCursorRequester : MonoBehaviour
    {
        [SerializeField] protected bool hideCursorOnDisable = false;

        private void OnDisable() => Cursor.visible = hideCursorOnDisable;
        private void OnEnable() => Cursor.visible = true;
    }
}
