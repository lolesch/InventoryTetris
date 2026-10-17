using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The ADR-0009 <i>Death is corpse-recovery, not haul-forfeit</i> rules, in one engine-free
    /// place. <see cref="Settle"/> runs the Death penalty - charge the currency fee, bury everything
    /// the hero wore and carried (coins included) as the one <see cref="Corpse"/> tagged with the
    /// fall Location, forfeit the XP. <see cref="Recover"/> is the other half: re-entering the
    /// Corpse's own Location puts it all back where it lay, and what does not fit stays on the
    /// Corpse, re-buried at the same Location.
    ///
    /// Everything Unity-typed - the live bag, the Wallet, the character sheet - sits behind
    /// <see cref="ISettlementBag"/> and <see cref="ISettlementLedger"/>, so the rules are
    /// verified through this interface against in-memory ports. The engine side (<c>SimulationService</c>) binds the ports and owns
    /// nothing else about Death.
    /// </summary>
    public sealed class RunSettlement
    {
        private readonly ISettlementBag _bag;
        private readonly ISettlementLedger _ledger;
        private readonly Corpse _corpse;

        public RunSettlement(ISettlementBag bag, ISettlementLedger ledger)
            : this(bag, ledger, new Corpse()) { }

        /// <summary>
        /// Settles onto <paramref name="corpse"/>, which belongs to whoever owns the hero (the Hero
        /// itself, in the game): the rules are stateless over it, so one can be built for each use.
        /// </summary>
        public RunSettlement(ISettlementBag bag, ISettlementLedger ledger, Corpse corpse)
        {
            _bag = bag ?? throw new ArgumentNullException(nameof(bag));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _corpse = corpse ?? throw new ArgumentNullException(nameof(corpse));
        }

        /// <summary>The one standing Corpse - exposed for the debug panel's read-out, mutated only in here.</summary>
        public Corpse Corpse => _corpse;

        /// <summary>
        /// Apply the Death penalty for <paramref name="result"/> at <paramref name="location"/>
        /// (the memoized <see cref="EncounterProfile"/> for the fall Location, or <c>null</c> when
        /// none was selected). A no-op unless <paramref name="result"/> is a
        /// <see cref="RunOutcome.Died"/>. Order matters: the fee is taken before the hero is emptied
        /// into the Corpse. The hero is left down and bare: the revive is Respawn's.
        /// With no <paramref name="location"/> the fee is still charged but nothing is buried and
        /// no XP is forfeited - there is no Location to recover a Corpse from.
        /// </summary>
        public void Settle(RunResult result, EncounterProfile location)
        {
            if (result.Outcome != RunOutcome.Died)
                return;

            // The fee comes out of the wallet first, so the Corpse carries what is left of the coins.
            if (result.CurrencyFee > 0L)
                _ledger.ChargeFee(result.CurrencyFee);

            // With no Location there is no Corpse to recover, so the hero is not stripped either.
            if (location != null)
                _corpse.Bury(location, _bag.TakeEverything(), result.FallPosition);

            if (result.XpLost > 0 && location != null)
                _ledger.ForfeitXp(result.XpLost);
        }

        /// <summary>
        /// Re-entering <paramref name="location"/>: if the standing Corpse is tagged with it, lay
        /// its contents back to where they lay, and whatever does not fit is re-buried at the same
        /// Location for a later recovery - never put on the ground. A no-op with no Corpse or a
        /// Location that is not the Corpse's.
        /// </summary>
        public void Recover(EncounterProfile location)
        {
            // Read before the recovery clears it: the leftovers lie where the hero fell.
            var fell = _corpse.FallPosition;

            if (!_corpse.TryRecover(location, out var drops))
                return;

            var stranded = new List<CorpseEntry>();
            foreach (var entry in drops)
            {
                var left = _bag.Restore(entry);
                if (left > 0u)
                    stranded.Add(entry.WithAmount(left));
            }

            if (stranded.Count > 0)
                _corpse.Bury(location, stranded, fell);
        }
    }
}
