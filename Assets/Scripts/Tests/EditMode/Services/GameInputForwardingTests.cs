using NUnit.Framework;
using Submodules.Utility.Services;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Services;
using UnityEngine.InputSystem;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// <see cref="GameInput.Performed"/> over the real service and a virtual keyboard: a view subscribed
    /// to it keeps hearing the keys across a boot that replaces the service (issue #69).
    /// </summary>
    public sealed class GameInputForwardingTests : InputTestFixture
    {
        private readonly List<UnityEngine.Object> created = new();
        private readonly List<GameAction> heard = new();
        private Keyboard keyboard;

        [SetUp]
        public void ArmOverAKeyboard()
        {
            ServiceLocator.Reset();
            GameLoop.Uninstall();
            GameLoop.Reset();

            heard.Clear();
            keyboard = InputSystem.AddDevice<Keyboard>();
            GameInput.Performed += heard.Add;
        }

        [TearDown]
        public void DisarmAndLetGo()
        {
            GameInput.Performed -= heard.Add;
            ServiceLocator.Reset();
            GameLoop.Uninstall();

            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private void Boot() => GameBoot.Arm(TestGameConfig.Create(created));

        [Test]
        public void APress_ReachesASubscriberOfGameInput()
        {
            Boot();

            PressAndRelease(keyboard.escapeKey);

            Assert.That(heard, Is.EqualTo(new[] { GameAction.Cancel }));
        }

        [Test]
        public void ASubscriber_KeepsHearing_AfterABootReplacedTheService_AndOnlyTheNewOne()
        {
            Boot();
            ServiceLocator.Reset();
            GameLoop.Uninstall();
            GameLoop.Reset();
            Boot();

            PressAndRelease(keyboard.escapeKey);

            Assert.That(heard, Is.EqualTo(new[] { GameAction.Cancel }), "once: the replaced service was disposed");
        }

        [Test]
        public void HammeringAHotkey_IsOnePress()
        {
            Boot();

            PressAndRelease(keyboard.escapeKey);
            currentTime += 0.02;
            PressAndRelease(keyboard.escapeKey);
            currentTime += 1.0;
            PressAndRelease(keyboard.escapeKey);

            Assert.That(heard, Is.EqualTo(new[] { GameAction.Cancel, GameAction.Cancel }));
        }
    }
}
