using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Statistics
{
    /// <summary>
    /// Pins the roll-quality font size: the bottom of a range renders at the minimum size, the top at the
    /// maximum, and a fixed roll (a zero-width range) at the maximum, since it cannot roll any better.
    /// </summary>
    [TestFixture]
    public sealed class RollQualityTests
    {
        private const float Min = 18f;
        private const float Max = 24f;

        private static StatModifier Roll(int low, int high, float value) => new(new Vector2Int(low, high), value);

        [Test]
        public void BottomOfTheRange_IsTheMinimumSize() =>
            Assert.That(RollQuality.FontSize(Roll(10, 20, 10f), Min, Max), Is.EqualTo(18f).Within(1e-4f));

        [Test]
        public void TopOfTheRange_IsTheMaximumSize() =>
            Assert.That(RollQuality.FontSize(Roll(10, 20, 20f), Min, Max), Is.EqualTo(24f).Within(1e-4f));

        [Test]
        public void MidOfTheRange_IsHalfwayBetween() =>
            Assert.That(RollQuality.FontSize(Roll(10, 20, 15f), Min, Max), Is.EqualTo(21f).Within(1e-4f));

        [Test]
        public void AFixedRoll_IsTheMaximumSize() =>
            Assert.That(RollQuality.FontSize(Roll(5, 5, 5f), Min, Max), Is.EqualTo(24f).Within(1e-4f));

        [Test]
        public void AFixedRollAtZero_IsTheMaximumSize() =>
            Assert.That(RollQuality.FontSize(Roll(0, 0, 0f), Min, Max), Is.EqualTo(24f).Within(1e-4f));
    }
}
