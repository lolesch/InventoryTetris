using Submodules.Utility.UI;
using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// The HUD Recall button (ADR-0022): shows the Portal Scrolls in the Inventory and Recalls on a click, the same
    /// Recall the <see cref="GameAction.Recall"/> key and a right-click on a scroll make. Interactable exactly while
    /// <see cref="ISimulationService.CanRecall"/>, so it is greyed out in Town, with the hero down, through the death
    /// beat and an opening, and with no scroll. It refreshes on the Inventory's content change and on the Run's phase
    /// changes, and binds both again on a hero load, which replaces the Inventory and the Run.
    /// </summary>
    public sealed class RecallButton : AbstractButton
    {
        [SerializeField, Tooltip("Shows the number of Portal Scrolls in the Inventory.")]
        private TextMeshProUGUI countLabel;

        private AbstractDimensionalContainer _bag;
        private RunState _run;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (Session.TrySubscribeHeroLoaded(Bind))
                Bind();
        }

        protected override void OnDisable()
        {
            Session.UnsubscribeHeroLoaded(Bind);
            Unbind();

            base.OnDisable();
        }

        protected override void OnClick()
        {
            var simulation = SimulationService.Instance;

            if (simulation.CanRecall)
                simulation.Recall();
        }

        private void Bind()
        {
            Unbind();

            _bag = Session.Instance.Hero.Inventory;
            _run = SimulationService.Instance.Run;
            _bag.OnContentChanged += OnContentChanged;
            _run.PhaseChanged += OnPhaseChanged;

            Refresh();
        }

        private void Unbind()
        {
            if (_bag != null)
                _bag.OnContentChanged -= OnContentChanged;

            if (_run != null)
                _run.PhaseChanged -= OnPhaseChanged;

            _bag = null;
            _run = null;
        }

        private void OnContentChanged(Dictionary<Vector2Int, Package> _) => Refresh();

        private void OnPhaseChanged(RunPhase _) => Refresh();

        private void Refresh()
        {
            var simulation = SimulationService.Instance;

            if (countLabel != null)
                countLabel.text = simulation.PortalScrollCount.ToString();

            interactable = simulation.CanRecall;
        }
    }
}
