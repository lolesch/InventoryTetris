using System.Collections.Generic;
using System.Reflection;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Two-state minimap shell (issue #55): swaps between a Town side (Stash / Vendor /
    /// Healer / Go Venture on town background art) and a Field side (location toggles +
    /// To Town on field background art). Two <see cref="RadioGroup"/> components control
    /// mutual exclusion within each state; the active <see cref="RunPhase"/> drives which
    /// group is interactable and which background sprite is shown.
    ///
    /// Toggle click actions (Send / Recall wiring) and panel open/close orchestration
    /// are out of scope — both are issue #56.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapPanel : AbstractPanel
    {
        private static readonly FieldInfo s_radioGroupField =
            typeof(AbstractToggle).GetField("radioGroup", BindingFlags.NonPublic | BindingFlags.Instance);

        [Header("Backgrounds")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite townBackground;
        [SerializeField] private Sprite fieldBackground;

        [Header("Radio Groups")]
        [SerializeField] private RadioGroup townGroup;
        [SerializeField] private RadioGroup fieldGroup;

        [Header("Town Toggles (manually positioned)")]
        [SerializeField] private AbstractToggle stashToggle;
        [SerializeField] private AbstractToggle vendorToggle;
        [SerializeField] private AbstractToggle healerToggle;
        [SerializeField] private AbstractToggle goVentureToggle;

        [Header("Field Toggles")]
        [SerializeField] private List<LocationToggle> locationToggles = new();
        [SerializeField] private AbstractToggle toTownToggle;

        private readonly List<AbstractToggle> _townToggles = new();

        protected override void BeforeAppear()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            var run = provider.Run;

            // Unsubscribe first to avoid duplicate handlers across show/hide cycles
            // (AbstractPanel keeps the GameObject enabled — FadeOut never triggers OnDisable).
            run.PhaseChanged -= OnPhaseChanged;
            run.PhaseChanged += OnPhaseChanged;

            CollectTownToggles();

            // Unregister before re-registering to avoid double-registration if OnEnable
            // auto-discovered a different RadioGroup via hierarchy before BeforeAppear ran.
            UnregisterTownToggles();
            UnregisterFieldToggles();
            RegisterTownToggles();
            RegisterFieldToggles();

            SyncToPhase(run.Phase);
        }

        private void OnDisable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged -= OnPhaseChanged;

            UnregisterTownToggles();
            UnregisterFieldToggles();
        }

        private void OnPhaseChanged(RunPhase phase) => SyncToPhase(phase);

        private void SyncToPhase(RunPhase phase)
        {
            var inTown = phase == RunPhase.InTown;

            SetGroupInteractable(townGroup, inTown);
            SetGroupInteractable(fieldGroup, !inTown);

            if (backgroundImage != null)
                backgroundImage.sprite = inTown ? townBackground : fieldBackground;
        }

        private void CollectTownToggles()
        {
            _townToggles.Clear();

            if (stashToggle != null) _townToggles.Add(stashToggle);
            if (vendorToggle != null) _townToggles.Add(vendorToggle);
            if (healerToggle != null) _townToggles.Add(healerToggle);
            if (goVentureToggle != null) _townToggles.Add(goVentureToggle);
        }

        private void RegisterTownToggles()
        {
            if (townGroup == null) return;

            foreach (var toggle in _townToggles)
            {
                AssignRadioGroup(toggle, townGroup);
                townGroup.Register(toggle);
            }
        }

        private void UnregisterTownToggles()
        {
            if (townGroup == null) return;

            foreach (var toggle in _townToggles)
                townGroup.Unregister(toggle);
        }

        private void RegisterFieldToggles()
        {
            if (fieldGroup == null) return;

            foreach (var toggle in locationToggles)
                if (toggle != null)
                {
                    AssignRadioGroup(toggle, fieldGroup);
                    fieldGroup.Register(toggle);
                }

            if (toTownToggle != null)
            {
                AssignRadioGroup(toTownToggle, fieldGroup);
                fieldGroup.Register(toTownToggle);
            }
        }

        private void UnregisterFieldToggles()
        {
            if (fieldGroup == null) return;

            foreach (var toggle in locationToggles)
                if (toggle != null) fieldGroup.Unregister(toggle);

            if (toTownToggle != null) fieldGroup.Unregister(toTownToggle);
        }

        /// <summary>
        /// Sets the backing <c>radioGroup</c> field on <see cref="AbstractToggle"/> so the
        /// toggle registers with the correct group on its next <c>OnEnable</c>.
        /// The property is get-only — reflection avoids modifying the Utility submodule.
        /// </summary>
        private static void AssignRadioGroup(AbstractToggle toggle, RadioGroup group)
        {
            if (toggle == null || s_radioGroupField == null) return;

            s_radioGroupField.SetValue(toggle, group);
        }

        private static void SetGroupInteractable(RadioGroup group, bool interactable)
        {
            if (group == null) return;

            foreach (var toggle in group.GetComponentsInChildren<AbstractToggle>(true))
                toggle.interactable = interactable;
        }
    }
}
