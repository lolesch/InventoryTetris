using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Inventories;
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

        /// <summary>Registers with <see cref="InventoryProvider.RegisterFieldFacePanel"/> so
        /// <see cref="InventoryProvider.IsFieldReachable"/> tracks this face without the provider
        /// holding a hard reference to it - a provider re-created mid-run would otherwise lose
        /// it silently. A panel "should always stay enabled" (<see cref="SimplePanel"/>'s own
        /// doc), so this fires effectively once per Play session.</summary>
        private void OnEnable() => InventoryProvider.RegisterFieldFacePanel(this);

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
