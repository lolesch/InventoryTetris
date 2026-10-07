using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI
{
    /// <summary>
    /// The held-key states the HUD reads. Compared as held states rather than as KeyDown/KeyUp: either
    /// key counts, and an Alt+Tab never sends the KeyUp.
    /// </summary>
    public static class ModifierKeys
    {
        /// <summary>Either Alt, or AltGr - which a keyboard reports as the right Alt.</summary>
        public static bool Alt =>
            Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
    }
}
