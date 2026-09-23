using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Locations;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// An <see cref="AbstractToggle"/> that carries a <see cref="LocationConfig"/> reference —
    /// one per location in the minimap's <see cref="RadioGroup"/>. When the player selects
    /// this toggle, <see cref="MinimapController"/> reads <see cref="Location"/> to know which
    /// Location to Send to (issue #27).
    ///
    /// Derives <see cref="AbstractToggle"/> directly rather than <see cref="PanelToggle"/>: the
    /// panel this toggle drives (the Field face) is opened by <see cref="MinimapController"/> off
    /// the Run's phase, not by the toggle itself — inheriting <see cref="PanelToggle"/> would
    /// leave its <c>panel</c> field authorable, and a scene that wires it (as this one had, onto
    /// the Combat Panel) gets a second, independent caller fading a panel MinimapController
    /// already owns. The toggle itself has no side effect: selection *is* the whole behaviour.
    /// Observers watch the owning <see cref="RadioGroup.OnGroupChanged"/> and read
    /// <see cref="RadioGroup.ActiveMember"/> rather than listening to each toggle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocationToggle : AbstractToggle
    {
        [SerializeField] private LocationConfig location;

        /// <summary>The authored field destination this toggle represents.</summary>
        public LocationConfig Location => location;

        protected override void OnToggle() { }
    }
}
