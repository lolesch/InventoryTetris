using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// A <see cref="SimplePanel"/> that fades itself in to match the Run's phase, previously
    /// driven by <c>MinimapController</c> (removed; issue #85's "panels derive, nothing
    /// announces" now applies to <see cref="RunPhase"/> too).
    ///
    /// <para><b>Only the InTown face uses this.</b> The InFields face
    /// (<see cref="FieldFacePanel"/>) is deliberately not phase-driven - Go Venture previews it
    /// while still <see cref="RunPhase.InTown"/>, and a <see cref="RunPhase"/>-driven panel would
    /// keep it hidden through exactly that preview. Entering the Field is always a deliberate UI
    /// action (<c>ToFieldsButton</c> fades InFields in, a Location Send commits) with no
    /// involuntary edge to catch. Leaving is not: Death ends the Run without any button click, so
    /// InTown needs its own route back in. Authored here for <see cref="RunPhase.InTown"/>, its
    /// <see cref="SimplePanel.FadeIn"/> goes through the InTown/InFields <see cref="PanelGroup"/>
    /// on Death exactly as a real To Town click would, which is what fades InFields (and, via its
    /// own cascade, the Combat Panel) back out without either of them needing to hear about
    /// <see cref="RunPhase"/> themselves.</para>
    /// </summary>
    public sealed class RunPhasePanel : SimplePanel
    {
        [Tooltip("The RunPhase this panel is up during. Applied on every PhaseChanged and on " +
                 "enable, against whatever the Run's phase actually is right now.")]
        [SerializeField] private RunPhase activeDuring = RunPhase.InTown;

        /// <summary>
        /// Play mode only, mirroring <see cref="ToolSmiths.InventorySystem.Inventories.InventoryProvider.TrySubscribeContextChanged"/>'s
        /// guard: a panel that enables edit-adjacent (scene load, domain reload, prefab
        /// isolation) is left unsubscribed rather than creating a provider.
        /// </summary>
        private void OnEnable()
        {
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

        private void SyncToPhase(RunPhase phase) => Toggle(phase == activeDuring);
    }
}
