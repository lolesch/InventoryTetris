using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>
    /// A menu toggle that declares which panel it opens (issue #30). The Menu Context
    /// reads the kind off the <em>authored data</em> on the toggle, so it never hard-codes
    /// which toggle is the Stash and which the Store. A second kind on the same toggle is
    /// a wiring error, not a supported mode - the first one wins.
    /// </summary>
    public sealed class MenuPanelToggle : PanelToggle
    {
        [SerializeField] private MenuContextKind menuContextKind;

        public MenuContextKind MenuContextKind => menuContextKind;
    }
}