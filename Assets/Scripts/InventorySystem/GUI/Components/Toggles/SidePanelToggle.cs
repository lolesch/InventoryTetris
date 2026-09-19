using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>
    /// A Town Stop's panel toggle. Fades its panel like any <see cref="PanelToggle"/> — it
    /// carries a hotkey and nothing else; the panel it fades
    /// (<see cref="ToolSmiths.InventorySystem.GUI.Components.Panels.SidePanel"/>) is what
    /// announces the <see cref="ToolSmiths.InventorySystem.Inventories.SidePanelContext"/> the
    /// toggle used to announce itself (#57), since #75 moved that job to the moment the panel
    /// actually appears or disappears rather than the moment its toggle is clicked.
    ///
    /// <para><b>No second group.</b> Mutual exclusion comes from the minimap's <c>TownGroup</c>,
    /// which Stash, Vendor and Healer belong to (Go Venture does not — it is a plain button, not
    /// a panel with state to protect). This class must not introduce a <see cref="RadioGroup"/>
    /// of its own, or "which panel is open" would have two answers.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SidePanelToggle : PanelToggle
    {
        [Tooltip("Optional hotkey. Inert whenever the toggle is non-interactable - which the " +
                 "minimap already arranges for the Field face and for InField.")]
        [SerializeField] private KeyCode hotkey = KeyCode.None;

        /// <summary>
        /// The hotkey is the same act as a click, guard for guard - including the
        /// <see cref="RadioGroup.IsClearable"/> rule, so a hotkey cannot switch off a
        /// toggle a click could not. <c>interactable</c> is the phase gate: the minimap turns
        /// the Town toggles off whenever the Field face is up, which covers both InField and
        /// the Go Venture preview, so no <c>RunPhase</c> dependency is needed here.
        /// </summary>
        private void Update()
        {
            if (hotkey == KeyCode.None || !interactable)
                return;

            if (!Input.GetKeyDown(hotkey))
                return;

            if (RadioGroup && !RadioGroup.IsClearable && IsOn)
                return;

            SetToggle(!IsOn);
        }
    }
}
