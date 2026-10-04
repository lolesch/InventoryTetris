using NUnit.Framework;
using Submodules.Utility.Services;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Services;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    [TestFixture]
    public sealed class GameBootTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig testConfig;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Reset();
            GameLoop.Uninstall();
            GameLoop.Reset();
            testConfig = TestGameConfig.Create(created);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Reset();
            GameLoop.Uninstall();

            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        [Test]
        public void Build_WithoutAConfig_Throws_NamingWhatIsExpected()
        {
            var e = Assert.Throws<InvalidOperationException>(() => GameBoot.Build(null));

            Assert.That(e.Message, Does.Contain(nameof(GameConfig)));
            Assert.That(e.Message, Does.Contain("Assets/Resources/GameConfig.asset"));
        }

        [Test]
        public void Build_WithAConfigMissingWhatAServiceNeeds_Throws_AtBoot()
        {
            var empty = TestGameConfig.CreateEmpty(created);

            var e = Assert.Throws<InvalidOperationException>(() => GameBoot.Build(empty));

            Assert.That(e.Message, Does.Contain(nameof(GameConfig)));
        }

        [Test]
        public void Build_RegistersTheItemService_UnderItsInterface()
        {
            var registry = GameBoot.Build(testConfig);

            Assert.That(registry.Get<IItemService>(), Is.InstanceOf<ItemService>());
        }

        [Test]
        public void Arm_MakesTheItemServiceReachableFromTheLocator()
        {
            GameBoot.Arm(testConfig);

            Assert.That(ItemService.Instance, Is.SameAs(ServiceLocator.Get<IItemService>()));
        }

        [Test]
        public void Arm_WithoutAConfig_Throws_AndLeavesNothingArmed()
        {
            _ = Assert.Throws<InvalidOperationException>(() => GameBoot.Arm(null));

            Assert.That(ServiceLocator.IsArmed, Is.False);
            Assert.That(GameLoop.IsInstalled, Is.False);
        }

        [Test]
        public void Build_FromATestConfig_NeedsNoScene_AndMutatesNone()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            var objects = Resources.FindObjectsOfTypeAll<GameObject>().Length;

            var registry = GameBoot.Build(testConfig);

            Assert.That(registry, Is.Not.Null);
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Length, Is.EqualTo(objects));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
            Assert.That(ServiceLocator.IsArmed, Is.False, "Build is the seam: it touches no global.");
        }

        [Test]
        public void Arm_ArmsTheLocator_AndInstallsTheLoopExactlyOnce()
        {
            GameBoot.Arm(testConfig);

            Assert.That(ServiceLocator.IsArmed, Is.True);
            Assert.That(GameLoop.IsInstalled, Is.True);
        }

        [Test]
        public void Arm_AfterAReset_StartsFromACleanBoot_WithoutDoublingTheLoop()
        {
            GameBoot.Arm(testConfig);
            var first = ServiceLocator.Current;

            // What SubsystemRegistration does between two Play entries with domain reload disabled.
            ServiceLocator.Reset();
            GameBoot.Arm(testConfig);

            Assert.That(ServiceLocator.Current, Is.Not.SameAs(first));
            Assert.That(GameLoop.IsInstalled, Is.True);
        }

        [Test]
        public void ReadingTheLocator_AfterArm_CreatesNoGameObjectAndDirtiesNoScene()
        {
            GameBoot.Arm(testConfig);
            var scene = EditorSceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            var objects = Resources.FindObjectsOfTypeAll<GameObject>().Length;

            _ = ServiceLocator.Current;
            _ = ServiceLocator.IsArmed;

            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Length, Is.EqualTo(objects));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }

        [Test]
        public void Load_ReturnsTheAuthoredRootConfig()
        {
            Assert.That(GameBoot.Load(), Is.Not.Null);
        }
    }
}
