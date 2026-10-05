using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The centre-bottom Ability hotbar (issue #62) — minimal v1: two icons, Strike and Cast,
    /// each punch-scaling (<see cref="ScaleTween"/>) on its matching <see cref="EncounterSimulation"/>
    /// event. No cooldown fill or resource-gated greyed-out state yet — own epic, per
    /// <c>dev/specs/2026-09-08-arpg-screen-layout-design.md</c>.
    ///
    /// Faded like <see cref="RunPhasePanel"/> — up only while the Run is actually
    /// <see cref="RunPhase.InField"/>, since Strike/Cast only fire on a live Encounter and there
    /// is nothing to flash In Town. Unlike <see cref="RunPhasePanel"/> it also has to track the
    /// live <see cref="EncounterSimulation"/> itself, not just the phase: a fresh Encounter is
    /// built on every <see cref="RunState.Send"/>, so the Strike/Cast subscription is
    /// re-established each time the Run steps into the Field rather than held once at enable.
    /// </summary>
    public sealed class AbilityHotbar : MonoBehaviour
    {
        [SerializeField] private ScaleTween strikeIcon;
        [SerializeField] private ScaleTween castIcon;

        private RunState run;
        private EncounterSimulation encounter;

        /// <summary>
        /// Play mode only, mirroring <see cref="RunPhasePanel.OnEnable"/>'s guard: a panel that
        /// enables edit-adjacent (scene load, domain reload, prefab isolation) is left
        /// unsubscribed rather than reading a service that was never armed.
        /// </summary>
        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _ = Session.TrySubscribeHeroLoaded(BindRun);

            BindRun();
        }

        // Lets go of the Run it subscribed to, not whatever the service holds by now.
        private void OnDisable()
        {
            Session.UnsubscribeHeroLoaded(BindRun);

            ReleaseRun();
        }

        // A hero load (#114) replaces the World and with it the Run: follow the new one.
        private void BindRun()
        {
            ReleaseRun();

            run = SimulationService.Instance.Run;
            run.PhaseChanged += SyncToPhase;
            run.Relocated += OnRelocated;

            SyncToPhase(run.Phase);
        }

        private void ReleaseRun()
        {
            if (run != null)
            {
                run.PhaseChanged -= SyncToPhase;
                run.Relocated -= OnRelocated;
                run = null;
            }

            UnsubscribeEncounter();
        }

        // A Relocate swaps the Encounter without a phase change — follow it to the new one.
        private void OnRelocated() => SyncToPhase(RunPhase.InField);

        private void SyncToPhase(RunPhase phase)
        {
            UnsubscribeEncounter();

            if (phase != RunPhase.InField)
                return;

            SubscribeEncounter();
        }

        private void SubscribeEncounter()
        {
            encounter = run.Encounter;
            if (encounter == null)
                return;

            encounter.HeroStriked += OnHeroStriked;
            encounter.HeroCast += OnHeroCast;
        }

        private void UnsubscribeEncounter()
        {
            if (encounter == null)
                return;

            encounter.HeroStriked -= OnHeroStriked;
            encounter.HeroCast -= OnHeroCast;
            encounter = null;
        }

        private void OnHeroStriked()
        {
            if (strikeIcon)
                strikeIcon.Punch();
        }

        private void OnHeroCast()
        {
            if (castIcon)
                castIcon.Punch();
        }
    }
}
