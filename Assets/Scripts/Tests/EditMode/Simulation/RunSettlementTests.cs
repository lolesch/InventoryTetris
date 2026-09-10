using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The ADR-0009 <i>Death is corpse-recovery, not haul-forfeit</i> rules, verified through
    /// <see cref="RunSettlement"/>'s interface (finding #2 of the 2026-09-08 architecture
    /// review — these rules had zero coverage while they lived in <c>SimulationProvider</c>).
    /// A Death buries the bag's non-currency contents as the one Location-tagged Corpse, charges
    /// the currency fee, forfeits the XP and revives the hero; re-entering that Location lays the
    /// Corpse back out, to the bag where it fits and the ground where it does not.
    /// </summary>
    [TestFixture]
    public sealed class RunSettlementTests
    {
        private static ItemInstance Item(string id) => new(id, ItemRarity.Common, 1, null);

        private static EncounterProfile Thornwood() => Profiles.Solo(EnemyArchetype.Skirmisher);
        private static EncounterProfile Ashfen() => Profiles.Solo(EnemyArchetype.Brute);

        private static RunResult Death(long currencyFee = 0L, int xpLost = 0) =>
            RunResult.Died(new EncounterTotals(0, 0, 0, 0, 0f), currencyBanked: 0L, xpLost: xpLost, currencyFee: currencyFee);

        private static RunResult Recall() =>
            RunResult.Recalled(new EncounterTotals(0, 0, 0, 0, 0f), currencyBanked: 0L);

        // ─── Settle: only on a Death ─────────────────────────────────────────

        [Test]
        public void Settle_OnARecall_DoesNothing()
        {
            var bag = new InMemorySettlementBag(Item("sword"));
            var ledger = new RecordingSettlementLedger();
            var settlement = new RunSettlement(bag, ledger);

            settlement.Settle(Recall(), Thornwood());

            Assert.That(settlement.Corpse.Exists, Is.False);
            Assert.That(ledger.FeeCharged, Is.Null);
            Assert.That(ledger.XpForfeited, Is.Null);
            Assert.That(ledger.ReviveCalls, Is.Zero);
        }

        // ─── Settle: the Corpse ─────────────────────────────────────────────

        [Test]
        public void Settle_OnADeath_BuriesTheBagContents_TaggedWithTheLocation()
        {
            var bag = new InMemorySettlementBag(Item("sword"), Item("potion"));
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();

            settlement.Settle(Death(), thornwood);

            Assert.That(settlement.Corpse.Exists, Is.True);
            Assert.That(settlement.Corpse.Location, Is.SameAs(thornwood));
            Assert.That(settlement.Corpse.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void Settle_WithNoLocation_ChargesTheFeeButBuriesNothing()
        {
            var bag = new InMemorySettlementBag(Item("sword"));
            var ledger = new RecordingSettlementLedger();
            var settlement = new RunSettlement(bag, ledger);

            settlement.Settle(Death(currencyFee: 40L, xpLost: 10), location: null);

            Assert.That(settlement.Corpse.Exists, Is.False);
            Assert.That(ledger.FeeCharged, Is.EqualTo(40L));
            Assert.That(ledger.XpForfeited, Is.Null, "no Location to recover a Corpse from — no XP is forfeited either");
        }

        // ─── Settle: the ledger ─────────────────────────────────────────────

        [Test]
        public void Settle_ChargesTheFeeAndForfeitsTheXp()
        {
            var ledger = new RecordingSettlementLedger();
            var settlement = new RunSettlement(new InMemorySettlementBag(), ledger);

            settlement.Settle(Death(currencyFee: 75L, xpLost: 30), Thornwood());

            Assert.That(ledger.FeeCharged, Is.EqualTo(75L));
            Assert.That(ledger.XpForfeited, Is.EqualTo(30));
        }

        [Test]
        public void Settle_WithNoFeeAndNoXpLoss_SkipsThoseCalls()
        {
            var ledger = new RecordingSettlementLedger();
            var settlement = new RunSettlement(new InMemorySettlementBag(), ledger);

            settlement.Settle(Death(currencyFee: 0L, xpLost: 0), Thornwood());

            Assert.That(ledger.FeeCharged, Is.Null);
            Assert.That(ledger.XpForfeited, Is.Null);
        }

        [Test]
        public void Settle_AlwaysRevivesTheHero()
        {
            var ledger = new RecordingSettlementLedger();
            var settlement = new RunSettlement(new InMemorySettlementBag(), ledger);

            settlement.Settle(Death(), Thornwood());

            Assert.That(ledger.ReviveCalls, Is.EqualTo(1));
        }

        // ─── Recover ───────────────────────────────────────────────────────

        [Test]
        public void Recover_AtAnotherLocation_LeavesTheCorpseUntouched()
        {
            var settlement = new RunSettlement(new InMemorySettlementBag(Item("sword")), new RecordingSettlementLedger());
            settlement.Settle(Death(), Thornwood());

            settlement.Recover(Ashfen(), new RecordingLootGround());

            Assert.That(settlement.Corpse.Exists, Is.True);
        }

        [Test]
        public void Recover_AtTheCorpsesLocation_StoresWhatFits_AndGroundsTheRest()
        {
            var bag = new InMemorySettlementBag(Item("a"), Item("b"), Item("c")) { Capacity = 0 };
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();
            settlement.Settle(Death(), thornwood);
            bag.Capacity = 2; // room for two of the three recovered items

            var ground = new RecordingLootGround();
            settlement.Recover(thornwood, ground);

            Assert.That(bag.Stored, Has.Count.EqualTo(2));
            Assert.That(ground.Placed, Has.Count.EqualTo(1));
            Assert.That(settlement.Corpse.Exists, Is.False, "a recovery with a ground clears the Corpse");
        }

        [Test]
        public void Recover_WithNoGround_ReBuriesTheOverflowAtTheSameLocation()
        {
            var bag = new InMemorySettlementBag(Item("a"), Item("b")) { Capacity = 0 };
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();
            settlement.Settle(Death(), thornwood);
            bag.Capacity = 1; // one of the two fits

            settlement.Recover(thornwood, ground: null);

            Assert.That(bag.Stored, Has.Count.EqualTo(1));
            Assert.That(settlement.Corpse.Exists, Is.True, "the item that did not fit stays recoverable");
            Assert.That(settlement.Corpse.Location, Is.SameAs(thornwood));
            Assert.That(settlement.Corpse.Items.Count, Is.EqualTo(1));
        }
    }
}
