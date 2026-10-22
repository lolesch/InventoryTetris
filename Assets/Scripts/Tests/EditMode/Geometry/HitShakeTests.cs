using NUnit.Framework;
using ToolSmiths.InventorySystem.Geometry;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Geometry
{
    /// <summary>Locks in the hit shake's envelope (issue #180): a decaying wobble that restarts on a hit, on sim time.</summary>
    [TestFixture]
    public sealed class HitShakeTests
    {
        private const float Eps = 1e-4f;
        private const float Duration = 0.2f;
        private const float Amplitude = 8f;
        private const float Frequency = 10f;

        [Test]
        public void NewShake_IsIdleWithNoOffset()
        {
            var shake = new HitShake();

            Assert.That(shake.IsActive, Is.False);
            Assert.That(shake.Offset(Amplitude, Frequency), Is.EqualTo(UnityEngine.Vector2.zero));
        }

        [Test]
        public void Hit_KicksAtFullAmplitudeOnTheFirstFrame()
        {
            var shake = new HitShake();

            shake.Hit();

            Assert.That(shake.IsActive, Is.True);
            Assert.That(shake.Offset(Amplitude, Frequency).x, Is.EqualTo(Amplitude).Within(Eps));
            Assert.That(shake.Offset(Amplitude, Frequency).y, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void Offset_FollowsTheWobbleAtAKnownPhase()
        {
            // A quarter period in (0.025 s at 10 Hz) the horizontal wobble crosses zero;
            // intensity is 1 - 0.025 / 0.2 = 0.875, the vertical one is 8 * 0.875 * sin(1.3 * pi / 2).
            var shake = new HitShake();
            shake.Hit();

            shake.Advance(0.025f, Duration);
            var offset = shake.Offset(Amplitude, Frequency);

            Assert.That(offset.x, Is.EqualTo(0f).Within(Eps));
            Assert.That(offset.y, Is.EqualTo(6.23705f).Within(1e-3f));
        }

        [Test]
        public void Offset_DecaysAndNeverExceedsTheRemainingAmplitude()
        {
            var shake = new HitShake();
            shake.Hit();

            for (var step = 0; step < 20; step++)
            {
                shake.Advance(Duration / 20f, Duration);
                var bound = Amplitude * shake.Intensity;

                Assert.That(shake.Offset(Amplitude, Frequency).magnitude, Is.LessThanOrEqualTo(bound * 1.5f + Eps),
                    $"step {step}: the wobble is bounded by the envelope");
            }
        }

        [Test]
        public void Advance_PastTheDuration_SettlesToZeroOffsetAndGoesIdle()
        {
            var shake = new HitShake();
            shake.Hit();

            shake.Advance(Duration * 3f, Duration);

            Assert.That(shake.IsActive, Is.False);
            Assert.That(shake.Offset(Amplitude, Frequency), Is.EqualTo(UnityEngine.Vector2.zero));
        }

        [Test]
        public void Advance_WithZeroDelta_FreezesTheOffset()
        {
            // Pause: sim speed 0 freezes the sim, and the shake with it.
            var shake = new HitShake();
            shake.Hit();
            shake.Advance(0.03f, Duration);
            var before = shake.Offset(Amplitude, Frequency);

            shake.Advance(0f, Duration);

            Assert.That(shake.Offset(Amplitude, Frequency), Is.EqualTo(before));
        }

        [Test]
        public void Hit_MidShake_RestartsInsteadOfStacking()
        {
            var shake = new HitShake();
            shake.Hit();
            shake.Advance(0.07f, Duration);

            shake.Hit();

            Assert.That(shake.Intensity, Is.EqualTo(1f));
            Assert.That(shake.Offset(Amplitude, Frequency).x, Is.EqualTo(Amplitude).Within(Eps), "same kick as a fresh hit");
            Assert.That(shake.Offset(Amplitude, Frequency).magnitude, Is.LessThanOrEqualTo(Amplitude * 1.5f));
        }

        [Test]
        public void SeveralHitsInOneFrame_ShakeOnceNotStacked()
        {
            // A x8 frame runs several ticks, each can raise a hit.
            var once = new HitShake();
            once.Hit();
            once.Advance(0.05f, Duration);

            var many = new HitShake();
            for (var i = 0; i < 8; i++)
                many.Hit();
            many.Advance(0.05f, Duration);

            Assert.That(many.Offset(Amplitude, Frequency), Is.EqualTo(once.Offset(Amplitude, Frequency)));
        }

        [Test]
        public void Advance_WithANonPositiveDuration_EndsTheShakeAtOnce()
        {
            var shake = new HitShake();
            shake.Hit();

            shake.Advance(0.01f, 0f);

            Assert.That(shake.IsActive, Is.False);
            Assert.That(shake.Offset(Amplitude, Frequency), Is.EqualTo(UnityEngine.Vector2.zero));
        }

        [Test]
        public void Reset_ClearsAShakeInFlight()
        {
            var shake = new HitShake();
            shake.Hit();
            shake.Advance(0.02f, Duration);

            shake.Reset();

            Assert.That(shake.IsActive, Is.False);
            Assert.That(shake.Offset(Amplitude, Frequency), Is.EqualTo(UnityEngine.Vector2.zero));
        }

        [Test]
        public void IsHit_IsTrueOnlyForADropInHealth()
        {
            Assert.That(HitShake.IsHit(previous: 10f, current: 7f), Is.True, "damage");
            Assert.That(HitShake.IsHit(previous: 10f, current: 0f), Is.True, "the killing blow");
            Assert.That(HitShake.IsHit(previous: 7f, current: 10f), Is.False, "a heal is not a hit");
            Assert.That(HitShake.IsHit(previous: 10f, current: 10f), Is.False);
        }
    }
}
