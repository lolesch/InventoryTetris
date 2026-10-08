using NUnit.Framework;
using ToolSmiths.InventorySystem.Geometry;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Geometry
{
    /// <summary>Locks in the damage number's amount, label and motion (issue #181): hits only, rise and fade on sim time.</summary>
    [TestFixture]
    public sealed class DamageNumberMotionTests
    {
        private const float Eps = 1e-4f;
        private const float Duration = 0.8f;
        private const float Height = 60f;
        private const float Hold = 0.4f;

        [Test]
        public void Damage_IsPreviousMinusCurrent()
        {
            Assert.That(DamageNumberMotion.Damage(previous: 30f, current: 22.5f), Is.EqualTo(7.5f).Within(Eps));
        }

        [Test]
        public void Damage_OfAHeal_IsZero()
        {
            // The accumulator sums negative amounts too, so heals are filtered where the event is read.
            Assert.That(DamageNumberMotion.Damage(previous: 10f, current: 14f), Is.EqualTo(0f));
        }

        [Test]
        public void Damage_OfNoChange_IsZero()
        {
            Assert.That(DamageNumberMotion.Damage(previous: 10f, current: 10f), Is.EqualTo(0f));
        }

        [TestCase(12f, "12")]
        [TestCase(7.5f, "7.5")]
        [TestCase(7.46f, "7.5")]
        [TestCase(100f, "100")]
        public void Label_ShowsOneDecimalAtMost(float amount, string expected)
        {
            Assert.That(DamageNumberMotion.Label(amount), Is.EqualTo(expected));
        }

        [Test]
        public void Rise_StartsAtZeroAndEndsAtHeight()
        {
            Assert.That(DamageNumberMotion.Rise(0f, Duration, Height), Is.EqualTo(0f).Within(Eps));
            Assert.That(DamageNumberMotion.Rise(Duration, Duration, Height), Is.EqualTo(Height).Within(Eps));
        }

        [Test]
        public void Rise_SlowsDown_SoTheFirstHalfCoversMoreThanHalfTheHeight()
        {
            Assert.That(DamageNumberMotion.Rise(Duration / 2f, Duration, Height), Is.GreaterThan(Height / 2f));
        }

        [Test]
        public void Rise_PastTheEnd_StaysAtHeight()
        {
            Assert.That(DamageNumberMotion.Rise(Duration * 3f, Duration, Height), Is.EqualTo(Height).Within(Eps));
        }

        [Test]
        public void Alpha_IsFullWhileHeld()
        {
            Assert.That(DamageNumberMotion.Alpha(0f, Duration, Hold), Is.EqualTo(1f).Within(Eps));
            Assert.That(DamageNumberMotion.Alpha(Duration * Hold, Duration, Hold), Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void Alpha_FadesLinearlyToZeroAtTheEnd()
        {
            var midFade = Duration * (Hold + (1f - Hold) / 2f);

            Assert.That(DamageNumberMotion.Alpha(midFade, Duration, Hold), Is.EqualTo(0.5f).Within(Eps));
            Assert.That(DamageNumberMotion.Alpha(Duration, Duration, Hold), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void IsFinished_OnceElapsedReachesTheDuration()
        {
            Assert.That(DamageNumberMotion.IsFinished(Duration - 0.01f, Duration), Is.False);
            Assert.That(DamageNumberMotion.IsFinished(Duration, Duration), Is.True);
        }

        [Test]
        public void ZeroDuration_IsFinishedAtOnce_AndDoesNotDivideByZero()
        {
            Assert.That(DamageNumberMotion.IsFinished(0f, 0f), Is.True);
            Assert.That(DamageNumberMotion.Rise(0f, 0f, Height), Is.EqualTo(Height).Within(Eps));
            Assert.That(DamageNumberMotion.Alpha(0f, 0f, Hold), Is.EqualTo(0f).Within(Eps));
        }
    }
}
