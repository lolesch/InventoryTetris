using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Statistics
{
    /// <summary>
    /// Pins the damage number's size and tint (issue #213): the size is the clamped map of the amount from zero up
    /// to the reference maximum onto the serialized sizes, a zero reference is guarded, and the damage type picks
    /// the tint.
    /// </summary>
    [TestFixture]
    public sealed class DamageNumberStyleTests
    {
        private const float Min = 20f;
        private const float Max = 40f;
        private static readonly Color Physical = new(1f, 0.8f, 0.2f, 1f);
        private static readonly Color Magical = new(0.3f, 0.2f, 0.7f, 1f);

        private static DamageNumberStyle Of(float amount, float reference, DamageType type = DamageType.PhysicalDamage) =>
            DamageNumberStyle.Of(amount, reference, type, Min, Max, Physical, Magical);

        [Test]
        public void AZeroAmount_IsTheMinimumSize() =>
            Assert.That(Of(0f, 10f).FontSize, Is.EqualTo(20f).Within(1e-4f));

        [Test]
        public void HalfTheReference_IsHalfwayBetween() =>
            Assert.That(Of(5f, 10f).FontSize, Is.EqualTo(30f).Within(1e-4f));

        [Test]
        public void TheReference_IsTheMaximumSize() =>
            Assert.That(Of(10f, 10f).FontSize, Is.EqualTo(40f).Within(1e-4f));

        [Test]
        public void MoreThanTheReference_StaysAtTheMaximumSize() =>
            Assert.That(Of(25f, 10f).FontSize, Is.EqualTo(40f).Within(1e-4f));

        [Test]
        public void ANegativeAmount_StaysAtTheMinimumSize() =>
            Assert.That(Of(-3f, 10f).FontSize, Is.EqualTo(20f).Within(1e-4f));

        [Test]
        public void AZeroReference_IsTheMaximumSize_NotNaN()
        {
            // Nothing could hit harder, so the hit is the best there is - the fixed-roll case of RollQuality.FontSize.
            var size = Of(4f, 0f).FontSize;

            Assert.That(float.IsNaN(size), Is.False);
            Assert.That(size, Is.EqualTo(40f).Within(1e-4f));
        }

        [Test]
        public void AZeroReference_WithAZeroAmount_IsTheMaximumSize() =>
            Assert.That(Of(0f, 0f).FontSize, Is.EqualTo(40f).Within(1e-4f));

        [Test]
        public void ANegativeReference_IsGuardedLikeAZeroOne() =>
            Assert.That(Of(4f, -2f).FontSize, Is.EqualTo(40f).Within(1e-4f));

        [Test]
        public void PhysicalDamage_UsesThePhysicalTint() =>
            Assert.That(Of(5f, 10f, DamageType.PhysicalDamage).Tint, Is.EqualTo(Physical));

        [Test]
        public void MagicalDamage_UsesTheMagicalTint() =>
            Assert.That(Of(5f, 10f, DamageType.MagicalDamage).Tint, Is.EqualTo(Magical));
    }
}
