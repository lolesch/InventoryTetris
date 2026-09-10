using System;
using System.Collections.Generic;
using System.Reflection;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Two-state minimap (issues #55, #56): swaps between a Town face (Stash / Vendor /
    /// Healer / Go Venture on town background art) and a Field face (location toggles +
    /// To Town on field background art). Two <see cref="RadioGroup"/> components control
    /// mutual exclusion within each face.
    ///
    /// The face normally follows <see cref="RunPhase"/> — Town face InTown, Field face
    /// InField — but Go Venture previews the Field face while still InTown so the player can
    /// pick a destination; To Town backs out of that preview. A location toggle
    /// <see cref="SimulationProvider.Send"/>s; To Town <see cref="SimulationProvider.Recall"/>s
    /// while InField. Entering the Field fades the <c>combatPanel</c> in and closes any open
    /// Side Panel; returning to Town (Recall or Death) fades it back out. Toggle-click
    /// subscriptions are made idempotent (detach before attach) because
    /// <see cref="BeforeAppear"/> can run again before <see cref="OnDisable"/>.
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

        [Header("Combat Panel")]
        [Tooltip("The left-side Combat Panel — fades in on Send (InField), out on Recall / Death (InTown).")]
        [SerializeField] private AbstractPanel combatPanel;

        private readonly List<AbstractToggle> _townToggles = new();

        /// <summary>One stored handler per location toggle so the click knows which toggle
        /// fired — <see cref="AbstractToggle.OnToggle"/> runs before <see cref="RadioGroup"/>
        /// updates <c>ActivatedToggle</c>, so that field can't be trusted here. Stored (not a
        /// fresh lambda each call) so <see cref="UnsubscribeFromToggleClicks"/> can detach it.</summary>
        private readonly Dictionary<LocationToggle, Action<bool>> _locationHandlers = new();

        /// <summary>Which minimap face is shown. Always the Field face while InField; the
        /// player can also flip to it InTown with Go Venture, then back with To Town.</summary>
        private bool _showingFieldFace;
        private bool _inTown = true;

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

            SubscribeToToggleClicks();

            SyncToPhase(run.Phase);
        }

        private void OnDisable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged -= OnPhaseChanged;

            UnsubscribeFromToggleClicks();
            UnregisterTownToggles();
            UnregisterFieldToggles();
        }

        private void OnPhaseChanged(RunPhase phase) => SyncToPhase(phase);

        /// <summary>
        /// Re-derive the whole minimap from the Run phase: which face is shown, whether the
        /// Combat Panel is up, and — on entering the Field — that no Side Panel is left open.
        /// Death routes through here too: <see cref="RunState.HandleDeath"/> fires
        /// <c>PhaseChanged(InTown)</c>, which resets the face and fades the Combat Panel out.
        /// </summary>
        private void SyncToPhase(RunPhase phase)
        {
            _inTown = phase == RunPhase.InTown;
            _showingFieldFace = !_inTown;

            ApplyFace();

            if (combatPanel != null)
            {
                if (_inTown) combatPanel.FadeOut();
                else combatPanel.FadeIn();
            }

            if (!_inTown)
                CloseSidePanels();
        }

        /// <summary>
        /// Paint the current face: swap the background art and set which toggles are live.
        /// Town toggles follow the Town face; location toggles are only sendable while
        /// actually InTown; To Town is live whenever the Field face is up (to Recall InField,
        /// or to back out of a Go Venture preview InTown).
        /// </summary>
        private void ApplyFace()
        {
            if (backgroundImage != null)
                backgroundImage.sprite = _showingFieldFace ? fieldBackground : townBackground;

            SetGroupInteractable(townGroup, !_showingFieldFace);

            foreach (var toggle in locationToggles)
                if (toggle != null)
                    toggle.interactable = _showingFieldFace && _inTown;

            if (toTownToggle != null)
                toTownToggle.interactable = _showingFieldFace;

            if (!_showingFieldFace && goVentureToggle != null && goVentureToggle.IsOn)
                goVentureToggle.SetToggle(false);
        }

        private void SubscribeToToggleClicks()
        {
            Rewire(goVentureToggle, OnGoVentureToggled);
            Rewire(toTownToggle, OnToTownToggled);

            foreach (var toggle in locationToggles)
                Rewire(toggle, LocationHandlerFor(toggle));
        }

        private void UnsubscribeFromToggleClicks()
        {
            Unwire(goVentureToggle, OnGoVentureToggled);
            Unwire(toTownToggle, OnToTownToggled);

            foreach (var pair in _locationHandlers)
                Unwire(pair.Key, pair.Value);
        }

        private Action<bool> LocationHandlerFor(LocationToggle toggle)
        {
            if (toggle == null) return null;

            if (!_locationHandlers.TryGetValue(toggle, out var handler))
                _locationHandlers[toggle] = handler = isOn => OnLocationToggled(toggle, isOn);

            return handler;
        }

        /// <summary>Idempotent subscribe — detach before attach so a second
        /// <see cref="BeforeAppear"/> before <see cref="OnDisable"/> never stacks handlers.</summary>
        private static void Rewire(AbstractToggle toggle, Action<bool> handler)
        {
            if (toggle == null) return;

            toggle.OnToggle -= handler;
            toggle.OnToggle += handler;
        }

        private static void Unwire(AbstractToggle toggle, Action<bool> handler)
        {
            if (toggle != null)
                toggle.OnToggle -= handler;
        }

        /// <summary>Go Venture (InTown only) previews the Field face so the player can pick a
        /// destination, and closes any open Stash / Vendor Side Panel.</summary>
        private void OnGoVentureToggled(bool isOn)
        {
            if (!isOn || !_inTown) return;

            _showingFieldFace = true;
            ApplyFace();
            CloseSidePanels();
        }

        /// <summary>The clicked location toggle Sends the hero there — only while InTown
        /// (in the Field the toggles are non-interactable; this also guards a phase change
        /// mid-frame). The resulting <c>PhaseChanged(InField)</c> re-syncs the rest.</summary>
        private void OnLocationToggled(LocationToggle sender, bool isOn)
        {
            if (!isOn || sender == null || sender.Location == null) return;

            var provider = SimulationProvider.Instance;
            if (provider == null || provider.Run.Phase != RunPhase.InTown) return;

            provider.Send(sender.Location);
        }

        /// <summary>To Town Recalls the hero when InField; when it is only a Go Venture
        /// preview InTown, it just flips back to the Town face.</summary>
        private void OnToTownToggled(bool isOn)
        {
            if (!isOn) return;

            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            if (provider.Run.Phase == RunPhase.InField)
            {
                provider.Recall();
                return;
            }

            _showingFieldFace = false;
            ApplyFace();
        }

        /// <summary>Clear whichever Side Panel is open — a no-op when none is
        /// (<see cref="SidePanelState.Clear"/>). Used on Go Venture and on entering the Field.</summary>
        private static void CloseSidePanels()
        {
            var inventory = InventoryProvider.Instance;
            if (inventory != null)
                inventory.ClearSidePanel(inventory.ActiveSidePanel);
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
