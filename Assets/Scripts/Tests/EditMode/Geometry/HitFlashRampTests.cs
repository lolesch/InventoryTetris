using NUnit.Framework;
using ToolSmiths.InventorySystem.Geometry;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Geometry
{
    /// <summary>Locks in the hit flash's ramp (issue #179): full on a hit, linear back to zero on sim time.</summary>
    [TestFixture]
    public sealed class HitFlashRampTests
    {
        private const float Eps = 1e-5f;
        private const float Duration = 0.2f;

        [Test]
        public void NewRamp_IsIdle()
        {
            var ramp = new HitFlashRamp();

            Assert.That(ramp.Intensity, Is.EqualTo(0f));
            Assert.That(ramp.IsActive, Is.False);
        }

        [Test]
        public void Hit_StartsAtFullIntensity()
        {
            var ramp = new HitFlashRamp();

            ramp.Hit();

            Assert.That(ramp.Intensity, Is.EqualTo(1f));
            Assert.That(ramp.IsActive, Is.True);
        }

        [Test]
        public void SeveralHitsInOneFrame_FlashOnceNotStacked()
        {
            // A x8 frame runs several ticks, each can raise a hit.
            var ramp = new HitFlashRamp();

            for (var i = 0; i < 8; i++)
                ramp.Hit();

            Assert.That(ramp.Intensity, Is.EqualTo(1f));

            ramp.Advance(Duration * 0.5f, Duration);

            Assert.That(ramp.Intensity, Is.EqualTo(0.5f).Within(Eps), "one ramp back, not eight of them");
        }

        [Test]
        public void Advance_RampsLinearlyBackToZeroOverTheDuration()
        {
            var ramp = new HitFlashRamp();
            ramp.Hit();

            ramp.Advance(Duration * 0.25f, Duration);
            Assert.That(ramp.Intensity, Is.EqualTo(0.75f).Within(Eps));

            ramp.Advance(Duration * 0.25f, Duration);
            Assert.That(ramp.Intensity, Is.EqualTo(0.5f).Within(Eps));
        }

        [Test]
        public void Advance_PastTheDuration_StopsAtZeroAndGoesIdle()
        {
            var ramp = new HitFlashRamp();
            ramp.Hit();

            ramp.Advance(Duration * 3f, Duration);

            Assert.That(ramp.Intensity, Is.EqualTo(0f));
            Assert.That(ramp.IsActive, Is.False);
        }

        [Test]
        public void Advance_WithZeroDelta_KeepsTheIntensity()
        {
            // Pause: sim speed 0 freezes the sim, and the flash with it.
            var ramp = new HitFlashRamp();
            ramp.Hit();
            ramp.Advance(Duration * 0.5f, Duration);

            ramp.Advance(0f, Duration);

            Assert.That(ramp.Intensity, Is.EqualTo(0.5f).Within(Eps));
        }

        [Test]
        public void Hit_MidRamp_RestartsFromFull()
        {
            var ramp = new HitFlashRamp();
            ramp.Hit();
            ramp.Advance(Duration * 0.75f, Duration);

            ramp.Hit();

            Assert.That(ramp.Intensity, Is.EqualTo(1f));
        }

        [Test]
        public void Advance_WithANonPositiveDuration_EndsTheFlashAtOnce()
        {
            var ramp = new HitFlashRamp();
            ramp.Hit();

            ramp.Advance(0.01f, 0f);

            Assert.That(ramp.Intensity, Is.EqualTo(0f));
        }

        [Test]
        public void Advance_WhenIdle_StaysIdle()
        {
            var ramp = new HitFlashRamp();

            ramp.Advance(1f, Duration);

            Assert.That(ramp.Intensity, Is.EqualTo(0f));
        }

        [Test]
        public void Reset_ClearsAFlashInFlight()
        {
            var ramp = new HitFlashRamp();
            ramp.Hit();

            ramp.Reset();

            Assert.That(ramp.Intensity, Is.EqualTo(0f));
            Assert.That(ramp.IsActive, Is.False);
        }

        [Test]
        public void IsHit_IsTrueOnlyForADropInHealth()
        {
            Assert.That(HitFlashRamp.IsHit(previous: 10f, current: 7f), Is.True, "damage");
            Assert.That(HitFlashRamp.IsHit(previous: 10f, current: 0f), Is.True, "the killing blow");
            Assert.That(HitFlashRamp.IsHit(previous: 7f, current: 10f), Is.False, "a heal is not a hit");
            Assert.That(HitFlashRamp.IsHit(previous: 10f, current: 10f), Is.False);
        }
    }
}
