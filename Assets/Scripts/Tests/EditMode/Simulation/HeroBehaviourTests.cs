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

        // ─── sim-speed slider mapping (issue #27) ──────────────────────────

        [TestCase(0f, 1f)]
        [TestCase(0.5f, 2.828427f)]  // sqrt(8) ≈ 2.828
        [TestCase(1f, 8f)]
        public void SliderToSimSpeed_MapsLogarithmically(float slider, float expected)
        {
            var speed = HeroBehaviour.SliderToSimSpeed(slider);

            Assert.That(speed, Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void SliderToSimSpeed_NeverBelowOne()
        {
            Assert.That(HeroBehaviour.SliderToSimSpeed(-1f), Is.GreaterThanOrEqualTo(1f));
        }

        [TestCase(1f, 0f)]
        [TestCase(8f, 1f)]
        [TestCase(2.828427f, 0.5f)]
        public void SimSpeedToSlider_IsInverseOfSliderToSimSpeed(float speed, float expectedSlider)
        {
            var slider = HeroBehaviour.SimSpeedToSlider(speed);

            Assert.That(slider, Is.EqualTo(expectedSlider).Within(0.001f));
        }

        [Test]
        public void SimSpeedToSlider_RoundTrip()
        {
            for (var t = 0f; t <= 1f; t += 0.1f)
            {
                var speed = HeroBehaviour.SliderToSimSpeed(t);
                var roundTrip = HeroBehaviour.SimSpeedToSlider(speed);

                Assert.That(roundTrip, Is.EqualTo(t).Within(0.001f),
                    $"round-trip failed for slider value {t}");
            }
        }

        // ─── loot-filter slider mapping (issue #27) ────────────────────────

        [TestCase(ItemRarity.Common, 0)]
        [TestCase(ItemRarity.Magic, 1)]
        [TestCase(ItemRarity.Rare, 2)]
        [TestCase(ItemRarity.Unique, 3)]
        [TestCase(ItemRarity.NoDrop, 0)]  // NoDrop (0) is below Common → index 0
        public void RarityIndex_MapsRarityToSliderIndex(ItemRarity rarity, int expected)
        {
            Assert.That(HeroBehaviour.RarityIndex(rarity), Is.EqualTo(expected));
        }

        [TestCase(0, ItemRarity.Common)]
        [TestCase(1, ItemRarity.Magic)]
        [TestCase(2, ItemRarity.Rare)]
        [TestCase(3, ItemRarity.Unique)]
        public void RarityForIndex_MapsSliderIndexToRarity(int index, ItemRarity expected)
        {
            Assert.That(HeroBehaviour.RarityForIndex(index), Is.EqualTo(expected));
        }

        [Test]
        public void RarityForIndex_ClampsOutOfBoundsIndices()
        {
            Assert.That(HeroBehaviour.RarityForIndex(-1), Is.EqualTo(ItemRarity.Common));
            Assert.That(HeroBehaviour.RarityForIndex(99), Is.EqualTo(ItemRarity.Unique));
        }

        [Test]
        public void RarityIndex_RoundTrip()
        {
            foreach (var rarity in HeroBehaviour.RaritySteps)
            {
                var index = HeroBehaviour.RarityIndex(rarity);
                var roundTrip = HeroBehaviour.RarityForIndex(index);

                Assert.That(roundTrip, Is.EqualTo(rarity),
                    $"round-trip failed for rarity {rarity}");
            }
        }
    }
}
