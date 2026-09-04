using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// <see cref="HeroBehaviour"/> (issue #23) — the six slider-fed values and their pure
    /// triggers: retreat and bag-fill fire an auto-Recall signal at their configured fraction;
    /// <c>CastThreshold</c> gates the <i>start</i> of a casting run by resource hysteresis
    /// (ADR-0010) and releases only once the pool is spent to empty; the loot filter admits
    /// items and coin denominations by minimum <see cref="ItemRarity"/> (CONTEXT.md's
    /// iron-Common … gold-Unique ladder). <c>Engagement</c> and <c>SimSpeed</c> are read live by
    /// the sim / clock adapter (issue #26) — plain storage here, nothing to assert.
    /// </summary>
    [TestFixture]
    public sealed class HeroBehaviourTests
    {
        // ─── retreat ────────────────────────────────────────────────────────

        [Test]
        public void RetreatTrigger_AboveTheThreshold_DoesNotFire()
        {
            var behaviour = new HeroBehaviour { RetreatHealthFraction = 0.3f };

            Assert.That(behaviour.ShouldRecallForHealth(0.31f), Is.False);
        }

        [Test]
        public void RetreatTrigger_ExactlyAtTheThreshold_Fires()
        {
            var behaviour = new HeroBehaviour { RetreatHealthFraction = 0.3f };

            Assert.That(behaviour.ShouldRecallForHealth(0.3f), Is.True);
        }

        [Test]
        public void RetreatTrigger_BelowTheThreshold_Fires()
        {
            var behaviour = new HeroBehaviour { RetreatHealthFraction = 0.3f };

            Assert.That(behaviour.ShouldRecallForHealth(0.1f), Is.True);
        }

        // ─── bag-fill ───────────────────────────────────────────────────────

        [Test]
        public void BagFillTrigger_BelowTheThreshold_DoesNotFire()
        {
            var behaviour = new HeroBehaviour { RecallBagFillFraction = 0.9f };

            Assert.That(behaviour.ShouldRecallForBagFull(0.89f), Is.False);
        }

        [Test]
        public void BagFillTrigger_ExactlyAtTheThreshold_Fires()
        {
            var behaviour = new HeroBehaviour { RecallBagFillFraction = 0.9f };

            Assert.That(behaviour.ShouldRecallForBagFull(0.9f), Is.True);
        }

        [Test]
        public void BagFillTrigger_AboveTheThreshold_Fires()
        {
            var behaviour = new HeroBehaviour { RecallBagFillFraction = 0.9f };

            Assert.That(behaviour.ShouldRecallForBagFull(1f), Is.True);
        }

        // ─── CastThreshold hysteresis ──────────────────────────────────────

        [Test]
        public void CastGate_BelowThresholdAndNotYetStarted_DoesNotCast()
        {
            var behaviour = new HeroBehaviour { CastThreshold = 0.5f };

            Assert.That(behaviour.ShouldCast(0.49f), Is.False);
        }

        [Test]
        public void CastGate_ReachingTheThreshold_StartsTheRun()
        {
            var behaviour = new HeroBehaviour { CastThreshold = 0.5f };

            Assert.That(behaviour.ShouldCast(0.5f), Is.True);
        }

        [Test]
        public void CastGate_OnceStarted_KeepsCastingBelowTheThreshold()
        {
            var behaviour = new HeroBehaviour { CastThreshold = 0.5f };

            behaviour.ShouldCast(0.5f); // starts the run

            Assert.That(behaviour.ShouldCast(0.2f), Is.True, "a started run keeps Casting down through the threshold");
        }

        [Test]
        public void CastGate_SpentToEmpty_ReleasesTheRun()
        {
            var behaviour = new HeroBehaviour { CastThreshold = 0.5f };

            behaviour.ShouldCast(0.5f);  // starts
            behaviour.ShouldCast(0.05f); // still running, low but not empty

            Assert.That(behaviour.ShouldCast(0f), Is.False, "empty releases the run");
        }

        [Test]
        public void CastGate_SpentToNearEmpty_StillReleases()
        {
            var behaviour = new HeroBehaviour { CastThreshold = 0.5f };

            behaviour.ShouldCast(0.5f); // starts

            // The live sim regenerates every tick before the next affordability check, so an
            // exact 0f is never actually observed once a run is under way — the release floor
            // has to tolerate a near-empty remainder, not just a bit-exact zero.
            Assert.That(behaviour.ShouldCast(0.01f), Is.False, "a near-empty remainder still releases the run");
        }

        [Test]
        public void CastGate_AfterReleaseBelowThreshold_StaysReleased()
        {
            var behaviour = new HeroBehaviour { CastThreshold = 0.5f };

            behaviour.ShouldCast(0.5f); // starts
            behaviour.ShouldCast(0f);   // releases

            // regen climbs back partway, still below the threshold — no restart yet
            Assert.That(behaviour.ShouldCast(0.4f), Is.False);
        }

        [Test]
        public void CastGate_AfterReleaseReachingThresholdAgain_Restarts()
        {
            var behaviour = new HeroBehaviour { CastThreshold = 0.5f };

            behaviour.ShouldCast(0.5f); // starts
            behaviour.ShouldCast(0f);   // releases

            Assert.That(behaviour.ShouldCast(0.5f), Is.True, "recharging back to the threshold restarts the run");
        }

        [Test]
        public void CastGate_NeverStarted_StaysFalseEvenAtEmpty()
        {
            var behaviour = new HeroBehaviour { CastThreshold = 0.5f };

            Assert.That(behaviour.ShouldCast(0f), Is.False);
        }

        // ─── loot filter ────────────────────────────────────────────────────

        [TestCase(ItemRarity.Common, ItemRarity.Common, true)]
        [TestCase(ItemRarity.Magic, ItemRarity.Common, true)]
        [TestCase(ItemRarity.Common, ItemRarity.Magic, false)]
        [TestCase(ItemRarity.NoDrop, ItemRarity.Common, false)]
        [TestCase(ItemRarity.Unique, ItemRarity.Unique, true)]
        public void LootFilter_AdmitsItemsAtOrAboveTheMinimum(ItemRarity rarity, ItemRarity minimum, bool admitted)
        {
            var behaviour = new HeroBehaviour { LootFilterMinimum = minimum };

            Assert.That(behaviour.AdmitsItem(rarity), Is.EqualTo(admitted));
        }

        [TestCase(CurrencyType.Iron, ItemRarity.Common, true)]
        [TestCase(CurrencyType.Copper, ItemRarity.Common, true)]
        [TestCase(CurrencyType.Silver, ItemRarity.Rare, true)]
        [TestCase(CurrencyType.Gold, ItemRarity.Unique, true)]
        [TestCase(CurrencyType.Iron, ItemRarity.Magic, false)]
        [TestCase(CurrencyType.Copper, ItemRarity.Rare, false)]
        [TestCase(CurrencyType.Silver, ItemRarity.Unique, false)]
        public void LootFilter_AdmitsCoinsByTheDenominationsRarity(CurrencyType denomination, ItemRarity minimum, bool admitted)
        {
            var behaviour = new HeroBehaviour { LootFilterMinimum = minimum };

            Assert.That(behaviour.AdmitsCoin(denomination), Is.EqualTo(admitted));
        }
    }
}
