using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Character
{
    /// <summary>
    /// Locks in <see cref="ResourceRegen.Step"/> &#8212; the synchronous replacement for
    /// <c>BaseCharacter</c>'s old <c>async void</c> regen (issue #17). The interesting behaviour is
    /// the per-resource post-depletion rule carried by <c>recoveryDelay</c>: never (Health, -1),
    /// immediate (Resource, 0), and a timed wait (Shield, 2s).
    /// </summary>
    [TestFixture]
    public sealed class ResourceRegenTests
    {
        private const float Rate = 10f; // per second

        private static CharacterResource Resource(float total, float current)
        {
            var resource = new CharacterResource(StatName.Health, total);
            resource.RemoveFromCurrent(total - current);
            return resource;
        }

        [Test]
        public void Step_PartiallyDrainedResource_RegeneratesByRateTimesDelta()
        {
            var resource = Resource(total: 100f, current: 50f);

            var secondsEmpty = ResourceRegen.Step(resource, Rate, recoveryDelay: -1f, secondsEmpty: 0f, deltaSeconds: 0.5f);

            Assert.That(resource.CurrentValue, Is.EqualTo(55f).Within(0.0001f));
            Assert.That(secondsEmpty, Is.EqualTo(0f));
        }

        [Test]
        public void Step_RegenNeverOvershootsTotal()
        {
            var resource = Resource(total: 100f, current: 98f);

            _ = ResourceRegen.Step(resource, Rate, recoveryDelay: -1f, secondsEmpty: 0f, deltaSeconds: 1f);

            Assert.That(resource.CurrentValue, Is.EqualTo(100f).Within(0.0001f));
        }

        [Test]
        public void Step_NegativeRecoveryDelay_DoesNotRegenerateWhileEmpty()
        {
            var resource = Resource(total: 100f, current: 0f);

            var secondsEmpty = ResourceRegen.Step(resource, Rate, recoveryDelay: -1f, secondsEmpty: 0f, deltaSeconds: 1f);

            Assert.That(resource.CurrentValue, Is.EqualTo(0f));
            Assert.That(secondsEmpty, Is.EqualTo(0f));
        }

        [Test]
        public void Step_ZeroRecoveryDelay_RegeneratesAnEmptyResourceImmediately()
        {
            var resource = Resource(total: 100f, current: 0f);

            _ = ResourceRegen.Step(resource, Rate, recoveryDelay: 0f, secondsEmpty: 0f, deltaSeconds: 0.5f);

            Assert.That(resource.CurrentValue, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void Step_PositiveRecoveryDelay_HoldsEmptyUntilTheDelayElapses()
        {
            var resource = Resource(total: 100f, current: 0f);

            var secondsEmpty = ResourceRegen.Step(resource, Rate, recoveryDelay: 2f, secondsEmpty: 0f, deltaSeconds: 1f);
            Assert.That(resource.CurrentValue, Is.EqualTo(0f), "still inside the 2s wait");
            Assert.That(secondsEmpty, Is.EqualTo(1f));

            secondsEmpty = ResourceRegen.Step(resource, Rate, recoveryDelay: 2f, secondsEmpty, deltaSeconds: 1f);
            Assert.That(resource.CurrentValue, Is.EqualTo(10f).Within(0.0001f), "wait elapsed, regen resumes");
            Assert.That(secondsEmpty, Is.EqualTo(2f));
        }

        [Test]
        public void Step_ResourceNoLongerEmpty_ResetsTheDepletionTimer()
        {
            var resource = Resource(total: 100f, current: 5f);

            var secondsEmpty = ResourceRegen.Step(resource, Rate, recoveryDelay: 2f, secondsEmpty: 1.5f, deltaSeconds: 1f);

            Assert.That(secondsEmpty, Is.EqualTo(0f));
            Assert.That(resource.CurrentValue, Is.EqualTo(15f).Within(0.0001f));
        }

        [Test]
        public void Step_NegativeRate_DrainsTheResource()
        {
            var resource = Resource(total: 100f, current: 50f);

            _ = ResourceRegen.Step(resource, regenPerSecond: -10f, recoveryDelay: -1f, secondsEmpty: 0f, deltaSeconds: 1f);

            Assert.That(resource.CurrentValue, Is.EqualTo(40f).Within(0.0001f));
        }
    }
}
