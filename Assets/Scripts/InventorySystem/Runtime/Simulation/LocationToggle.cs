using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.Serialization;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    [DisallowMultipleComponent]
    public sealed class LocationToggle : AbstractToggle
    {
        [field: FormerlySerializedAs("location"), SerializeField] public LocationConfig Location { get; private set; }

        protected override void OnToggle()
        {
            if (!IsOn) return;
            
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            if (provider.Run.HeroIsDown) return;

            if (provider.Run.Phase == RunPhase.InField)
                provider.Recall();
            provider.Send(Location);
        }
    }
}
