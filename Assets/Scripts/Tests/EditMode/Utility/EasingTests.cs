using System;
using NUnit.Framework;
using Submodules.Utility.Tools.Tweening;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Utility
{
    /// <summary>
    /// Pins <see cref="Easing.Evaluate"/> as a pure curve: every ease starts at 0 and
    /// ends at 1, <see cref="Ease.Linear"/> is the identity, and none of the shipped
    /// curves ever backtrack. Overshoot / bounce curves are deliberately not in the set
    /// (see the shared-UI spec, 2026-09-03), so "non-decreasing" holds for all of them.
    /// </summary>
    [TestFixture]
    public sealed class EasingTests
    {
        private static readonly Ease[] AllEases = (Ease[]) Enum.GetValues(typeof(Ease));

        [Test]
        public void EveryEase_StartsAtZero([ValueSource(nameof(AllEases))] Ease ease) =>
            Assert.That(Easing.Evaluate(ease, 0f), Is.EqualTo(0f).Within(1e-4f));

        [Test]
        public void EveryEase_EndsAtOne([ValueSource(nameof(AllEases))] Ease ease) =>
            Assert.That(Easing.Evaluate(ease, 1f), Is.EqualTo(1f).Within(1e-4f));

        [Test]
        public void Linear_IsTheIdentity()
        {
            Assert.That(Easing.Evaluate(Ease.Linear, 0.5f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(Easing.Evaluate(Ease.Linear, 0.25f), Is.EqualTo(0.25f).Within(1e-6f));
        }

        // Known Penner literals, independent of the implementation's own formula path.
        [Test]
        public void InQuad_IsTSquared() =>
            Assert.That(Easing.Evaluate(Ease.InQuad, 0.5f), Is.EqualTo(0.25f).Within(1e-5f));

        [Test]
        public void OutQuad_IsTheMirrorOfInQuad() =>
            Assert.That(Easing.Evaluate(Ease.OutQuad, 0.5f), Is.EqualTo(0.75f).Within(1e-5f));

        [Test]
        public void InOutEases_PassThroughTheMidpointAtTheMidpoint(
            [Values(Ease.InOutQuad, Ease.InOutCubic, Ease.InOutSine, Ease.InOutExpo)] Ease ease) =>
            Assert.That(Easing.Evaluate(ease, 0.5f), Is.EqualTo(0.5f).Within(1e-4f));

        [Test]
        public void EveryEase_IsNonDecreasingAcrossASweep([ValueSource(nameof(AllEases))] Ease ease)
        {
            var previous = Easing.Evaluate(ease, 0f);

            for (var step = 1; step <= 200; step++)
            {
                var t = step / 200f;
                var current = Easing.Evaluate(ease, t);

                Assert.That(current, Is.GreaterThanOrEqualTo(previous - 1e-5f),
                    $"{ease} decreased between t={(step - 1) / 200f} and t={t}");

                previous = current;
            }
        }

        [Test]
        public void Evaluate_ClampsInputOutsideTheUnitInterval(
            [ValueSource(nameof(AllEases))] Ease ease)
        {
            Assert.That(Easing.Evaluate(ease, -0.5f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Easing.Evaluate(ease, 1.5f), Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
