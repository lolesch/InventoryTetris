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
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocationToggle : AbstractToggle
    {
        [SerializeField] private LocationConfig location;

        /// <summary>The authored field destination this toggle represents.</summary>
        public LocationConfig Location => location;
    }
}
