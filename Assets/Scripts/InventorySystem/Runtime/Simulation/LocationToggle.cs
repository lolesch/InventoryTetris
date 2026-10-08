using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.Serialization;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// A Location of the InFields face. It carries no phase subscription: its selection is cleared by
    /// its <see cref="AbstractToggle.RadioGroup"/>, which resets itself when the face it sits under
    /// has finished fading out (<see cref="ToggleGroup.ResetWithParentPanel"/>) - a
    /// selection never outlives the panel that showed it. That ties the clear to the face rather
    /// than to the Run phase: on Death or Recall the highlight lasts one fade longer than the phase,
    /// and <see cref="RunPhasePanel"/> is what guarantees the face does go.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocationToggle : AbstractToggle
    {
        [field: FormerlySerializedAs("location"), SerializeField] public LocationConfig Location { get; private set; }

        protected override void OnToggle()
        {
            if (!IsOn) return;
            
            var simulation = SimulationService.Instance;

            if (simulation.Run.HeroIsDown) return;

            // Switching mid-Run moves the live Run — no Recall, so the Run never passes through
            // Town (which would collapse the InFields face and clear this very toggle with it).
            if (simulation.Run.Phase == RunPhase.InField)
                simulation.Relocate(Location);
            else
                simulation.Send(Location);
        }

#if UNITY_EDITOR
        /// <summary>The group's reset is the only thing that clears a Location, so a group of them
        /// without <see cref="ToggleGroup.ResetWithParentPanel"/> keeps a stale selection
        /// after the Run ends - and nothing else would say so.</summary>
        protected override void OnValidate()
        {
            base.OnValidate();

            if (RadioGroup && !RadioGroup.ResetWithParentPanel)
                Debug.LogWarning($"{name}: its ToggleGroup '{RadioGroup.name}' does not reset with its parent " +
                                 "panel - a Location stays selected after the Run ends. Enable " +
                                 "'ResetWithParentPanel' on the group.", RadioGroup);
        }
#endif // UNITY_EDITOR
    }
}
