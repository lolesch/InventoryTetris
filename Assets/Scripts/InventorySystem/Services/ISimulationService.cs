using Submodules.Utility.Extensions;
using Submodules.Utility.Services;
using System;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The state service over the Hero and the World (ADR-0015): the Run, its loot flow, the Send /
    /// Relocate / Return transitions and the tick that drives them. It reads the current Hero and
    /// World off the <see cref="ISession"/> on every call and caches neither - the Run lives on the
    /// World and the Corpse and selected Location on the Hero - so a hero load changes what it
    /// operates on without it noticing. The tick is hosted by <see cref="GameLoop"/>.
    /// </summary>
    public interface ISimulationService : IService
    {
        /// <summary>
        /// The authored Locations by stable id, and the one <see cref="EncounterProfile"/> this service
        /// runs each of them as. The loader reads it so a restored Corpse matches the profile a
        /// <see cref="Send"/> uses.
        /// </summary>
        ILocationRegistry Locations { get; }

        /// <summary>
        /// The current hero's Unlock State for <paramref name="unlockable"/> (ADR-0023). <see cref="Send"/> and
        /// <see cref="Relocate"/> go only to an <see cref="UnlockState.Active"/> or <see cref="UnlockState.Complete"/> Location.
        /// </summary>
        UnlockState StateOf(Unlockable unlockable);

        /// <summary>
        /// Raised once when a Location's Complete changed the hero's Completed set, so anything showing an
        /// <see cref="UnlockState"/> re-reads it. Not raised for a Complete Location cleared again.
        /// </summary>
        event Action ProgressionChanged;

        /// <summary>The current World's Run FSM - <see cref="RunPhase.None"/> until a <see cref="Send"/>. Built on the first ask.</summary>
        RunState Run { get; }

        /// <summary>
        /// The live Run's loot flow - <c>null</c> in Town. Exposed so the Combat Panel's stats readout
        /// can show a full bag visibly stranding loot.
        /// </summary>
        LootFlow LootFlow { get; }

        /// <summary>
        /// Raised once, after a Run has ended and everything its end does has been applied: the Run is
        /// back in Town, and on a Death the bag is already buried in the Corpse and the penalty paid. A
        /// Return raises it the same way. The Run's own ended event fires earlier, inside the Death
        /// handling, so a save hooked to that one would write a hero with no Corpse and a full bag.
        /// This one is the service's, not the Run's, so it survives a World swap: a subscriber attaches
        /// once.
        /// </summary>
        event Action<RunResult> RunSettled;

        /// <summary>
        /// Send the hero to <paramref name="location"/>: <see cref="RunPhase.None"/> to
        /// <see cref="RunPhase.Live"/>. Selects the Location on the Hero, opens the Run and, if the
        /// Hero's Corpse is at exactly this Location, lays it back out (to the bag where it fits, else
        /// the ground). Throws while the hero is down: a Death leaves him so until <see cref="Respawn"/>;
        /// also for a Location that is not open to him. Nothing changes then.
        /// </summary>
        void Send(LocationConfig location);

        /// <summary>
        /// Whether the hero is up and <paramref name="location"/> is open to him, so <see cref="Send"/> and
        /// <see cref="Relocate"/> will not refuse for either reason. A caller that has to tell a refusal from a
        /// fault asks this instead of catching their <see cref="InvalidOperationException"/>.
        /// </summary>
        bool CanSend(LocationConfig location);

        /// <summary>
        /// Move the live Run to <paramref name="location"/> without a Return. Throws unless a Run is
        /// live with its hero up, and for a Location that is not open to him. Like <see cref="Send"/>, lays the Hero's Corpse back out when the new
        /// Location is where it fell.
        /// </summary>
        void Relocate(LocationConfig location);

        /// <summary>
        /// Whether <see cref="Recall"/> will not refuse: a Run is live with its hero up and no portal opening, and
        /// the hero's Inventory (not the Stash) holds a Portal Scroll. Also true after a Location's Complete.
        /// </summary>
        bool CanRecall { get; }

        /// <summary>
        /// The Run's half of <see cref="CanRecall"/>, without the scan of the Inventory: a Run is live with its hero
        /// up and no portal opening. A view that already counts the scrolls (<see cref="PortalScrollCount"/>) asks
        /// this and the count rather than <see cref="CanRecall"/>, which would count them again.
        /// </summary>
        bool RunAllowsRecall { get; }

        /// <summary>
        /// Open a Town Portal on the live Run (ADR-0022): takes one Portal Scroll from the hero's Inventory and
        /// starts the Run's opening, after which the Run is <see cref="RunPhase.Held"/> and the
        /// Hero's <c>TownPortal</c> is the Run's Location. Throws when <see cref="CanRecall"/> is false;
        /// the Inventory is then unchanged. A Death in the opening is an ordinary Death and the scroll stays spent.
        /// </summary>
        void Recall();

        /// <summary>The Portal Scrolls in the hero's Inventory, stacks summed; the Stash does not count. The number the Recall button shows.</summary>
        int PortalScrollCount { get; }

        /// <summary>
        /// Resume the held Run exactly where it was and close the hero's Town Portal. Throws unless the Run is
        /// <see cref="RunPhase.Held"/>. A Relocate from Held, a Send, a Return and a Death all leave the portal open.
        /// </summary>
        void Resume();

        /// <summary>Whether the Hero's Corpse lies at <paramref name="location"/>, selected or not.</summary>
        bool CorpseLiesAt(LocationConfig location);

        /// <summary>The Origin every Location's arena is centred on, the frame a Corpse's fall position is kept in.</summary>
        Coordinate ArenaOrigin { get; }

        /// <summary>Raised when the Hero's Corpse is buried, replaced or recovered.</summary>
        event Action CorpseChanged;

        /// <summary>
        /// Whether <see cref="Return"/> will not refuse: a Run is live (not held) with its hero up and no portal
        /// opening (ADR-0022). A caller that can be clicked in any of those states asks this instead of catching.
        /// </summary>
        bool CanReturn { get; }

        /// <summary>End the Run with everything kept. Refused once the hero is down - that ends in a Death - and while a Recall's portal opens.</summary>
        RunResult Return();

        /// <summary>
        /// Ends a live Run so the game can close in Town: a Return, or the Death when the hero is already
        /// down, in that order of precedence the tick uses. Raises <see cref="RunSettled"/> like either
        /// does. A quit does not wait for a Recall's opening: the opening ends with the Run. Nothing happens when
        /// no Run is in the Field.
        /// </summary>
        /// <returns>Whether a Run was ended.</returns>
        bool LeaveField();

        /// <summary>
        /// The way back from a Death: revives the hero at full Health and Resource and raises
        /// <see cref="Respawned"/>. A Death leaves him at 0 Health in Town; only this (or a hero load) stands him
        /// up. Does nothing to a hero who is not down.
        /// </summary>
        void Respawn();

        /// <summary>Raised by <see cref="Respawn"/> with the hero up. Belongs to the service, so a subscriber attaches once across hero loads.</summary>
        event Action Respawned;

        /// <summary>
        /// Whether the live Run is frozen: <see cref="Tick"/> does nothing at all while it is true, so
        /// the Encounter, the Hero's regeneration and both ways home stand still. Only a Run in the
        /// Field can be paused, and the Run's end (Return or Death) or the next <see cref="Send"/>
        /// clears it, so it is never true in Town.
        /// </summary>
        bool IsPaused { get; }

        /// <summary>Raised with the new state whenever <see cref="IsPaused"/> changes.</summary>
        event Action<bool> PausedChanged;

        /// <summary>
        /// Freezes (<c>true</c>) or resumes (<c>false</c>) the live Run. Pausing is refused - nothing
        /// happens - unless a Run is in the Field; resuming is always allowed. Separate from the Behaviour
        /// Profile's sim speed, which is the hero's persisted setting and is left as the player set it.
        /// </summary>
        void SetPaused(bool paused);

        /// <summary>
        /// Whether a menu holds the live Run still. A second freeze beside <see cref="IsPaused"/>, which stays
        /// the player's own: releasing the hold gives the Pause back as it was found. Like the Pause it can only
        /// be true while a Run is in the Field, so it is never true in Town and the Run's end clears it.
        /// </summary>
        bool IsMenuFrozen { get; }

        /// <summary>
        /// Holds (<c>true</c>) or releases (<c>false</c>) the live Run for a menu. Holding is refused - nothing
        /// happens - unless a Run is in the Field; releasing is always allowed, and holding twice is one hold.
        /// Never changes <see cref="IsPaused"/> or raises <see cref="PausedChanged"/>.
        /// </summary>
        void SetMenuFreeze(bool held);

        /// <summary>
        /// The sim seconds <paramref name="deltaSeconds"/> of real time is worth: 0 while <see cref="IsPaused"/>
        /// or <see cref="IsMenuFrozen"/>, otherwise scaled by the Behaviour Profile's sim speed. What <see cref="Tick"/>
        /// advances by, for views that animate on sim time (they scale with the speed slider and freeze with the Run).
        /// </summary>
        float SimDelta(float deltaSeconds);

        /// <summary>
        /// The sim seconds <paramref name="deltaSeconds"/> of real time is worth to a walk the hero is shown taking
        /// (ADR-0021): <see cref="SimDelta"/> without the freeze, since a Pause does not stop a walk. Scales with the
        /// Behaviour Profile's sim speed, so a faster sim walks faster.
        /// </summary>
        float WalkDelta(float deltaSeconds);

        /// <summary>
        /// Arena units per second the hero walks, read live off his <c>MovementSpeed</c> stat: what an Encounter
        /// walks him at and what a Hero figure Travels at in Town (ADR-0021). Never negative.
        /// </summary>
        float HeroWalkingSpeed { get; }

        /// <summary>
        /// The factor the view's time runs at: the tuned slow-down for a short real-time span after a Death, 1
        /// otherwise. Already inside <see cref="SimDelta"/>; cleared early by <see cref="Respawn"/> and a hero load.
        /// </summary>
        float ViewTimeFactor { get; }

        /// <summary>
        /// Whether the Death beat is under way, from a Death until its slow-down runs out or is cleared. The views
        /// hold what the Death shows back until it ends.
        /// </summary>
        bool DeathBeatPlaying { get; }

        /// <summary>
        /// Raised once when <see cref="DeathBeatPlaying"/> goes from true to false, however it ended. A subscriber
        /// that needs the hero still down asks him.
        /// </summary>
        event Action DeathBeatEnded;

        /// <summary>
        /// One frame of the simulation, <paramref name="deltaSeconds"/> of real time: the Hero's
        /// regeneration (Town and Field alike), the Encounter while a Run is in the Field, and the
        /// Run's way home on a Death. Scaled by the
        /// Behaviour Profile's sim speed here, never through <c>Time.timeScale</c> (ADR-0008).
        /// Does nothing while <see cref="IsPaused"/> or <see cref="IsMenuFrozen"/>.
        /// </summary>
        void Tick(float deltaSeconds);
    }
}
