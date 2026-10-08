using NUnit.Framework;
using ToolSmiths.InventorySystem.Geometry;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Geometry
{
    /// <summary>Locks in the arena's figure maths: facing and the dying fade.</summary>
    [TestFixture]
    public sealed class ArenaLayoutTests
    {
        private const float Eps = 1e-4f;

        [TestCase(500f, 100f, -1, 1)]   // hero to the right of the enemy
        [TestCase(100f, 500f, 1, -1)]   // hero to the left
        [TestCase(500f, 100f, 1, 1)]
        [TestCase(100f, 500f, -1, -1)]
        public void FacingSign_FollowsWhichSideTheHeroIsOn(float heroX, float enemyX, int current, int expected)
        {
            Assert.That(ArenaLayout.FacingSign(enemyX, heroX, current, 5f), Is.EqualTo(expected));
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void FacingSign_InsideTheDeadZone_KeepsTheCurrentSign(int current)
        {
            // 3 units to either side of the hero, dead zone 5
            Assert.That(ArenaLayout.FacingSign(100f, 103f, current, 5f), Is.EqualTo(current));
            Assert.That(ArenaLayout.FacingSign(100f, 97f, current, 5f), Is.EqualTo(current));
            Assert.That(ArenaLayout.FacingSign(100f, 100f, current, 5f), Is.EqualTo(current));
        }

        [Test]
        public void FacingSign_JustOutsideTheDeadZone_Flips()
        {
            Assert.That(ArenaLayout.FacingSign(100f, 105f, -1, 5f), Is.EqualTo(1));
            Assert.That(ArenaLayout.FacingSign(100f, 95f, 1, 5f), Is.EqualTo(-1));
        }

        [Test]
        public void DyingAlpha_StartsOpaqueAndFallsLinearlyToZero()
        {
            Assert.That(ArenaLayout.DyingAlpha(0f, 0.4f), Is.EqualTo(1f).Within(Eps));
            Assert.That(ArenaLayout.DyingAlpha(0.1f, 0.4f), Is.EqualTo(0.75f).Within(Eps));
            Assert.That(ArenaLayout.DyingAlpha(0.2f, 0.4f), Is.EqualTo(0.5f).Within(Eps));
            Assert.That(ArenaLayout.DyingAlpha(0.4f, 0.4f), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void DyingAlpha_StaysWithinZeroAndOne_OutsideTheInterval()
        {
            Assert.That(ArenaLayout.DyingAlpha(-1f, 0.4f), Is.EqualTo(1f));
            Assert.That(ArenaLayout.DyingAlpha(9f, 0.4f), Is.EqualTo(0f));
        }

        [Test]
        public void DyingAlpha_WithNoDuration_IsAlreadyGone()
        {
            Assert.That(ArenaLayout.DyingAlpha(0f, 0f), Is.EqualTo(0f));
            Assert.That(ArenaLayout.DyingAlpha(0f, -1f), Is.EqualTo(0f));
        }
    }
}
