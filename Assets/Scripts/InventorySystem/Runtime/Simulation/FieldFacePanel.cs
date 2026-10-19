using Submodules.Utility.Services;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.GUI.Components.Panels;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Field map's face. Purely UI-driven, like <see cref="ToolSmiths.InventorySystem.GUI.Components.Buttons.ToFieldsButton"/>
    /// and <see cref="ToolSmiths.InventorySystem.GUI.Components.Buttons.ToTownButton"/> that fade
    /// it - it carries no <see cref="RunPhase"/> of its own, because Go Venture previews this face
    /// while still <see cref="RunPhase.None"/> (no Run exists yet to be Live about; think
    /// opening the Town Portal without stepping through it). <see cref="RunPhasePanel"/> would
    /// keep this face hidden through exactly that preview, which is the one thing it must not do.
    ///
    /// <see cref="combatPanel"/> is a child of this face but not a child <em>fade</em> - it is a
    /// <see cref="SidePanel"/> that derives its own visibility from the Inventory Context (up on
    /// <c>None</c> and <c>Ground</c>) while this map is up, so this face requests no context when it appears:
    /// the Combat Panel is up and the Hero Panel stays closed until asked for. It closes the Ground
    /// context again as it goes.
    /// </summary>
    public sealed class FieldFacePanel : SimplePanel
    {
        [Tooltip("The Combat Panel - a SidePanel whose Ground context this face closes as it goes.")]
        [SerializeField] private SidePanel combatPanel;

        /// <summary>Raised as the map starts to come up: the hero's figure learns that the Town Exit brought it up.</summary>
        public event System.Action Appearing;

        protected override void BeforeAppear()
        {
            base.BeforeAppear();
            Appearing?.Invoke();

            if (combatPanel)
                combatPanel.SetMapUp(true);
        }

        protected override void BeforeDisappear()
        {
            base.BeforeDisappear();

            if (!combatPanel)
                return;

            // Only its own context: a Town Stop already open by the time this face goes is not this panel's to close.
            if (ServiceLocator.IsArmed && combatPanel.IsUpIn(InventoryService.Instance.ActiveContext))
                _ = combatPanel.RequestContext(false);

            combatPanel.SetMapUp(false);
        }
    }
}
