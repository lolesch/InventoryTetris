using Submodules.Utility.Services;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.GUI.Components.Panels;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The InFields face. Purely UI-driven, like <see cref="ToolSmiths.InventorySystem.GUI.Components.Buttons.ToFieldsButton"/>
    /// and <see cref="ToolSmiths.InventorySystem.GUI.Components.Buttons.ToTownButton"/> that fade
    /// it — it carries no <see cref="RunPhase"/> of its own, because Go Venture previews this face
    /// while still <see cref="RunPhase.InTown"/> (no Run exists yet to be InField about; think
    /// opening the Town Portal without stepping through it). <see cref="RunPhasePanel"/> would
    /// keep this face hidden through exactly that preview, which is the one thing it must not do.
    ///
    /// <see cref="combatPanel"/> is a child of this face but not a child <em>fade</em> — it is a
    /// <see cref="SidePanel"/> that derives its own visibility from the Inventory Context, and so
    /// does the Hero Panel beside it. This face is the entry point that requests that context
    /// when it appears (and closes it again when it goes), so the Combat Panel is already up
    /// during the preview and the Hero Panel opens with it.
    /// </summary>
    public sealed class FieldFacePanel : SimplePanel
    {
        [Tooltip("The Combat Panel - a SidePanel whose context this face requests while it is up, " +
                 "including during the Go Venture preview.")]
        [SerializeField] private SidePanel combatPanel;

        protected override void BeforeAppear()
        {
            base.BeforeAppear();

            if (combatPanel)
                _ = combatPanel.RequestContext(true);
        }

        protected override void BeforeDisappear()
        {
            base.BeforeDisappear();

            // Only its own context: a Town Stop already open by the time this face goes is not this panel's to close.
            if (combatPanel && ServiceLocator.IsArmed && combatPanel.IsUpIn(InventoryService.Instance.ActiveContext))
                _ = combatPanel.RequestContext(false);
        }
    }
}
