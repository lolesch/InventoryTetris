using System;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The Inventory Context rule, as a rule the tests can actually reach (issue #83) -
    /// engine-free and below the provider for the same reason #54 gave: the provider compiles
    /// into <c>Assembly-CSharp</c>, which no test assembly can reference.
    ///
    /// <para>With entry points requesting a context rather than panels announcing their own,
    /// nothing needs to name which context it is clearing, so <see cref="Close"/> takes no
    /// argument and always yields <see cref="InventoryContext.None"/> - there is no
    /// per-context clear.</para>
    ///
    /// <para>The rule reaches the scene through two static derivations, <see cref="PanelFor"/>
    /// and <see cref="PanelsFor"/>, so a panel's subscription and a toggle's pressed-visual
    /// resync both read the same statement of "which panels does this context derive" rather
    /// than each re-deriving it (issue #85).</para>
    /// </summary>
    public sealed class InventoryContextState
    {
        /// <summary>The context currently active, or <see cref="InventoryContext.None"/>.</summary>
        public InventoryContext Active { get; private set; } = InventoryContext.None;

        /// <summary>The open overlays (Character Stats, Sim Settings): panels beside the context, so opening or closing one never changes the context. Only a toggle opens one, and only a Town Stop or the Ground Items panel taking the screen closes it (see <see cref="Set"/>); closing never brings one back.</summary>
        private InventoryPanels overlays;

        private const InventoryPanels SharedSpace = InventoryPanels.CharacterStats | InventoryPanels.SimSettings;

        /// <summary>
        /// The panels <see cref="Active"/> and the open overlays derive together (see <see cref="PanelsFor"/>).
        /// </summary>
        public InventoryPanels Panels => PanelsFor(Active, overlays);

        /// <summary>
        /// Raised with the current context whenever <see cref="Active"/> actually changes - never
        /// on a no-op <see cref="Set"/> - and whenever an overlay changes, with the
        /// context unchanged: a subscriber re-reads <see cref="Panels"/>. A handover between two Town Stop
        /// contexts fires this exactly once, with no intermediate <see cref="InventoryContext.None"/>.
        /// </summary>
        public event Action<InventoryContext> Changed;

        /// <summary>
        /// Requests <paramref name="context"/>, replacing whatever was active. Requesting the
        /// already-active context is a no-op, except that asking for <see cref="InventoryContext.Ground"/>
        /// or a Town Stop closes an overlay: the Ground Items panel's screen space is taken back, and a Town
        /// Stop leaves no room for one. The overlay stays closed when the context closes.
        /// </summary>
        public void Set(InventoryContext context)
        {
            var overlayClosed = context is InventoryContext.Ground or InventoryContext.Stash or InventoryContext.Vendor or InventoryContext.Healer
                && (overlays & SharedSpace) != InventoryPanels.None;
            if (overlayClosed)
                overlays &= ~SharedSpace;

            if (Active == context && !overlayClosed)
                return;

            Active = context;
            Changed?.Invoke(Active);
        }

        /// <summary>
        /// Closes whatever is active. Closing is always <see cref="InventoryContext.None"/> -
        /// closing a Town Stop's panel closes the Hero Panel with it. A no-op from
        /// <see cref="InventoryContext.None"/>.
        /// </summary>
        public void Close() => Set(InventoryContext.None);

        /// <summary>Whether <paramref name="context"/> names an overlay - the Character Stats or Sim Settings panel - rather than a context.</summary>
        public static bool IsOverlay(InventoryContext context) =>
            context is InventoryContext.CharacterStats or InventoryContext.SimSettings;

        /// <summary>Opens or closes an overlay (see <see cref="IsOverlay"/>). Restating the current state is a no-op.</summary>
        public void SetOverlay(InventoryContext overlay, bool open)
        {
            var bit = PanelFor(overlay);
            if (!IsOverlay(overlay) || ((overlays & bit) != InventoryPanels.None) == open)
                return;

            // The overlays share the Ground Items panel's screen space, so only one is up at a time.
            overlays = open ? (overlays & ~SharedSpace) | bit : overlays & ~bit;
            Changed?.Invoke(Active);
        }

        /// <summary>Closes whichever overlay is up. <c>false</c> when none was.</summary>
        public bool CloseOverlay()
        {
            if ((overlays & SharedSpace) == InventoryPanels.None)
                return false;

            overlays &= ~SharedSpace;
            Changed?.Invoke(Active);
            return true;
        }

        /// <summary>
        /// Applies the map that just came up: a map that leaves <see cref="Active"/> unreachable drops it
        /// to <see cref="InventoryContext.None"/>, and arriving on either map reopens nothing on its own.
        /// </summary>
        public void SyncToMap(bool onFieldMap)
        {
            if (!IsReachable(Active, onFieldMap))
                Close();
        }

        /// <summary>
        /// Whether <paramref name="context"/> can be active while a map is up - a function of the map, not
        /// of the Run phase (a held Run puts a Town map up beside a Run). The Field map reaches only
        /// <see cref="InventoryContext.None"/>, <see cref="InventoryContext.Hero"/> and
        /// <see cref="InventoryContext.Ground"/>; the Town map reaches every context, the Ground's among
        /// them (the World's ground is there too).
        /// </summary>
        public static bool IsReachable(InventoryContext context, bool onFieldMap) =>
            !onFieldMap || context is InventoryContext.None or InventoryContext.Hero or InventoryContext.Ground;

        /// <summary>
        /// The one panel a context names for itself: the Hero Panel for
        /// <see cref="InventoryContext.Hero"/>, that Town Stop's own panel otherwise, and
        /// nothing for <see cref="InventoryContext.None"/>. The single statement of "which
        /// panel is mine", which a panel asks about its own authored context and a toggle asks
        /// about the panel it drives (issue #85).
        /// </summary>
        public static InventoryPanels PanelFor(InventoryContext context) => context switch
        {
            InventoryContext.Hero => InventoryPanels.Hero,
            InventoryContext.Stash => InventoryPanels.Stash,
            InventoryContext.Vendor => InventoryPanels.Vendor,
            InventoryContext.Healer => InventoryPanels.Healer,
            InventoryContext.Ground => InventoryPanels.Ground,
            InventoryContext.CharacterStats => InventoryPanels.CharacterStats,
            InventoryContext.SimSettings => InventoryPanels.SimSettings,
            _ => InventoryPanels.None,
        };

        /// <summary>
        /// The panels a context derives: <see cref="PanelFor"/> plus the Hero Panel, whose
        /// membership in every non-<c>None</c> context is stated here once rather than repeated
        /// on each Town Stop panel or as a branch inside each one (issue #85). The Ground Items panel is
        /// up wherever no Town Stop is open - <c>None</c>, <c>Hero</c> and <c>Ground</c> - so closing a Town
        /// Stop brings it back with no request. The Character Stats and Sim Settings overlays, when open, add their own panel and
        /// take the Ground Items panel's screen space (so only one of them is up at a time), so that panel is down
        /// whatever the context; the Hero Panel and the Town Stop panel are untouched. Closing the overlay brings it back.
        /// </summary>
        public static InventoryPanels PanelsFor(InventoryContext context, InventoryPanels overlays = InventoryPanels.None)
        {
            var panels = context switch
            {
                InventoryContext.None => InventoryPanels.Ground,
                InventoryContext.Hero => InventoryPanels.Hero | InventoryPanels.Ground,
                _ => InventoryPanels.Hero | PanelFor(context),
            };

            if ((overlays & SharedSpace) != InventoryPanels.None)
                panels &= ~InventoryPanels.Ground;

            return panels | overlays;
        }
    }
}
