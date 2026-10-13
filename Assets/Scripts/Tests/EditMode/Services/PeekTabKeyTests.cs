using NUnit.Framework;
using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.Services;
using UnityEngine.InputSystem;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    [TestFixture]
    public sealed class PeekTabKeyTests
    {
        private FakeInput input;
        private PeekTabKey key;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Reset();
            input = new FakeInput();
            var registry = new ServiceRegistry();
            registry.Register<Submodules.Utility.Bindings.IInputService<GameAction>>(input);
            ServiceLocator.Install(registry);
            key = new PeekTabKey();
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Reset();

        // Tab as shipped: the physical key and the action bound to it go down together.
        private void PressTab()
        {
            input.PressKey(Key.Tab);
            input.Press(GameAction.PeekTab);
        }

        private void ReleaseTab()
        {
            input.ReleaseKey(Key.Tab);
            input.Release(GameAction.PeekTab);
        }

        [Test]
        public void TabHeld_Peeks_AndLettingGoEndsIt()
        {
            PressTab();
            Assert.That(key.IsHeld(), Is.True);

            ReleaseTab();
            Assert.That(key.IsHeld(), Is.False);
        }

        [TestCase(Key.LeftAlt)]
        [TestCase(Key.RightAlt)]
        public void AltAlone_DoesNotPeek(Key alt)
        {
            input.PressKey(alt);

            Assert.That(key.IsHeld(), Is.False);
        }

        [TestCase(Key.LeftAlt)]
        [TestCase(Key.RightAlt)]
        public void TabThenAlt_KeepsThePeek(Key alt)
        {
            PressTab();
            Assert.That(key.IsHeld(), Is.True);

            input.PressKey(alt);
            Assert.That(key.IsHeld(), Is.True);
        }

        [TestCase(Key.LeftAlt)]
        [TestCase(Key.RightAlt)]
        public void AltThenTab_IsTheWindowSwitch_AndNeverPeeks(Key alt)
        {
            input.PressKey(alt);
            PressTab();

            Assert.That(key.IsHeld(), Is.False);
        }

        [Test]
        public void AfterAltThenTab_LettingGoOfAltFirst_StillDoesNotPeek()
        {
            input.PressKey(Key.LeftAlt);
            PressTab();
            Assert.That(key.IsHeld(), Is.False);

            input.ReleaseKey(Key.LeftAlt);
            Assert.That(key.IsHeld(), Is.False, "the press began blocked and stays blocked until Tab is let go");
        }

        [Test]
        public void ABlockedPress_DoesNotBlockTheNextOne()
        {
            input.PressKey(Key.LeftAlt);
            PressTab();
            _ = key.IsHeld();
            ReleaseTab();
            input.ReleaseKey(Key.LeftAlt);
            _ = key.IsHeld();

            PressTab();

            Assert.That(key.IsHeld(), Is.True);
        }

        // The rule is about the keys: what ShowDetails is bound to changes nothing, so an action held first that is
        // not an Alt key does not block, and Alt itself blocks whatever ShowDetails is bound to.
        [Test]
        public void ShowDetailsHeldFirst_OnAKeyThatIsNotAlt_StillPeeks()
        {
            input.Press(GameAction.ShowDetails);
            input.PressKey(Key.LeftShift);
            PressTab();

            Assert.That(key.IsHeld(), Is.True);
        }

        [Test]
        public void AltThenTab_BlocksWithShowDetailsRebound()
        {
            input.PressKey(Key.LeftAlt);
            PressTab();

            Assert.That(input.IsHeld(GameAction.ShowDetails), Is.False, "ShowDetails is not on Alt in this test");
            Assert.That(key.IsHeld(), Is.False);
        }

        // A peek rebound off Tab is not the window switch: Alt then that key is not Alt+Tab.
        [Test]
        public void APeekRebound_OffTab_IsNotBlockedByAlt()
        {
            input.PressKey(Key.LeftAlt);
            input.PressKey(Key.Q);
            input.Press(GameAction.PeekTab);

            Assert.That(key.IsHeld(), Is.True);
        }
    }
}
