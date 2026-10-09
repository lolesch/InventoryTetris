using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// How faint a Drop on the ground is drawn (epic #214, issue #221): the newest few fully opaque, each
    /// drop landed after those taking one fixed step off, down to a minimum - counted in drops, never in time.
    /// </summary>
    [TestFixture]
    public sealed class GroundFadeTests
    {
        private const int Fresh = 5;
        private const float Step = 0.15f;
        private const float Minimum = 0.25f;

        [TestCase(0)]
        [TestCase(4)]
        public void TheNewestFew_AreFullyOpaque(int age)
        {
            Assert.That(GroundFade.Alpha(age, Fresh, Step, Minimum), Is.EqualTo(1f));
        }

        [Test]
        public void EachDropBeyondThem_TakesOneStepOff()
        {
            Assert.That(GroundFade.Alpha(age: 5, Fresh, Step, Minimum), Is.EqualTo(0.85f).Within(1e-6f));
            Assert.That(GroundFade.Alpha(age: 7, Fresh, Step, Minimum), Is.EqualTo(0.55f).Within(1e-6f));
        }

        [Test]
        public void AnOldDrop_StopsFadingAtTheMinimum()
        {
            Assert.That(GroundFade.Alpha(age: 10, Fresh, Step, Minimum), Is.EqualTo(Minimum).Within(1e-6f));
            Assert.That(GroundFade.Alpha(age: 40, Fresh, Step, Minimum), Is.EqualTo(Minimum));
        }

        [Test]
        public void AFreshCountOfOne_FadesFromTheSecondNewest()
        {
            Assert.That(GroundFade.Alpha(age: 0, 1, Step, Minimum), Is.EqualTo(1f));
            Assert.That(GroundFade.Alpha(age: 1, 1, Step, Minimum), Is.EqualTo(0.85f).Within(1e-6f));
        }

        [Test]
        public void AStepOfZero_LeavesEveryDropOpaque()
        {
            Assert.That(GroundFade.Alpha(age: 9, Fresh, 0f, Minimum), Is.EqualTo(1f));
        }
    }
}
