using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The Run finite-state machine (issue #21; spec <i>The RunState machine</i>). Two states —
    /// <see cref="RunPhase.InTown"/> and <see cref="RunPhase.InField"/>; <see cref="Send"/> opens
    /// a Run at a Location, <see cref="Recall"/> and <see cref="HandleDeath"/> close it. Send and
    /// Recall are instant — there is no <c>Traveling</c> state — and Death is not a game-over,
    /// only a penalised trip back to Town.
    ///
    /// While <see cref="RunPhase.InField"/> a single <see cref="EncounterSimulation"/> runs the
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

        private EncounterSimulation _encounter;
        private EncounterProfile _location;
        private long _currencyBanked;
        private bool _heroDown;

        // ─── construction ───────────────────────────────────────────────────

        /// <param name="startEncounter">
        /// Builds the <see cref="EncounterSimulation"/> for a Location when the hero is Sent
        /// there. Issue #26 supplies one that binds the hero adapter, the roll source and
        /// <c>HeroBehaviour.Engagement</c>; a test binds a fake hero and a scripted roll source.
        /// </param>
        /// <param name="penalty">The cost of a Death. <c>default</c> (<see cref="RunPenalty.None"/>) costs nothing extra.</param>
        public RunState(Func<EncounterProfile, EncounterSimulation> startEncounter, RunPenalty penalty = default)
        {
            _startEncounter = startEncounter ?? throw new ArgumentNullException(nameof(startEncounter));
            _penalty = penalty;
            _onHeroDowned = OnHeroDowned;
        }

        // ─── state ──────────────────────────────────────────────────────────

        /// <summary>Which state the Run is in.</summary>
        public RunPhase Phase { get; private set; } = RunPhase.InTown;

        /// <summary>The Location the hero is currently in the Field at — <c>null</c> while <see cref="RunPhase.InTown"/>.</summary>
        public EncounterProfile Location => _location;

        /// <summary>The live Encounter simulation — <c>null</c> while <see cref="RunPhase.InTown"/>.</summary>
        public EncounterSimulation Encounter => _encounter;

        /// <summary>
        /// Currency banked to the Wallet this Run so far, in base units (<see cref="BankCurrency"/>).
        /// Zero while <see cref="RunPhase.InTown"/>; the Death fee is computed against it.
        /// </summary>
        public long CurrencyBanked => _currencyBanked;

        /// <summary>
        /// Whether the hero has been downed in the current Run. The sim has ended and the only
        /// way out is <see cref="HandleDeath"/> — <see cref="Recall"/> is refused. <c>false</c>
        /// while <see cref="RunPhase.InTown"/>.
        /// </summary>
        public bool HeroIsDown => _heroDown;

        /// <summary>The result of the most recently finished Run, or <c>null</c> if none has finished yet.</summary>
        public RunResult? LastResult { get; private set; }

        // ─── transitions ────────────────────────────────────────────────────

        /// <summary>
        /// Start a Run at <paramref name="location"/> — <see cref="RunPhase.InTown"/> →
        /// <see cref="RunPhase.InField"/>. Builds a fresh Encounter and zeroes the Run totals.
        /// </summary>
        public void Send(EncounterProfile location)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (Phase != RunPhase.InTown)
                throw new InvalidOperationException("A Run is already in the Field — Recall or HandleDeath before Sending again.");

            var encounter = _startEncounter(location)
                ?? throw new InvalidOperationException("The encounter factory returned null.");

            _location = location;
            _encounter = encounter;
            _encounter.HeroDowned += _onHeroDowned;
            ResetRunTotals();

            Phase = RunPhase.InField;
        }

        /// <summary>
        /// Bank <paramref name="deltaSeconds"/> of real time on the Encounter and run every whole
        /// tick now due. Forwards to <see cref="EncounterSimulation.Advance"/> (already
        /// sim-speed-scaled by the caller); returns the tick count. A no-op in Town.
        /// </summary>
        public int Advance(float deltaSeconds) =>
            Phase == RunPhase.InField ? _encounter.Advance(deltaSeconds) : 0;

        /// <summary>
        /// Record <paramref name="baseUnits"/> of currency banked to the Wallet by a kill's loot
        /// (issue #24). Accumulates into <see cref="CurrencyBanked"/>; only valid while a Run is
        /// live and the hero is still up — once it is down the Run's take is frozen for
        /// <see cref="HandleDeath"/> to read.
        /// </summary>
        public void BankCurrency(long baseUnits)
        {
            if (Phase != RunPhase.InField)
                throw new InvalidOperationException("Currency only banks during a Run.");
            if (_heroDown)
                throw new InvalidOperationException("The hero is down — no more currency banks before HandleDeath closes the Run.");
            if (baseUnits < 0L)
                throw new ArgumentOutOfRangeException(nameof(baseUnits), baseUnits, "Cannot bank a negative amount.");

            _currencyBanked += baseUnits;
        }

        /// <summary>
        /// End the Run with everything kept — <see cref="RunPhase.InField"/> →
        /// <see cref="RunPhase.InTown"/>, freezing and returning a <see cref="RunOutcome.Recalled"/>
        /// result. Refused once the hero is down: that Run ends in <see cref="HandleDeath"/>.
        /// </summary>
        public RunResult Recall()
        {
            RequireInField();
            if (_heroDown)
                throw new InvalidOperationException("The hero is down — the Run ends in HandleDeath, not a Recall.");

            var result = RunResult.Recalled(FreezeEncounter(), _currencyBanked);
            EndRun(result);
            return result;
        }

        /// <summary>
        /// End the Run in Death — <see cref="RunPhase.InField"/> → <see cref="RunPhase.InTown"/>,
        /// freezing and returning a <see cref="RunOutcome.Died"/> result. Refused unless the hero
        /// is actually down (<see cref="HeroIsDown"/>): that guard is what stops a driver bug from
        /// imposing the Death penalty on a Run that is still winnable — a live hero's Run only
        /// ever ends in <see cref="Recall"/>.
        ///
        /// On top of the kept accumulation it computes the penalty: <see cref="RunResult.XpLost"/>
        /// is <see cref="RunPenalty.XpLossFraction"/> of <paramref name="xpTowardNextLevel"/>,
        /// <see cref="RunResult.CurrencyFee"/> is <see cref="RunPenalty.CurrencyFeeFraction"/> of
        /// <see cref="CurrencyBanked"/>. The bag → Corpse hand-off is issue #22.
        /// </summary>
        /// <param name="xpTowardNextLevel">The hero's current progress toward its next level, in XP.</param>
        public RunResult HandleDeath(int xpTowardNextLevel)
        {
            RequireInField();
            if (!_heroDown)
                throw new InvalidOperationException("The hero is not down — the Run ends in Recall, not HandleDeath.");
            if (xpTowardNextLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(xpTowardNextLevel), xpTowardNextLevel, "Progress toward the next level cannot be negative.");

            var totals = FreezeEncounter();

            // RunPenalty's fractions are validated to [0, 1] and both inputs here are already
            // non-negative (the guard above, BankCurrency's own guard), so the product can only
            // land in [0, its input] — no further clamping needed.
            var xpLost = (int)Math.Round((double)_penalty.XpLossFraction * xpTowardNextLevel, MidpointRounding.AwayFromZero);
            var currencyFee = (long)Math.Round((double)_penalty.CurrencyFeeFraction * _currencyBanked, MidpointRounding.AwayFromZero);

            var result = RunResult.Died(totals, _currencyBanked, xpLost, currencyFee);
            EndRun(result);
            return result;
        }

        // ─── internals ──────────────────────────────────────────────────────

        private void OnHeroDowned() => _heroDown = true;

        private void RequireInField()
        {
            if (Phase != RunPhase.InField)
                throw new InvalidOperationException("No Run is in the Field.");
        }

        /// <summary>Stop the fight (idempotent) and read its running totals out.</summary>
        private EncounterTotals FreezeEncounter()
        {
            _encounter.Abandon(); // no-op if the hero going down already ended it
            return new EncounterTotals(
                _encounter.SettledXp, _encounter.ForfeitedXp, _encounter.EnemiesDefeated,
                _encounter.EncountersCleared, _encounter.Duration);
        }

        private void ResetRunTotals()
        {
            _currencyBanked = 0L;
            _heroDown = false;
        }

        private void EndRun(RunResult result)
        {
            _encounter.HeroDowned -= _onHeroDowned;
            _encounter = null;
            _location = null;
            ResetRunTotals();
            Phase = RunPhase.InTown;
            LastResult = result;
        }
    }
}
