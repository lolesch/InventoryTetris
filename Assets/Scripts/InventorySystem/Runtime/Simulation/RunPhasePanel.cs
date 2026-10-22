using Submodules.Utility.Services;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// A <see cref="SimplePanel"/> that fades itself in to match the Run's phase, previously
    /// driven by <c>MinimapController</c> (removed; issue #85's "panels derive, nothing
    /// announces" now applies to <see cref="RunPhase"/> too). It is the Town map's face, up whenever the Run is
    /// not live (<see cref="RunMaps.TownMapIsUp"/>): with no Run, and with a held one, once the Field figure has
    /// gone into the portal.
    ///
    /// <para><b>Only the InTown face uses this.</b> The InFields face
    /// (<see cref="FieldFacePanel"/>) is deliberately not phase-driven - Go Venture previews it
    /// while still <see cref="RunPhase.None"/>, and a <see cref="RunPhase"/>-driven panel would
    /// keep it hidden through exactly that preview. Entering the Field is always a deliberate UI
    /// action (<c>ToFieldsButton</c> fades InFields in, a Location Send commits) with no
    /// involuntary edge to catch. Leaving is not: a Return ends the Run without any button click, so
    /// InTown needs its own route back in. Authored here for <see cref="RunPhase.None"/>, its
    /// <see cref="SimplePanel.Expand"/> goes through the InTown/InFields <see cref="PanelGroup"/>
    /// on a Return exactly as a real To Town click would, which is what fades InFields (and, via its
    /// own cascade, the Combat Panel) back out without either of them needing to hear about
    /// <see cref="RunPhase"/> themselves. A Death does not bring it up: the hero is still down, and
    /// <see cref="ISimulationService.Respawn"/> does.</para>
    /// </summary>
    public sealed class RunPhasePanel : SimplePanel
    {
        [Tooltip("The Field map's Hero figure. A held Run brings the Town map up only once it has gone into the " +
                 "portal. Empty: at once.")]
        [SerializeField] private HeroFigure fieldFigure;

        private RunState _run;

        /// <summary>Raised as the Town map starts to come up, whatever brought it.</summary>
        public event System.Action Appearing;

        protected override void BeforeAppear()
        {
            base.BeforeAppear();
            Appearing?.Invoke();
        }

        /// <summary>
        /// Play mode only, mirroring <see cref="ToolSmiths.InventorySystem.Services.InventoryService.TrySubscribeContextChanged"/>'s
        /// guard: a panel that enables edit-adjacent (scene load, domain reload, prefab
        /// isolation) is left unsubscribed rather than reading a service that was never armed.
        /// </summary>
        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _ = Session.TrySubscribeHeroLoaded(BindRun);
            SimulationService.Instance.Respawned += Resync;
            if (fieldFigure)
                fieldFigure.PortalEntered += OnPortalEntered;

            BindRun();
        }

        // The figure walks into the portal on its own clock: the Town map of a held Run comes up when he is gone.
        private void OnPortalEntered()
        {
            if (_run.Phase == RunPhase.Held && !Session.Instance.Hero.IsDead)
                ToggleState(true);
        }

        // Lets go of the Run it subscribed to, not whatever the service holds by now.
        protected override void OnDisable()
        {
            base.OnDisable();

            if (fieldFigure)
                fieldFigure.PortalEntered -= OnPortalEntered;

            Session.UnsubscribeHeroLoaded(BindRun);
            if (ServiceLocator.IsArmed)
                SimulationService.Instance.Respawned -= Resync;

            ReleaseRun();
        }

        // A hero load (#114) replaces the World and with it the Run: follow the new one.
        private void BindRun()
        {
            ReleaseRun();

            _run = SimulationService.Instance.Run;
            _run.PhaseChanged += SyncToPhase;

            SyncToPhase(_run.Phase);
        }

        private void ReleaseRun()
        {
            if (_run == null)
                return;

            _run.PhaseChanged -= SyncToPhase;
            _run = null;
        }

        // A Death ends the Run but leaves the hero down; the Town map waits for Respawn.
        private void SyncToPhase(RunPhase phase)
        {
            // A held Run's Town map waits for the hero to go into the portal (OnPortalEntered).
            if (phase == RunPhase.Held && fieldFigure != null)
                return;

            ToggleState(phase.TownMapIsUp() && !Session.Instance.Hero.IsDead);
        }

        private void Resync() => SyncToPhase(_run.Phase);
    }
}
