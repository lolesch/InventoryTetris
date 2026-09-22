using System.Collections.Generic;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.GUI.Components.Toggles;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Orchestrates Town/Field navigation (issues #55, #56; #58 correction 2026-09-17): shows
    /// exactly one of <see cref="inTownPanel"/> / <see cref="inFieldPanel"/> at a time. Not a
    /// panel itself — it never fades, it only decides which of its two children does.
    ///
    /// The face normally follows <see cref="RunPhase"/> — <see cref="inTownPanel"/> shown
    /// InTown, <see cref="inFieldPanel"/> shown InField — but Go Venture previews the InField
    /// panel while still InTown so the player can pick a destination; To Town backs out of
    /// that preview. A location toggle <see cref="SimulationProvider.Send"/>s; To Town
    /// <see cref="SimulationProvider.Recall"/>s while InField. Entering the Field shows
    /// <see cref="combatPanel"/> and returning to Town (Recall or Death) hides it again — by
    /// name, on the phase edge (issue #85). It shares no group with the Town Stop panels any
    /// more: with the Town Stop contexts unreachable during a Run, nothing can share the left
    /// side with it, so it needs no group membership to stay exclusive.
    ///
    /// <see cref="GoVentureButton"/> / <see cref="ToTownButton"/> hold a reference to this
    /// controller and call <see cref="GoVenture"/> / <see cref="ToTown"/> on click; this class
    /// deliberately does not hold a reference back to them. Interactable gating went with that
    /// reference: Go Venture, To Town, and the Stash/Vendor/Healer <see cref="townGroup"/> all
    /// live on whichever panel is currently faded out, and <c>CanvasGroup.blocksRaycasts</c>
    /// already makes a faded-out panel's children non-interactive. Two sets are the exception,
    /// each for a reason of its own:
    ///
    /// <list type="bullet">
    /// <item>Locations keep their clicks while the Field face is up — <see cref="inFieldPanel"/>
    /// stays shown whether the player is previewing (InTown) or has actually travelled
    /// (InField), so only each <c>LocationToggle</c>'s own <c>interactable</c>, gated on
    /// <see cref="_inTown"/>, stops a stray click from reassigning <see cref="fieldGroup"/>'s
    /// selection while already in the field.</item>
    /// <item>The Town Stops keep their <i>hotkeys</i>, which read <c>interactable</c> and so
    /// bypass that <c>CanvasGroup</c> entirely — nothing about the fade reaches them. They are
    /// gated on the face being shown instead, which covers the Go Venture preview as well
    /// (issue #73).</item>
    /// </list>
    ///
    /// <para><b>This class is no longer a visibility authority (issue #85).</b> It used to hold
    /// a <c>PanelGroup</c> over the left panels and close a Side Panel by driving it; that group
    /// is gone, and the Inventory Context is the one answer to which panels are up. Every panel
    /// derives its own visibility from the context and every toggle resyncs its own pressed
    /// visual from it, so all this class still does about the left side is call
    /// <see cref="InventoryProvider.SyncContextToPhase"/> whenever the phase could have taken a
    /// context out of reach — which is what actually closes a Town Stop's panel on Send, Recall,
    /// Death or the Go Venture preview. <see cref="townGroup"/> remains, answering only which
    /// button is pressed (issue #74's input/content split).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapController : MonoBehaviour
    {
        [Header("Faces")]
        [SerializeField] private SimplePanel inTownPanel;
        [SerializeField] private SimplePanel inFieldPanel;

        [Header("Radio Groups")]
        [SerializeField] private RadioGroup townGroup;
        [SerializeField] private RadioGroup fieldGroup;

        [Header("Locations (children of fieldGroup, manually positioned)")]
        [SerializeField] private List<LocationToggle> locationToggles = new();

        [Header("Left Panels")]
        [Tooltip("The left-side Combat Panel — shown on Send (InField), hidden on Recall / Death " +
                 "(InTown). Phase-driven and ungrouped since issue #85: with the Town Stop " +
                 "contexts unreachable during a Run, nothing can share the left side with it.")]
        [SerializeField] private SimplePanel combatPanel;

        private bool _inTown = true;

        /// <summary>
        /// Every <see cref="SidePanelToggle"/> under <see cref="townGroup"/>, each with the
        /// <c>interactable</c> it was authored with. Resolved from the group rather than listed
        /// by hand so a Town Stop added later is gated by construction instead of by remembering
        /// to author it here. The authored value is what the gate restores on the way back into
        /// Town, so a Town Stop shipped disabled stays disabled rather than coming back on for
        /// merely being InTown.
        /// </summary>
        private readonly Dictionary<SidePanelToggle, bool> _townToggles = new();

        private void Awake()
        {
            if (!townGroup)
                return;

            foreach (var toggle in townGroup.GetComponentsInChildren<SidePanelToggle>(true))
                _townToggles[toggle] = toggle.interactable;
        }

        private void OnEnable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged += SyncToPhase;

            if (fieldGroup)
                fieldGroup.OnGroupChanged += OnFieldSelectionChanged;

            SyncToPhase(provider.Run.Phase);
        }

        private void OnDisable()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            provider.Run.PhaseChanged -= SyncToPhase;

            if (fieldGroup)
                fieldGroup.OnGroupChanged -= OnFieldSelectionChanged;
        }

        /// <summary>
        /// Re-derive the whole minimap from the Run phase: which face is shown, whether the
        /// Combat Panel is up, and — on entering the Field — that no Town Stop context is left
        /// active. Death routes through here too: <see cref="RunState.HandleDeath"/> fires
        /// <c>PhaseChanged(InTown)</c>, which resets the face and hides the Combat Panel.
        ///
        /// <see cref="InventoryProvider.SyncContextToPhase"/> is the Inventory Context's own
        /// phase-reachability rule (issue #84): Send, Recall and Death all reach this one method,
        /// so this is the single place all three drop a Town Stop context. Closing a Town Stop
        /// panel is then what that context change does on its own (issue #85) — the panels derive
        /// their visibility from it and the toggles resync their pressed visuals from it, so
        /// nothing here has to reach into a panel.
        /// </summary>
        private void SyncToPhase(RunPhase phase)
        {
            _inTown = phase == RunPhase.InTown;

            ApplyFace(!_inTown);

            InventoryProvider.Instance?.SyncContextToPhase(!_inTown);

            // Phase-driven and ungrouped: the Combat Panel is up for the length of a Run and
            // nothing else can be on the left side with it, because the contexts that name the
            // Town Stop panels are unreachable while it is.
            if (combatPanel)
                combatPanel.Toggle(!_inTown);

            // Recall/Death land back InTown with the just-visited LocationToggle still
            // ActiveMember — RadioGroup.Activate no-ops when the clicked toggle is already
            // selected (RadioGroup.cs), so without this the same Location could never be
            // re-picked. Send (InTown -> InField) never reaches this branch, so a fresh
            // selection is never clobbered on the way in.
            if (_inTown && fieldGroup)
                fieldGroup.ClearActive();
        }

        /// <summary>Go Venture (InTown only — the button is non-interactable otherwise):
        /// preview the Field face and close any open Town Stop panel. The preview makes every
        /// Town Stop context unreachable exactly as a real Send would (issue #84) — no
        /// <see cref="RunState.PhaseChanged"/> fires here, so <see cref="SyncToPhase"/> never
        /// runs and the context needs its own call, treating the preview as "in field" for
        /// reachability only.</summary>
        public void GoVenture()
        {
            ApplyFace(true);

            InventoryProvider.Instance?.SyncContextToPhase(true);
        }

        /// <summary>To Town: Recall while InField (the resulting <c>PhaseChanged</c> re-syncs
        /// the face via <see cref="SyncToPhase"/>), or back out of a Go Venture preview while
        /// still InTown.</summary>
        public void ToTown()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            if (provider.Run.Phase == RunPhase.InField)
            {
                provider.Recall();
                return;
            }

            ApplyFace(false);
        }

        /// <summary>Show one face, hide the other, and re-gate the two sets the fade does not
        /// reach on its own — the locations and the Town Stops (see class doc for why each is
        /// gated differently).</summary>
        private void ApplyFace(bool showField)
        {
            if (inTownPanel)
                inTownPanel.Toggle(!showField);

            if (inFieldPanel)
                inFieldPanel.Toggle(showField);

            foreach (var toggle in locationToggles)
                if (toggle != null)
                    toggle.interactable = _inTown;

            // Gated on the face, not on _inTown: the Go Venture preview has faded the Town face
            // out while still InTown, and a Town Stop is as unreachable there as it is after a
            // Send — a click cannot reach it, so its hotkey must not either (#73).
            foreach (var entry in _townToggles)
                entry.Key.interactable = entry.Value && !showField;
        }

        /// <summary>
        /// A selected location Sends the hero there. Fires on deselection too
        /// (<see cref="RadioGroup.ActiveMember"/> null) — that is not a destination, so it
        /// is ignored.
        /// </summary>
        private void OnFieldSelectionChanged(AbstractToggle toggle)
        {
            if (toggle is not LocationToggle location || location.Location == null) return;

            var provider = SimulationProvider.Instance;
            if (provider == null || provider.Run.Phase != RunPhase.InTown) return;

            provider.Send(location.Location);
        }

        /// <summary>Drives <see cref="SyncToPhase"/> straight from the Inspector's right-click
        /// menu, bypassing <see cref="SimulationProvider"/>/<see cref="RunState"/> entirely — for
        /// isolating whether a broken panel swap is this class's wiring or the Send/Recall path
        /// that normally raises <see cref="RunState.PhaseChanged"/>.
        ///
        /// <b>Leaves <see cref="_inTown"/> desynced from the live <see cref="RunState.Phase"/></b>
        /// once anything real (<see cref="SimulationProvider.Send"/>/<see cref="SimulationProvider.Recall"/>,
        /// <see cref="ToTownButton"/>, a <see cref="LocationToggle"/> click) is in play — those all
        /// read the real phase, not this cached field, so forcing one and then driving the other
        /// looks broken (locations stop being selectable, To Town stops matching what's on
        /// screen). Call <see cref="DebugResyncFromLivePhase"/> to pull it back before switching
        /// back to testing the real flow, rather than restarting Play Mode.
        ///
        /// <b>Not isolated from <see cref="InventoryProvider"/> (issue #84).</b>
        /// <see cref="SyncToPhase"/> calls <see cref="InventoryProvider.SyncContextToPhase"/>
        /// unconditionally, so forcing InField here really does drop an active Town Stop context
        /// to <see cref="InventoryContext.None"/> - and <see cref="DebugResyncFromLivePhase"/>
        /// does not undo that, since it only re-derives <see cref="_inTown"/> and the face, not
        /// the Inventory Context. Bypassing <see cref="SimulationProvider"/>/<see cref="RunState"/>
        /// was always the point of this menu; <see cref="InventoryProvider"/> was never part of
        /// that isolation and #84 does not attempt to add it.</summary>
        [ContextMenu("Debug: Force InTown")]
        private void DebugForceInTown() => SyncToPhase(RunPhase.InTown);

        [ContextMenu("Debug: Force InField")]
        private void DebugForceInField() => SyncToPhase(RunPhase.InField);

        /// <summary>Re-derives the whole minimap from <see cref="SimulationProvider"/>'s actual
        /// <see cref="RunState.Phase"/> — undoes the desync <see cref="DebugForceInTown"/> /
        /// <see cref="DebugForceInField"/> leave behind, without needing a Play Mode restart.</summary>
        [ContextMenu("Debug: Resync From Live Phase")]
        private void DebugResyncFromLivePhase()
        {
            var provider = SimulationProvider.Instance;
            if (provider != null)
                SyncToPhase(provider.Run.Phase);
        }
    }
}
