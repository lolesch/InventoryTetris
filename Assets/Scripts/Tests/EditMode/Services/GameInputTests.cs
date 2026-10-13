using NUnit.Framework;
using Submodules.Utility.Bindings;
using Submodules.Utility.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>The game's catalog and its default keys; the mechanism under them is tested in the Utility submodule.</summary>
    [TestFixture]
    public sealed class GameInputTests
    {
        private InputActionAsset shipped;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Reset();
            shipped = Resources.Load<InputActionAsset>(GameBoot.InputResourceKey);
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Reset();

        private static IEnumerable<GameAction> Catalog() =>
            ((GameAction[])Enum.GetValues(typeof(GameAction))).Where(action => action != GameAction.None);

        [Test]
        public void TheShippedActions_HoldEveryMemberOfTheCatalog_EachBoundToAKey()
        {
            Assert.That(shipped, Is.Not.Null, "Assets/Resources/GameInput.inputactions");

            foreach (var action in Catalog())
            {
                var found = shipped.FindAction(action.ToString());

                Assert.That(found, Is.Not.Null, $"{action} has no action in the asset");
                Assert.That(found.bindings, Is.Not.Empty, $"{action} is bound to nothing");
            }
        }

        [Test]
        public void TheShippedActions_BuildAService_WithoutThrowing()
        {
            using var keys = new InputService<GameAction>(shipped, new InMemoryBindingStore());

            Assert.That(keys.IsHeld(GameAction.TogglePause), Is.False);
        }

        // The keys the game had before the catalog existed (hard-coded reads and four scene hotkeys).
        [TestCase(GameAction.TogglePause, "<Keyboard>/space")]
        [TestCase(GameAction.ToggleHeroPanel, "<Keyboard>/i")]
        [TestCase(GameAction.ToggleStash, "<Keyboard>/s")]
        [TestCase(GameAction.ToggleVendor, "<Keyboard>/v")]
        [TestCase(GameAction.ToggleHealer, "<Keyboard>/h")]
        [TestCase(GameAction.QuickMove, "<Keyboard>/leftShift")]
        [TestCase(GameAction.SecondSlot, "<Keyboard>/leftShift")]
        [TestCase(GameAction.HalveStack, "<Keyboard>/leftCtrl")]
        [TestCase(GameAction.Peek, "<Keyboard>/leftAlt")]
        [TestCase(GameAction.Peek, "<Keyboard>/rightAlt")]
        [TestCase(GameAction.Cancel, "<Keyboard>/escape")]
        public void TheDefaultKeys_AreTheOnesTheGameHadBefore(GameAction action, string path)
        {
            Assert.That(shipped.FindAction(action.ToString()).bindings.Select(binding => binding.path), Does.Contain(path));
        }

        [Test]
        public void WithNothingArmed_NothingIsHeld_AndThePointerIsAtTheOrigin()
        {
            Assert.That(GameInput.Armed, Is.Null);
            Assert.That(GameInput.IsHeld(GameAction.Peek), Is.False);
            Assert.That(GameInput.PointerPosition, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void AnArmedRegistryWithoutAnInputService_IsTheSameAsNothingArmed()
        {
            ServiceLocator.Install(new ServiceRegistry());

            Assert.That(GameInput.Armed, Is.Null);
            Assert.That(GameInput.IsHeld(GameAction.Peek), Is.False);
        }

        [Test]
        public void WithAServiceArmed_TheViewsReadsAreTheServices()
        {
            var input = new FakeInput { PointerPosition = new Vector2(30f, 40f) };
            var registry = new ServiceRegistry();
            registry.Register<IInputService<GameAction>>(input);
            ServiceLocator.Install(registry);

            input.Press(GameAction.Peek);

            Assert.That(GameInput.Armed, Is.SameAs(input));
            Assert.That(GameInput.IsHeld(GameAction.Peek), Is.True);
            Assert.That(GameInput.IsHeld(GameAction.HalveStack), Is.False);
            Assert.That(GameInput.PointerPosition, Is.EqualTo(new Vector2(30f, 40f)));
        }
    }
}
