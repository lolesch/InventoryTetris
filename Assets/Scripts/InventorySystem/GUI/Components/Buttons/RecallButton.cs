using Submodules.Utility.Services;
using Submodules.Utility.UI;
using TMPro;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// The HUD Recall button (ADR-0022): shows the Portal Scrolls in the Inventory and Recalls on a click, the same
    /// Recall the <see cref="GameAction.Recall"/> key and a right-click on a scroll make. Interactable exactly while
    /// <see cref="ISimulationService.CanRecall"/>, so it is greyed out in Town, with the hero down, through the death
    /// beat and an opening, and with no scroll. Read every frame, not subscribed: the count follows any pick up, buy,
    /// sell or stash of a scroll, and a hero load replaces the Inventory without announcing it. The frame's one
    /// scan of the Inventory is the count; the Run's half of <c>CanRecall</c> is asked without a second scan.
    /// </summary>
    public sealed class RecallButton : AbstractButton
    {
        [SerializeField, Tooltip("Shows the number of Portal Scrolls in the Inventory.")]
        private TextMeshProUGUI countLabel;

        private int _shownCount = -1;

        protected override void OnClick()
        {
            var simulation = SimulationService.Instance;

            if (simulation.CanRecall)
                simulation.Recall();
        }

        private void Update()
        {
            if (!Application.isPlaying || !ServiceLocator.IsArmed)
                return;

            var simulation = SimulationService.Instance;

            var count = simulation.PortalScrollCount;
            if (count != _shownCount)
            {
                _shownCount = count;

                if (countLabel != null)
                    countLabel.text = count.ToString();
            }

            var canRecall = count > 0 && simulation.RunAllowsRecall;
            if (interactable != canRecall)
                interactable = canRecall;
        }
    }
}
