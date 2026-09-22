using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// A Town Stop's Side Panel, and the Hero Panel's own panel component: each one subscribes
    /// to the Inventory Context and derives its own visibility from it (issue #85). A panel
    /// knows only the one context it was authored with (<see cref="inventoryContext"/>) and
    /// references no other panel. The Hero Panel is referenced by nothing at all, because its
    /// membership in every non-<see cref="InventoryContext.None"/> context is stated once, in
    /// <see cref="InventoryContextState.PanelsFor"/>.
    ///
    /// <para><b>Nothing announces.</b> Until #85 this class pushed its own
    /// <c>SidePanelContext</c> from its appear and disappear hooks (#75). That was right while
    /// every context meant exactly one panel, and became wrong the moment the Hero Panel joined
    /// all of them: a panel that belongs to every context can never be the thing that names one.
    /// Entry points request; panels derive.</para>
    ///
    /// <para><b>Both directions of the close, with no panel knowing the other.</b> Closing the
    /// Hero Panel closes whatever Town Stop panel is open, and closing a Town Stop's panel
    /// closes the Hero Panel too, because in either case both have simply left the derived set.
    /// A handover between two Town Stops is one context change, so the Hero Panel's own derived
    /// answer never flips and it stays up throughout, with no flicker and no intermediate
    /// <see cref="InventoryContext.None"/>.</para>
    ///
    /// <para><b>The toggle drives the request, never the fade.</b>
    /// <see cref="ToolSmiths.InventorySystem.GUI.Components.Toggles.SidePanelToggle"/> calls
    /// <see cref="RequestContext"/> from its own click/hotkey edge and no longer fades this panel
    /// itself: two layers deciding visibility is what this rework removes. The request lands
    /// synchronously, so a staged Sell Basket sale still cancels the instant the Vendor leaves,
    /// not once its fade finishes.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SidePanel : SimplePanel
    {
        [Tooltip("Which Inventory Context this panel belongs to. Visibility is derived from the " +
                 "active context through this value; the toggle that drives the panel requests " +
                 "the same value. Must not be None.")]
        [SerializeField] private InventoryContext inventoryContext = InventoryContext.None;

        /// <summary>
        /// Whether this panel is up in <paramref name="context"/>: <see cref="InventoryContextState.PanelsFor"/>
        /// against the one panel this component owns, <see cref="InventoryContextState.PanelFor"/>
        /// of its authored <see cref="inventoryContext"/>. The one statement of the derivation,
        /// which <see cref="ToolSmiths.InventorySystem.GUI.Components.Toggles.SidePanelToggle"/>
        /// asks rather than recomputing - it needs the same answer for its own pressed visual and
        /// has no business knowing how the answer is reached.
        ///
        /// <para>Also false for a panel authored with no context, which owns no panel bit and can
        /// therefore never be derived by anything.</para>
        /// </summary>
        public bool IsUpIn(InventoryContext context) =>
            (InventoryContextState.PanelsFor(context) & InventoryContextState.PanelFor(inventoryContext))
            != InventoryPanels.None;

        /// <summary>
        /// The visibility the last context application settled on. Tracked so a context that
        /// merely restates it - the Hero Panel's own answer during a Stash-to-Vendor handover -
        /// does not restart a fade that is already where it belongs, which is what the "no
        /// flicker" half of the rule asks for.
        /// </summary>
        private bool? shown;

        /// <summary>
        /// Play mode only: reading a provider's <c>Instance</c> in the editor <i>creates</i> a
        /// provider GameObject when none exists (issue #46), and a panel that enables at
        /// edit-adjacent times - opening the scene, a domain reload, prefab isolation - would
        /// otherwise reach this while the player is only editing.
        ///
        /// <see cref="ApplyContext"/> runs here rather than only on the event so a panel that
        /// enables into an already-open context (the scene loads, or the panel was disabled
        /// through it) starts out agreeing with the context instead of waiting for the next change.
        /// </summary>
        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            var provider = InventoryProvider.Instance;
            if (provider == null)
                return;

            provider.OnContextChanged -= OnContextChanged;
            provider.OnContextChanged += OnContextChanged;

            ApplyContext(provider.ActiveContext);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (Application.isPlaying && InventoryProvider.Instance != null)
                InventoryProvider.Instance.OnContextChanged -= OnContextChanged;
        }

        private void OnContextChanged(InventoryContext context) => ApplyContext(context);

        /// <summary>
        /// Shows or hides this panel for one context, from <see cref="IsUpIn"/>. A panel authored
        /// with no context at all is left exactly as the scene has it: it has no role to derive
        /// and <see cref="OnValidate"/> is what complains about that, not this.
        /// </summary>
        private void ApplyContext(InventoryContext context)
        {
            if (InventoryContextState.PanelFor(inventoryContext) == InventoryPanels.None)
                return;

            var shouldShow = IsUpIn(context);
            if (shown == shouldShow)
                return;

            shown = shouldShow;
            Toggle(shouldShow);
        }

        /// <summary>
        /// The request half (issue #84), called from <see cref="SidePanelToggle"/>'s own
        /// click/hotkey edge and never from a fade hook - entry points request, panels derive.
        /// A no-op while <see cref="inventoryContext"/> is <see cref="InventoryContext.None"/>:
        /// a panel with no role must not touch the context at all, not even to close it.
        /// </summary>
        /// <returns>
        /// <c>true</c> when the request actually reached the provider. The caller gates its own
        /// toggle state on this, so a request that could not be made - no provider, no authored
        /// context, not playing - leaves the button where it was instead of flipping it into a
        /// state the context knows nothing about. That divergence is the whole bug class here:
        /// a pressed toggle and an open panel are one fact and must not be able to disagree.
        /// </returns>
        public bool RequestContext(bool open)
        {
            if (!Application.isPlaying || inventoryContext == InventoryContext.None)
                return false;

            var provider = InventoryProvider.Instance;
            if (provider == null)
                return false;

            if (open)
                provider.SetContext(inventoryContext);
            else
                provider.CloseContext();

            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// The authored-data check nothing else can make: a panel left at
        /// <see cref="InventoryContext.None"/> fades normally but is invisible to the whole
        /// mechanism - nothing derives its visibility, so it never appears, and its toggle
        /// requests nothing. The code has no way to infer what a panel is for.
        /// </summary>
        private void OnValidate()
        {
            if (inventoryContext == InventoryContext.None)
                Debug.LogWarning($"{name}: SidePanel has no InventoryContext - nothing derives " +
                                 "its visibility, so it never appears, and its toggle requests " +
                                 "nothing.", gameObject);
        }
#endif // UNITY_EDITOR
    }
}
