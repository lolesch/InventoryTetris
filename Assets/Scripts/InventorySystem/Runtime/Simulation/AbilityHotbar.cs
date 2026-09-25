using Submodules.Utility.UI;
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
    public sealed class AbilityHotbar : SimplePanel
    {
        [SerializeField] private ScaleTween strikeIcon;
        [SerializeField] private ScaleTween castIcon;

        private EncounterSimulation encounter;

        /// <summary>
        /// Play mode only, mirroring <see cref="RunPhasePanel.OnEnable"/>'s guard: a panel that
        /// enables edit-adjacent (scene load, domain reload, prefab isolation) is left
        /// unsubscribed rather than creating a provider.
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

            if (Application.isPlaying)
            {
                var provider = SimulationProvider.Instance;
                if (provider != null)
                    provider.Run.PhaseChanged -= SyncToPhase;
            }

            UnsubscribeEncounter();
        }

        private void SyncToPhase(RunPhase phase)
        {
            Toggle(phase == RunPhase.InField);

            UnsubscribeEncounter();

            if (phase != RunPhase.InField)
                return;

            encounter = SimulationProvider.Instance.Run.Encounter;
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
