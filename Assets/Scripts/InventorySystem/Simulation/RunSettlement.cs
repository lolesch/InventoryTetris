using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The ADR-0009 <i>Death is corpse-recovery, not haul-forfeit</i> rules, in one engine-free
    /// place. <see cref="Settle"/> runs the Death penalty - bury the bag's non-currency contents
    /// as the one <see cref="Corpse"/> tagged with the fall Location, charge the currency fee,
    /// forfeit the XP, revive the hero. <see cref="Recover"/> is the other half: re-entering the
    /// Corpse's own Location lays it back out into the bag, and what does not fit stays on the
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
        /// <see cref="RunOutcome.Died"/>. Order matters: the bag is emptied into the Corpse before
        /// the fee and XP are taken, and the revive is last so the hero is left full and Sendable.
        /// With no <paramref name="location"/> the fee is still charged but nothing is buried and
        /// no XP is forfeited - there is no Location to recover a Corpse from.
        /// </summary>
        public void Settle(RunResult result, EncounterProfile location)
        {
            if (result.Outcome != RunOutcome.Died)
                return;

            var contents = _bag.TakeNonCurrencyContents();

            if (location != null)
                _corpse.Bury(location, contents);

            if (result.CurrencyFee > 0L)
                _ledger.ChargeFee(result.CurrencyFee);

            if (result.XpLost > 0 && location != null)
                _ledger.ForfeitXp(result.XpLost);

            _ledger.ReviveIfDown();
        }

        /// <summary>
        /// Re-entering <paramref name="location"/>: if the standing Corpse is tagged with it, lay
        /// its contents back out into the bag, and whatever does not fit is re-buried at the same
        /// Location for a later recovery - never put on the ground. A no-op with no Corpse or a
        /// Location that is not the Corpse's.
        /// </summary>
        public void Recover(EncounterProfile location)
        {
            if (!_corpse.TryRecover(location, out var drops))
                return;

            var stranded = new List<ItemInstance>();
            foreach (var item in drops)
            {
                if (!_bag.TryStore(item))
                    stranded.Add(item);
            }

            if (stranded.Count > 0)
                _corpse.Bury(location, stranded);
        }
    }
}
