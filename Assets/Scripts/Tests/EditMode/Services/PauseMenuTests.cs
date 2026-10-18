using NUnit.Framework;
using Submodules.Utility.Services;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The Pause Menu's state (issue #257): open and closed, the sim hold it takes through the menu hold, and
    /// the keys it swallows while open. Engine-free over a built Hero and World, like
    /// <see cref="SimulationServiceTests"/>; the scene's panel is proven in Play Mode.
    /// </summary>
    [TestFixture]
    public sealed class PauseMenuTests
    {
        private readonly List<Object> created = new();
        private SimulationService simulation;
        private PauseMenu menu;
        private LocationConfig thornwood;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Reset();

            var config = TestGameConfig.Create(created);
            var rolls = new SessionBuilderTests.FixedRolls(0.5f);
            var items = new ItemService(config, rolls);
            var session = SessionBuilder.Build(config, GameBoot.Load().DefaultHero, items);
            simulation = new SimulationService(session, items, new InventoryService(session, items), config, rolls);
            menu = new PauseMenu(simulation);

            thornwood = ScriptableObject.CreateInstance<LocationConfig>();
            created.Add(thornwood);
            var so = new SerializedObject(thornwood);
            so.FindProperty("id").stringValue = "thornwood";
            so.FindProperty("categoryDistribution").objectReferenceValue = config.ItemCategoryDistribution;
            so.FindProperty("rarityDistribution").objectReferenceValue = config.ItemRarityDistribution;
            _ = so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Reset();

            foreach (var asset in created)
                Object.DestroyImmediate(asset);

            created.Clear();
        }

        [Test]
        public void OpeningInTheField_HoldsTheRun_AndClosingReleasesIt()
        {
            simulation.Send(thornwood);

            menu.Open();
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(simulation.IsMenuFrozen, Is.True);

            menu.Close();
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(simulation.IsMenuFrozen, Is.False);
        }

        [Test]
        public void ASpacePausedRun_IsStillPausedAfterTheMenuCloses_AndARunningOneResumes()
        {
            simulation.Send(thornwood);
            simulation.SetPaused(true);

            menu.Open();
            menu.Close();
            Assert.That(simulation.IsPaused, Is.True, "the player's own Pause is given back as found");

            simulation.SetPaused(false);
            menu.Open();
            menu.Close();
            Assert.That(simulation.IsPaused, Is.False);
            Assert.That(simulation.SimDelta(0.1f), Is.GreaterThan(0f));
        }

        [Test]
        public void InTown_TheMenuOpensAndCloses_WithNothingToHold()
        {
            menu.Open();

            Assert.That(menu.IsOpen, Is.True);
            Assert.That(simulation.IsMenuFrozen, Is.False);

            menu.Close();
            Assert.That(menu.IsOpen, Is.False);
        }

        [Test]
        public void Toggle_OpensAClosedMenu_AndClosesAnOpenOne()
        {
            menu.Toggle();
            Assert.That(menu.IsOpen, Is.True);

            menu.Toggle();
            Assert.That(menu.IsOpen, Is.False);
        }

        [Test]
        public void OpenChanged_RaisesOncePerChange()
        {
            var raised = new List<bool>();
            menu.OpenChanged += raised.Add;

            menu.Open();
            menu.Open();
            menu.Close();
            menu.Close();

            Assert.That(raised, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void ThePauseKey_DoesNothingWhileTheMenuIsOpen()
        {
            var input = new FakeInput();
            _ = new PauseHotkey(simulation, input);
            simulation.Send(thornwood);
            menu.Open();

            input.Press(GameAction.TogglePause);

            Assert.That(simulation.IsPaused, Is.False);

            menu.Close();
            input.Press(GameAction.TogglePause);

            Assert.That(simulation.IsPaused, Is.True, "the key works again once the menu is closed");
        }

        [Test]
        public void HotkeysReachGameInputSubscribers_OnlyWhileTheMenuIsClosed_ExceptCancel()
        {
            var registry = new ServiceRegistry();
            registry.Register<IPauseMenu>(menu);
            ServiceLocator.Install(registry);
            var heard = new List<GameAction>();
            GameInput.Performed += heard.Add;

            try
            {
                menu.Open();
                GameInput.Raise(GameAction.ToggleStash);
                GameInput.Raise(GameAction.ToggleHeroPanel);
                GameInput.Raise(GameAction.TogglePause);
                GameInput.Raise(GameAction.Cancel);

                Assert.That(heard, Is.EqualTo(new[] { GameAction.Cancel }));

                menu.Close();
                GameInput.Raise(GameAction.ToggleStash);

                Assert.That(heard, Is.EqualTo(new[] { GameAction.Cancel, GameAction.ToggleStash }));
            }
            finally
            {
                GameInput.Performed -= heard.Add;
            }
        }

        [Test]
        public void TheBoot_RegistersTheMenu()
        {
            var registry = GameBoot.Build(TestGameConfig.Create(created));

            Assert.That(registry.Get<IPauseMenu>().IsOpen, Is.False);
        }
    }
}
