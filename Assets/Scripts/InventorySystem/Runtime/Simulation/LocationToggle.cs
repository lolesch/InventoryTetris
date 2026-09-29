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
        
        protected override void OnEnable()
        {
            base.OnEnable();
            
            if (!Application.isPlaying) 
                return;
            
            var provider = SimulationProvider.Instance;
            if (provider == null) 
                return;

            provider.Run.PhaseChanged -= SyncToPhase;
            provider.Run.PhaseChanged += SyncToPhase;
            
            SyncToPhase(provider.Run.Phase);
        }
        
        protected override void OnDisable()
        {
            base.OnDisable();
            if (!Application.isPlaying) 
                return;
            
            var provider = SimulationProvider.Instance;
            if (provider != null)
                provider.Run.PhaseChanged -= SyncToPhase;
        }

        private void SyncToPhase(RunPhase phase)
        {
            if (phase != RunPhase.InField)
                RadioGroup.ClearActive();
        }
        
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
