using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The ADR-0009 <i>Death is corpse-recovery, not haul-forfeit</i> rules, verified through
    /// <see cref="RunSettlement"/>'s interface (finding #2 of the 2026-09-08 architecture
    /// review — these rules had zero coverage while they lived in the scene's simulation component).
    /// A Death buries the bag's non-currency contents as the one Location-tagged Corpse, charges
    /// the currency fee and forfeits the XP (the revive is Respawn's); re-entering that Location lays the
    /// Corpse back out, to the bag where it fits and re-burying what does not.
    /// </summary>
    [TestFixture]
    public sealed class RunSettlementTests
    {
        private static ItemInstance Item(string id) => new(id, ItemRarity.Common, 1, null);

        private static EncounterProfile Thornwood() => Profiles.Solo(EnemyArchetype.Skirmisher);
        private static EncounterProfile Ashfen() => Profiles.Solo(EnemyArchetype.Brute);

        private static RunResult Death(long currencyFee = 0L, int xpLost = 0, Coordinate fell = default) =>
            RunResult.Died(new EncounterTotals(0, 0, 0, 0f), currencyBanked: 0L, xpLost: xpLost, currencyFee: currencyFee, fell: fell);

        private static RunResult Return() =>
            RunResult.Returned(new EncounterTotals(0, 0, 0, 0f), currencyBanked: 0L);

        // ─── Settle: only on a Death ─────────────────────────────────────────

        [Test]
        public void Settle_OnAReturn_DoesNothing()
        {
            var bag = new InMemorySettlementBag(Item("sword"));
            var ledger = new RecordingSettlementLedger();
            var settlement = new RunSettlement(bag, ledger);

            settlement.Settle(Return(), Thornwood());

            Assert.That(settlement.Corpse.Exists, Is.False);
            Assert.That(ledger.FeeCharged, Is.Null);
            Assert.That(ledger.XpForfeited, Is.Null);
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
        public void Settle_OnADeath_BuriesTheCorpseWhereTheHeroFell()
        {
            var settlement = new RunSettlement(new InMemorySettlementBag(Item("sword")), new RecordingSettlementLedger());

            settlement.Settle(Death(fell: new Coordinate(4f, -6f)), Thornwood());

            Assert.That(settlement.Corpse.FallPosition, Is.EqualTo(new Coordinate(4f, -6f)));
        }

        [Test]
        public void Recover_WithALeftover_ReBuriesItWhereTheHeroFell()
        {
            var bag = new InMemorySettlementBag(Item("a"), Item("b")) { Capacity = 0 };
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();
            settlement.Settle(Death(fell: new Coordinate(4f, -6f)), thornwood);

            settlement.Recover(thornwood);

            Assert.That(settlement.Corpse.Exists, Is.True);
            Assert.That(settlement.Corpse.FallPosition, Is.EqualTo(new Coordinate(4f, -6f)));
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

        // ─── Recover ───────────────────────────────────────────────────────

        [Test]
        public void Recover_AtAnotherLocation_LeavesTheCorpseUntouched()
        {
            var settlement = new RunSettlement(new InMemorySettlementBag(Item("sword")), new RecordingSettlementLedger());
            settlement.Settle(Death(), Thornwood());

            settlement.Recover(Ashfen());

            Assert.That(settlement.Corpse.Exists, Is.True);
        }

        // What does not fit stays on the Corpse, re-buried at the same Location (ADR-0009); the
        // ground is not part of a recovery at all, so no port to it is handed in.

        [Test]
        public void Recover_WithRoomForEverything_StoresItAll_AndClearsTheCorpse()
        {
            var bag = new InMemorySettlementBag(Item("a"), Item("b")) { Capacity = 0 };
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();
            settlement.Settle(Death(), thornwood);
            bag.Capacity = null;

            settlement.Recover(thornwood);

            Assert.That(bag.Stored, Has.Count.EqualTo(2));
            Assert.That(settlement.Corpse.Exists, Is.False, "nothing was left over, so nothing stays buried");
        }

        [Test]
        public void Recover_WithAPartialFit_StoresWhatFits_AndReBuriesTheRestAtTheSameLocation()
        {
            var a = Item("a");
            var b = Item("b");
            var c = Item("c");
            var bag = new InMemorySettlementBag(a, b, c) { Capacity = 0 };
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();
            settlement.Settle(Death(), thornwood);
            bag.Capacity = 2; // room for two of the three recovered items

            settlement.Recover(thornwood);

            Assert.That(bag.Stored, Is.EqualTo(new[] { a, b }));
            Assert.That(settlement.Corpse.Exists, Is.True, "the item that did not fit stays recoverable");
            Assert.That(settlement.Corpse.Location, Is.SameAs(thornwood));
            Assert.That(settlement.Corpse.Items, Is.EqualTo(new[] { c }));
        }

        [Test]
        public void Recover_WithAFullBag_LeavesTheWholeCorpseWhereItLay()
        {
            var a = Item("a");
            var b = Item("b");
            var bag = new InMemorySettlementBag(a, b) { Capacity = 0 };
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();
            settlement.Settle(Death(), thornwood);

            settlement.Recover(thornwood);

            Assert.That(bag.Stored, Is.Empty);
            Assert.That(settlement.Corpse.Location, Is.SameAs(thornwood));
            Assert.That(settlement.Corpse.Items, Is.EqualTo(new[] { a, b }));
        }

        [Test]
        public void Recover_HandsEachEntryBackWithItsPlace_AndAStackKeepsItsAmount()
        {
            var sword = new CorpseEntry(Item("sword"), 1u, CorpseSource.Equipment, 3, 0);
            var coins = new CorpseEntry(Item("coins"), 40u, CorpseSource.Bag, 0, 2);
            var bag = new InMemorySettlementBag(new[] { sword, coins });
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();
            settlement.Settle(Death(), thornwood);

            settlement.Recover(thornwood);

            Assert.That(bag.Offered, Is.EqualTo(new[] { sword, coins }));
            Assert.That(settlement.Corpse.Exists, Is.False);
        }

        [Test]
        public void Recover_WithAStackThatPartlyFits_ReBuriesTheRestAtTheSamePlace()
        {
            var coins = new CorpseEntry(Item("coins"), 40u, CorpseSource.Bag, 0, 2);
            var bag = new InMemorySettlementBag(new[] { coins }) { Capacity = 0 };
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();
            settlement.Settle(Death(), thornwood);
            bag.Capacity = 15;

            settlement.Recover(thornwood);

            var left = settlement.Corpse.Entries.Single();
            Assert.That(left.Amount, Is.EqualTo(25u));
            Assert.That((left.Source, left.X, left.Y), Is.EqualTo((CorpseSource.Bag, 0, 2)));
        }

        [Test]
        public void Settle_TakesTheFeeBeforeTheHeroIsEmptied_SoTheCorpseCarriesWhatIsLeft()
        {
            var order = new List<string>();
            var bag = new OrderRecordingBag(order);
            var ledger = new OrderRecordingLedger(order);
            var settlement = new RunSettlement(bag, ledger);

            settlement.Settle(Death(currencyFee: 10L), Thornwood());

            Assert.That(order, Is.EqualTo(new[] { "fee", "take" }));
        }

        [Test]
        public void Settle_WithNoLocation_LeavesTheHeroDressed()
        {
            var order = new List<string>();
            var settlement = new RunSettlement(new OrderRecordingBag(order), new RecordingSettlementLedger());

            settlement.Settle(Death(), location: null);

            Assert.That(order, Is.Empty, "nothing to recover from, so nothing is taken");
        }

        private sealed class OrderRecordingBag : ISettlementBag
        {
            private readonly List<string> _order;
            public OrderRecordingBag(List<string> order) => _order = order;

            public IReadOnlyList<CorpseEntry> TakeEverything()
            {
                _order.Add("take");
                return Array.Empty<CorpseEntry>();
            }

            public uint Restore(CorpseEntry entry) => 0u;
        }

        private sealed class OrderRecordingLedger : ISettlementLedger
        {
            private readonly List<string> _order;
            public OrderRecordingLedger(List<string> order) => _order = order;

            public void ChargeFee(long baseUnits) => _order.Add("fee");
            public void ForfeitXp(int xp) { }
        }

        [Test]
        public void Recover_OfAnEmptyCorpse_StoresNothing_AndClearsIt()
        {
            var bag = new InMemorySettlementBag();
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();
            settlement.Settle(Death(), thornwood);

            settlement.Recover(thornwood);

            Assert.That(bag.Stored, Is.Empty);
            Assert.That(settlement.Corpse.Exists, Is.False);
        }

        [Test]
        public void Recover_AfterALeftoverReBurial_TakesTheRestOnTheNextVisit()
        {
            var a = Item("a");
            var b = Item("b");
            var bag = new InMemorySettlementBag(a, b) { Capacity = 0 };
            var settlement = new RunSettlement(bag, new RecordingSettlementLedger());
            var thornwood = Thornwood();
            settlement.Settle(Death(), thornwood);
            bag.Capacity = 1;
            settlement.Recover(thornwood);

            bag.Capacity = null; // room made between visits
            settlement.Recover(thornwood);

            Assert.That(bag.Stored, Is.EqualTo(new[] { a, b }));
            Assert.That(settlement.Corpse.Exists, Is.False);
        }
    }
}
