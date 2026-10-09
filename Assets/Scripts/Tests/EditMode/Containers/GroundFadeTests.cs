using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Containers
{
    /// <summary>
    /// How faint a Drop on the ground is drawn (epic #214, issue #221): the newest fully opaque, each drop
    /// landed since taking one fixed step off, down to a minimum - counted in drops, never in time.
    /// </summary>
    [TestFixture]
    public sealed class GroundFadeTests
    {
        private const float Step = 0.15f;
        private const float Minimum = 0.25f;

        [Test]
        public void TheNewest_IsFullyOpaque()
        {
            Assert.That(GroundFade.Alpha(age: 0, Step, Minimum), Is.EqualTo(1f));
        }

        [Test]
        public void EachLaterDrop_TakesOneStepOff()
        {
            Assert.That(GroundFade.Alpha(age: 1, Step, Minimum), Is.EqualTo(0.85f).Within(1e-6f));
            Assert.That(GroundFade.Alpha(age: 3, Step, Minimum), Is.EqualTo(0.55f).Within(1e-6f));
        }

        [Test]
        public void AnOldDrop_StopsFadingAtTheMinimum()
        {
            Assert.That(GroundFade.Alpha(age: 5, Step, Minimum), Is.EqualTo(Minimum).Within(1e-6f));
            Assert.That(GroundFade.Alpha(age: 40, Step, Minimum), Is.EqualTo(Minimum));
        }

        [Test]
        public void AFreshDrop_IsNotDimmedByHowManyCameBefore()
        {
            // One drop landing after the first fades it one step, whether the ground holds two drops or two hundred.
            Assert.That(GroundFade.Alpha(age: 1, Step, Minimum), Is.EqualTo(0.85f).Within(1e-6f));
        }

        [Test]
        public void AStepOfZero_LeavesEveryDropOpaque()
        {
            Assert.That(GroundFade.Alpha(age: 9, 0f, Minimum), Is.EqualTo(1f));
        }
    }
}
