using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The Run finite-state machine (issue #21; spec <i>The RunState machine</i>). Three states —
    /// <see cref="RunPhase.None"/>, <see cref="RunPhase.Live"/> and, after a <see cref="Recall"/>,
    /// <see cref="RunPhase.Held"/>; <see cref="Send"/> opens
    /// a Run at a Location, <see cref="Return"/> and <see cref="HandleDeath"/> close it. Send and
    /// Return are instant — there is no <c>Traveling</c> state — and Death is not a game-over,
    /// only a penalised end of the Run.
    ///
    /// While <see cref="RunPhase.Live"/> a single <see cref="EncounterSimulation"/> runs the
    /// endless series of Encounters; this class is the façade the engine-side driver (issue #26)
    /// and the behaviour triggers (issue #23) talk to. It owns the one running total the sim
    /// does not — <see cref="CurrencyBanked"/>, fed per kill by the loot flow (issue #24) — and
    /// on exit freezes a <see cref="RunResult"/> read off the sim, adding the Death penalty when
    /// the outcome is <see cref="RunOutcome.Died"/>.
    ///
    /// Engine-free: the "Location" is the plain <see cref="EncounterProfile"/> the
    /// <c>LocationConfig</c> ScriptableObject (issue #25) maps onto, currency is a base-unit
    /// integer, and the sim is built through an injected factory so nothing here needs the hero
    /// adapter or <c>HeroBehaviour</c>.
    /// </summary>
    public sealed class RunState
    {
        private readonly Func<EncounterProfile, EncounterSimulation> _startEncounter;
        private readonly RunPenalty _penalty;
        private readonly Action _onHeroDowned;
        private readonly float _recallOpening;

        private EncounterSimulation _encounter;
        private float _openingLeft; // sim seconds until a Recall's portal is open; meaningful while IsRecalling
        private EncounterProfile _location;
        private EncounterTotals _carried; // totals of the Encounters a Relocate left behind
        private long _currencyBanked;
        private bool _heroDown;
        private bool _recalling;

        // ─── construction ───────────────────────────────────────────────────

        /// <param name="startEncounter">
        /// Builds the <see cref="EncounterSimulation"/> for a Location when the hero is Sent
        /// there. Issue #26 supplies one that binds the hero adapter, the roll source and
        /// <c>HeroBehaviour.Engagement</c>; a test binds a fake hero and a scripted roll source.
        /// </param>
        /// <param name="penalty">The cost of a Death. <c>default</c> (<see cref="RunPenalty.None"/>) costs nothing extra.</param>
        /// <param name="recallOpening">Sim seconds a <see cref="Recall"/>'s portal takes to open, during which the Run stays live.</param>
        public RunState(Func<EncounterProfile, EncounterSimulation> startEncounter, RunPenalty penalty = default,
            float recallOpening = 0f)
        {
            _startEncounter = startEncounter ?? throw new ArgumentNullException(nameof(startEncounter));
            _penalty = penalty;
            _recallOpening = Math.Max(0f, recallOpening);
            _onHeroDowned = OnHeroDowned;
        }

        // ─── state ──────────────────────────────────────────────────────────

        /// <summary>Which state the Run is in.</summary>
        public RunPhase Phase { get; private set; } = RunPhase.None;

        /// <summary>The Location the Run is at — <c>null</c> while <see cref="RunPhase.None"/>.</summary>
        public EncounterProfile Location => _location;

        /// <summary>The live Encounter simulation — <c>null</c> while <see cref="RunPhase.None"/>.</summary>
        public EncounterSimulation Encounter => _encounter;

        /// <summary>
        /// Currency banked to the Wallet this Run so far, in base units (<see cref="BankCurrency"/>).
        /// Zero while <see cref="RunPhase.None"/>; the Death fee is computed against it.
        /// </summary>
        public long CurrencyBanked => _currencyBanked;

        /// <summary>
        /// Whether the hero has been downed in the current Run. The sim has ended and the only
        /// way out is <see cref="HandleDeath"/> — <see cref="Return"/> is refused. <c>false</c>
        /// while <see cref="RunPhase.None"/>.
        /// </summary>
        public bool HeroIsDown => _heroDown;

        /// <summary>
        /// Whether a <see cref="Recall"/>'s portal is still opening. The Run is <see cref="RunPhase.Live"/> through it and
        /// only a Death cancels it; <see cref="Return"/>, <see cref="Relocate"/> and a second Recall are refused.
        /// </summary>
        public bool IsRecalling => _recalling;

        /// <summary>
        /// The result of the most recently finished Run, or <c>null</c> if none has finished yet
        /// or a new Run has since been <see cref="Send"/>.
        /// </summary>
        public RunResult? LastResult { get; private set; }

        /// <summary>
        /// Raised once a Run ends, on both outcomes — <see cref="Return"/> and
        /// <see cref="HandleDeath"/> alike. The loot flow (issue #24) subscribes to clear
        /// whatever Drops are still on the ground, per GLOSSARY.md's Drop entry: "a Drop still
        /// on the ground when the Run ends is gone, on Return or Death alike."
        /// </summary>
        public event Action RunEnded;

        /// <summary>
        /// Raised on every phase transition — <see cref="Send"/> (→ Live) and
        /// <see cref="EndRun"/> (→ None). The UI subscribes to toggle panels.
        /// </summary>
        public event Action<RunPhase> PhaseChanged;

        /// <summary>
        /// Raised when <see cref="Relocate"/> swaps the live Encounter for one at another
        /// Location. The Run never leaves <see cref="RunPhase.Live"/>, so
        /// <see cref="PhaseChanged"/> and <see cref="RunEnded"/> stay quiet; anything holding the
        /// old <see cref="Encounter"/> re-reads it here.
        /// </summary>
        public event Action Relocated;

        // ─── transitions ────────────────────────────────────────────────────

        /// <summary>
        /// Start a Run at <paramref name="location"/> — <see cref="RunPhase.None"/> →
        /// <see cref="RunPhase.Live"/>. Builds a fresh Encounter and zeroes the Run totals.
        /// </summary>
        public void Send(EncounterProfile location)
        {
            if (_heroDown)
                throw new InvalidOperationException("The hero is down — the Run ends in HandleDeath before a new one can be Sent.");

            if (location == null) throw new ArgumentNullException(nameof(location));
            if (Phase != RunPhase.None)
                throw new InvalidOperationException("A Run is already live — Return or HandleDeath before Sending again.");

            var encounter = _startEncounter(location)
                ?? throw new InvalidOperationException("The encounter factory returned null.");

            _location = location;
            _encounter = encounter;
            _encounter.HeroDowned += _onHeroDowned;
            ResetRunTotals();
            LastResult = null;

            Phase = RunPhase.Live;
            PhaseChanged?.Invoke(Phase);
        }

        /// <summary>
        /// Move the live Run to <paramref name="location"/> without ending it — stays
        /// <see cref="RunPhase.Live"/>, stops the current Encounter and starts a fresh one there.
        /// This is not a Return: the Run's totals (<see cref="CurrencyBanked"/>, kills, XP, clears,
        /// duration) carry on into the one <see cref="RunResult"/> the Run eventually freezes, and
        /// no <see cref="RunEnded"/> or <see cref="PhaseChanged"/> is raised (<see cref="Relocated"/>
        /// is). Refused with no Run (nothing to move) and once the hero is down (that Run ends in
        /// <see cref="HandleDeath"/>). The new Encounter is built first, so a factory that throws
        /// leaves the current one running.
        /// </summary>
        public void Relocate(EncounterProfile location)
        {
            RequireLive();
            if (_heroDown)
                throw new InvalidOperationException("The hero is down — the Run ends in HandleDeath, not a Relocate.");
            RequirePortalNotOpening();
            if (location == null) throw new ArgumentNullException(nameof(location));

            var encounter = _startEncounter(location)
                ?? throw new InvalidOperationException("The encounter factory returned null.");

            _carried = FreezeEncounter();
            _encounter.HeroDowned -= _onHeroDowned;

            _location = location;
            _encounter = encounter;
            _encounter.HeroDowned += _onHeroDowned;

            Relocated?.Invoke();
        }

        /// <summary>
        /// Open a Town Portal on the live Run (ADR-0022). The Run stays <see cref="RunPhase.Live"/>, the Encounter
        /// ticking and hits landing, for the opening's sim time (<see cref="Advance"/> counts it, the Encounter's
        /// Complete phase included); then it goes <see cref="RunPhase.Held"/>. A Death in the opening cancels it.
        /// Refused with no Run, while Held, while a portal is already opening and once the hero is down.
        /// </summary>
        public void Recall()
        {
            RequireLive();
            if (_heroDown)
                throw new InvalidOperationException("The hero is down — the Run ends in HandleDeath, not a Recall.");
            if (_recalling)
                throw new InvalidOperationException("A portal is already opening.");

            _recalling = true;
            _openingLeft = _recallOpening;
        }

        /// <summary>
        /// Bank <paramref name="deltaSeconds"/> of real time on the Encounter and run every whole
        /// tick now due. Forwards to <see cref="EncounterSimulation.Advance"/> (already
        /// sim-speed-scaled by the caller); returns the tick count. A no-op with no Run and while
        /// <see cref="RunPhase.Held"/>. An opening Recall takes its time off the same clock and holds the Run
        /// once it has run out, unless the hero went down in it.
        /// </summary>
        public int Advance(float deltaSeconds)
        {
            if (Phase != RunPhase.Live)
                return 0;

            if (!_recalling)
                return _encounter.Advance(deltaSeconds);

            // The Encounter gets the opening's sim time and no more: what is left of the frame is held.
            var step = Math.Min(deltaSeconds, _openingLeft);
            var ticks = _encounter.Advance(step);
            _openingLeft -= step;

            if (_recalling && _openingLeft <= 0f)
            {
                _recalling = false;
                Phase = RunPhase.Held;
                PhaseChanged?.Invoke(Phase);
            }

            return ticks;
        }

        /// <summary>
        /// Record <paramref name="baseUnits"/> of currency banked to the Wallet by a kill's loot
        /// (issue #24). Accumulates into <see cref="CurrencyBanked"/>; only valid while a Run is
        /// live and the hero is still up — once it is down the Run's take is frozen for
        /// <see cref="HandleDeath"/> to read.
        /// </summary>
        public void BankCurrency(long baseUnits)
        {
            if (Phase != RunPhase.Live)
                throw new InvalidOperationException("Currency only banks during a Run.");
            if (_heroDown)
                throw new InvalidOperationException("The hero is down — no more currency banks before HandleDeath closes the Run.");
            if (baseUnits < 0L)
                throw new ArgumentOutOfRangeException(nameof(baseUnits), baseUnits, "Cannot bank a negative amount.");

            _currencyBanked += baseUnits;
        }

        /// <summary>
        /// End the Run with everything kept — <see cref="RunPhase.Live"/> →
        /// <see cref="RunPhase.None"/>, freezing and returning a <see cref="RunOutcome.Returned"/>
        /// result. Refused once the hero is down: that Run ends in <see cref="HandleDeath"/>.
        /// </summary>
        public RunResult Return()
        {
            RequireLive();
            if (_heroDown)
                throw new InvalidOperationException("The hero is down — the Run ends in HandleDeath, not a Return.");
            RequirePortalNotOpening();

            var result = RunResult.Returned(FreezeEncounter(), _currencyBanked);
            EndRun(result);
            return result;
        }

        /// <summary>
        /// End the Run in Death — <see cref="RunPhase.Live"/> → <see cref="RunPhase.None"/>,
        /// freezing and returning a <see cref="RunOutcome.Died"/> result. Refused unless the hero
        /// is actually down (<see cref="HeroIsDown"/>): that guard is what stops a driver bug from
        /// imposing the Death penalty on a Run that is still winnable — a live hero's Run only
        /// ever ends in <see cref="Return"/>.
        ///
        /// On top of the kept accumulation it computes the penalty: <see cref="RunResult.XpLost"/>
        /// is <see cref="RunPenalty.XpLossFraction"/> of <paramref name="xpTowardNextLevel"/>,
        /// <see cref="RunResult.CurrencyFee"/> is <see cref="RunPenalty.CurrencyFeeFraction"/> of
        /// <see cref="CurrencyBanked"/>. The bag → Corpse hand-off is issue #22.
        /// </summary>
        /// <param name="xpTowardNextLevel">The hero's current progress toward its next level, in XP.</param>
        public RunResult HandleDeath(int xpTowardNextLevel)
        {
            RequireLive();
            if (!_heroDown)
                throw new InvalidOperationException("The hero is not down — the Run ends in Return, not HandleDeath.");
            if (xpTowardNextLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(xpTowardNextLevel), xpTowardNextLevel, "Progress toward the next level cannot be negative.");

            var totals = FreezeEncounter();

            // RunPenalty's fractions are validated to [0, 1] and both inputs here are already
            // non-negative (the guard above, BankCurrency's own guard), so the product can only
            // land in [0, its input] — no further clamping needed.
            var xpLost = (int)Math.Round((double)_penalty.XpLossFraction * xpTowardNextLevel, MidpointRounding.AwayFromZero);
            var currencyFee = (long)Math.Round((double)_penalty.CurrencyFeeFraction * _currencyBanked, MidpointRounding.AwayFromZero);

            var result = RunResult.Died(totals, _currencyBanked, xpLost, currencyFee, _encounter.HeroPosition);
            EndRun(result);
            return result;
        }

        // ─── internals ──────────────────────────────────────────────────────

        // A Death is not held: it cancels an opening portal, and the Run ends in HandleDeath as usual.
        private void OnHeroDowned()
        {
            _heroDown = true;
            _recalling = false;
        }

        private void RequirePortalNotOpening()
        {
            if (_recalling)
                throw new InvalidOperationException("A portal is opening — the Run is held first, or a Death ends it.");
        }

        private void RequireLive()
        {
            if (Phase != RunPhase.Live)
                throw new InvalidOperationException("No Run is in the Field.");
        }

        /// <summary>Stop the fight (idempotent) and read the Run's running totals out — the live Encounter's plus any carried over a Relocate.</summary>
        private EncounterTotals FreezeEncounter()
        {
            _encounter.Abandon(); // no-op if the hero going down already ended it
            return _carried.Plus(new EncounterTotals(
                _encounter.SettledXp, _encounter.EnemiesDefeated,
                _encounter.EncountersCleared, _encounter.Duration));
        }

        private void ResetRunTotals()
        {
            _carried = default;
            _currencyBanked = 0L;
            _heroDown = false;
            _recalling = false;
        }

        private void EndRun(RunResult result)
        {
            _encounter.HeroDowned -= _onHeroDowned;
            _encounter = null;
            _location = null;
            ResetRunTotals();
            Phase = RunPhase.None;
            LastResult = result;
            PhaseChanged?.Invoke(Phase);
            RunEnded?.Invoke();
        }
    }
}
