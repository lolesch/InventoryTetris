using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Locations;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// An <see cref="AbstractToggle"/> that carries a <see cref="LocationConfig"/> reference —
    /// one per location in the map panel's <see cref="RadioGroup"/>. When the player selects
    /// this toggle, the <see cref="MapPanel"/> reads <see cref="Location"/> to know which
    /// Location to Send to (issue #27).
    ///
    /// The toggle itself has no side effect: selection *is* the whole behaviour. Observers
    /// watch the owning <see cref="RadioGroup.OnGroupChanged"/> and read
    /// <see cref="RadioGroup.ActivatedToggle"/> rather than listening to each toggle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocationToggle : PanelToggle
    {
        [SerializeField] private LocationConfig location;

        /// <summary>The authored field destination this toggle represents.</summary>
        public LocationConfig Location => location;

        protected override void OnToggle()
        {
            base.OnToggle();
            
        }
    }
}
