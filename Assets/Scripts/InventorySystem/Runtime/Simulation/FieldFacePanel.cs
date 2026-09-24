using Submodules.Utility.UI;
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
    /// <see cref="combatPanel"/> is a child of this face but not a child <em>fade</em> — it has
    /// its own <see cref="CanvasGroup"/> and must already be visible during the preview, so it is
    /// cascaded from this panel's own appear/disappear rather than left to inherit alpha from the
    /// parent <see cref="CanvasGroup"/> or to derive its own visibility from anything simulation-side.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FieldFacePanel : SimplePanel
    {
        [Tooltip("The Combat Panel - faded in lockstep with this face, including during the Go " +
                 "Venture preview.")]
        [SerializeField] private SimplePanel combatPanel;

        protected override void BeforeAppear()
        {
            base.BeforeAppear();

            if (combatPanel)
                combatPanel.FadeIn();
        }

        protected override void BeforeDisappear()
        {
            base.BeforeDisappear();

            if (combatPanel)
                combatPanel.FadeOut();
        }
    }
}
