using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Switch Context toggle, plus recall. Turning it on while
    /// <see cref="RunPhase.InField"/> recalls the hero (<see cref="SimulationProvider.Recall"/>)
    /// on top of the panel switching <see cref="MultiplePanelToggle"/> already does.
    ///
    /// The side effect lives here rather than in an observer: <c>AbstractToggle</c> exposes
    /// <see cref="MultiplePanelToggle.OnToggle"/> as its only extension point, so a
    /// toggle that does more owns a type that does more. <see cref="MapPanel"/> keeps the
    /// reference only to drive <c>interactable</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TownRecallToggle : MultiplePanelToggle
    {
        protected override void OnToggle()
        {
            base.OnToggle();

            if (!IsOn)
                return;

            var provider = SimulationProvider.Instance;
            if (provider == null)
                return;

            // Only recall while InField — in Town the toggle is non-interactable, but a
            // phase change mid-frame could still land here.
            if (provider.Run.Phase != RunPhase.InField)
                return;

            provider.Recall();
        }
    }
}
