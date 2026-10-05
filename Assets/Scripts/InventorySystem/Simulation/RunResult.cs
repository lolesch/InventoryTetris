namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The frozen readout of one finished Run (issue #21; spec <i>RunResult / RunOutcome</i>).
    /// It is a <b>summary</b>, not a delivery mechanism — most of what a Run earns is applied
    /// live: settled XP goes to the hero on each Encounter clear, coins bank to the Wallet per
    /// kill, items land in the bag on pickup. The one thing the result <em>delivers</em> is the
    /// Death case — <see cref="XpLost"/>, <see cref="CurrencyFee"/>, and (issue #22) the Corpse.
    ///
    /// Currency figures are in <b>base units</b> (iron-equivalent, the <c>GLOSSARY.md</c> <i>Base
    /// Unit</i> scale — the same total <c>Currency.Total</c> reports). The engine-side adapter
    /// (issue #26) maps them back to a <c>Currency</c> when it applies the fee to the Wallet;
    /// keeping this struct on a plain integer keeps the whole module engine-free.
    /// </summary>
    public readonly struct RunResult
    {
        private RunResult(
            RunOutcome outcome,
            int xpSettled,
            long currencyBanked,
            int xpLost,
            long currencyFee,
            int enemiesDefeated,
            int encountersCleared,
            float duration)
        {
            Outcome = outcome;
            XpSettled = xpSettled;
            CurrencyBanked = currencyBanked;
            XpLost = xpLost;
            CurrencyFee = currencyFee;
            EnemiesDefeated = enemiesDefeated;
            EncountersCleared = encountersCleared;
            Duration = duration;
        }

        /// <summary>Which way the Run ended.</summary>
        public RunOutcome Outcome { get; }

        /// <summary>XP delivered per kill this Run, summed. Already applied to the hero, live — nothing is held back to forfeit on an exit.</summary>
        public int XpSettled { get; }

        /// <summary>Currency banked to the Wallet this Run (base units), summed over every kill.</summary>
        public long CurrencyBanked { get; }

        /// <summary>
        /// <see cref="RunOutcome.Died"/> only — the hero's progress toward the next level that
        /// the Death penalty forfeits (<see cref="RunPenalty.XpLossFraction"/> of it). Zero on a
        /// Recall. The adapter (issue #26) subtracts this from the hero's XP.
        /// </summary>
        public int XpLost { get; }

        /// <summary>
        /// <see cref="RunOutcome.Died"/> only — currency withdrawn from the Wallet as the Death
        /// fee (<see cref="RunPenalty.CurrencyFeeFraction"/> of <see cref="CurrencyBanked"/>, base
        /// units, never more than was banked). Zero on a Recall. A separate line — it does not
        /// reduce <see cref="CurrencyBanked"/>.
        /// </summary>
        public long CurrencyFee { get; }

        /// <summary>Every enemy that fell this Run, across every Encounter.</summary>
        public int EnemiesDefeated { get; }

        /// <summary>Encounters cleared this Run — equivalently, the number of XP settlements.</summary>
        public int EncountersCleared { get; }

        /// <summary>Tick-quantised simulation time this Run lasted (<c>CombatClock.ElapsedTime</c>), summed.</summary>
        public float Duration { get; }

        /// <summary>Freeze a <see cref="RunOutcome.Recalled"/> result — the full accumulation, no penalty.</summary>
        internal static RunResult Recalled(EncounterTotals totals, long currencyBanked) =>
            new(RunOutcome.Recalled, totals.XpSettled, currencyBanked, 0, 0L,
                totals.EnemiesDefeated, totals.EncountersCleared, totals.Duration);

        /// <summary>Freeze a <see cref="RunOutcome.Died"/> result — the accumulation plus the penalty.</summary>
        internal static RunResult Died(EncounterTotals totals, long currencyBanked, int xpLost, long currencyFee) =>
            new(RunOutcome.Died, totals.XpSettled, currencyBanked, xpLost, currencyFee,
                totals.EnemiesDefeated, totals.EncountersCleared, totals.Duration);
    }

    /// <summary>
    /// The Encounter-sourced half of a <see cref="RunResult"/> — everything <see cref="RunState"/>
    /// reads off the live <see cref="EncounterSimulation"/> when a Run ends, before the Death
    /// penalty (computed separately, against <see cref="RunState.CurrencyBanked"/> and the hero's
    /// XP progress) is added. One value instead of five positional parameters, so the two
    /// <see cref="RunResult"/> factories can't have them transposed at the call site. Totals
    /// from the Encounters a Run left behind on a Relocate are added in with <see cref="Plus"/>.
    /// </summary>
    internal readonly struct EncounterTotals
    {
        public EncounterTotals(int xpSettled, int enemiesDefeated, int encountersCleared, float duration)
        {
            XpSettled = xpSettled;
            EnemiesDefeated = enemiesDefeated;
            EncountersCleared = encountersCleared;
            Duration = duration;
        }

        public int XpSettled { get; }
        public int EnemiesDefeated { get; }
        public int EncountersCleared { get; }
        public float Duration { get; }

        public EncounterTotals Plus(EncounterTotals other) => new(
            XpSettled + other.XpSettled,
            EnemiesDefeated + other.EnemiesDefeated,
            EncountersCleared + other.EncountersCleared,
            Duration + other.Duration);
    }
}
