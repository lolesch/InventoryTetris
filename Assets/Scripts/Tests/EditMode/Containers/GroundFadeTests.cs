using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// How faint a Drop on the floor is drawn (epic #214, issue #221): by its age rank among the other
    /// drops - the newest fully opaque, the oldest at a minimum - and never by time.
    /// </summary>
    [TestFixture]
    public sealed class GroundFadeTests
    {
        private const float Minimum = 0.35f;

        [Test]
        public void TheNewest_IsFullyOpaque()
        {
            Assert.That(GroundFade.Alpha(rank: 4, count: 5, Minimum), Is.EqualTo(1f));
        }

        [Test]
        public void TheOldest_IsAtTheMinimum()
        {
            Assert.That(GroundFade.Alpha(rank: 0, count: 5, Minimum), Is.EqualTo(Minimum));
        }

        [Test]
        public void ADropBetween_IsStepped_ByItsRank()
        {
            // 0.35, 0.5125, 0.675, 0.8375, 1 - five drops, four equal steps.
            Assert.That(GroundFade.Alpha(rank: 2, count: 5, Minimum), Is.EqualTo(0.675f).Within(1e-6f));
        }

        [Test]
        public void ALoneDrop_IsTheNewest_SoFullyOpaque()
        {
            Assert.That(GroundFade.Alpha(rank: 0, count: 1, Minimum), Is.EqualTo(1f));
        }

        [Test]
        public void TheFade_DependsOnTheRank_NotOnHowManyDropsThereAre()
        {
            Assert.That(GroundFade.Alpha(rank: 1, count: 2, Minimum), Is.EqualTo(1f));
            Assert.That(GroundFade.Alpha(rank: 9, count: 10, Minimum), Is.EqualTo(1f));
            Assert.That(GroundFade.Alpha(rank: 0, count: 10, Minimum), Is.EqualTo(Minimum));
        }

        [Test]
        public void AMinimumOfOne_LeavesEveryDropOpaque()
        {
            Assert.That(GroundFade.Alpha(rank: 0, count: 3, 1f), Is.EqualTo(1f));
        }
    }
}
