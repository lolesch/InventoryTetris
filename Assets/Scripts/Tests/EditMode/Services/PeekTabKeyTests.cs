using NUnit.Framework;
using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.Services;

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

        [Test]
        public void TabHeld_Peeks_AndLettingGoEndsIt()
        {
            input.Press(GameAction.PeekTab);
            Assert.That(key.IsHeld(), Is.True);

            input.Release(GameAction.PeekTab);
            Assert.That(key.IsHeld(), Is.False);
        }

        [Test]
        public void ShowDetailsAlone_DoesNotPeek()
        {
            input.Press(GameAction.ShowDetails);

            Assert.That(key.IsHeld(), Is.False);
        }

        [Test]
        public void TabThenShowDetails_KeepsThePeek()
        {
            input.Press(GameAction.PeekTab);
            Assert.That(key.IsHeld(), Is.True);

            input.Press(GameAction.ShowDetails);
            Assert.That(key.IsHeld(), Is.True);
        }

        [Test]
        public void ShowDetailsThenTab_IsTheWindowSwitch_AndNeverPeeks()
        {
            input.Press(GameAction.ShowDetails);
            input.Press(GameAction.PeekTab);

            Assert.That(key.IsHeld(), Is.False);
        }

        [Test]
        public void AfterShowDetailsThenTab_LettingGoOfShowDetailsFirst_StillDoesNotPeek()
        {
            input.Press(GameAction.ShowDetails);
            input.Press(GameAction.PeekTab);
            Assert.That(key.IsHeld(), Is.False);

            input.Release(GameAction.ShowDetails);
            Assert.That(key.IsHeld(), Is.False, "the press began blocked and stays blocked until Tab is let go");
        }

        [Test]
        public void ABlockedPress_DoesNotBlockTheNextOne()
        {
            input.Press(GameAction.ShowDetails);
            input.Press(GameAction.PeekTab);
            _ = key.IsHeld();
            input.Release(GameAction.PeekTab);
            input.Release(GameAction.ShowDetails);
            _ = key.IsHeld();

            input.Press(GameAction.PeekTab);

            Assert.That(key.IsHeld(), Is.True);
        }
    }
}
