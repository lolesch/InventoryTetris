using NUnit.Framework;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The progression registry refuses to build from a broken Unlock graph (ADR-0023), the way the
    /// Location registry refuses a duplicate id, so a broken graph never ships silently.
    /// </summary>
    [TestFixture]
    public sealed class ProgressionRegistryTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;

        [SetUp]
        public void SetUp() => config = TestGameConfig.Create(created);

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private LocationConfig Location(string id) => TestLocations.Create(config, created, id);

        private static void Unlock(Unlockable source, Unlockable target, bool whenComplete = true)
        {
            var so = new SerializedObject(source);
            var links = so.FindProperty(whenComplete ? "unlocksWhenComplete" : "unlocksWhenActive");
            links.arraySize++;
            links.GetArrayElementAtIndex(links.arraySize - 1).objectReferenceValue = target;
            _ = so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void ASoundGraph_Builds()
        {
            var root = Location("root");
            var child = Location("child");
            Unlock(root, child);

            Assert.DoesNotThrow(() => new ProgressionRegistry(new[] { root, child }));
        }

        [Test]
        public void TwoUnlockablesWithOneId_Throw_NamingTheId()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => new ProgressionRegistry(new[] { Location("thornwood"), Location("thornwood") }));

            Assert.That(exception.Message, Does.Contain("thornwood"));
        }

        [Test]
        public void ALoopReachedFromARoot_Throws_NamingAnUnlockableInIt()
        {
            var root = Location("root");
            var first = Location("first");
            var second = Location("second");
            Unlock(root, first);
            Unlock(first, second);
            Unlock(second, first, whenComplete: false);

            var exception = Assert.Throws<InvalidOperationException>(
                () => new ProgressionRegistry(new[] { root, first, second }));

            Assert.That(exception.Message, Does.Contain("first").Or.Contain("second"));
        }

        [Test]
        public void AnUnlockableNoRootReaches_Throws_NamingIt()
        {
            var root = Location("root");
            var island = Location("island");
            var other = Location("other");
            Unlock(island, other);
            Unlock(other, island);

            var exception = Assert.Throws<InvalidOperationException>(
                () => new ProgressionRegistry(new[] { root, island, other }));

            Assert.That(exception.Message, Does.Contain("island").Or.Contain("other"));
        }

        [Test]
        public void ATownStopThatUnlocksWhenComplete_Throws_NamingIt()
        {
            var root = Location("root");
            var vendor = TestTownStops.Create(created, "town.vendor");
            var child = Location("child");
            Unlock(root, vendor);
            Unlock(vendor, child);

            var exception = Assert.Throws<InvalidOperationException>(
                () => new ProgressionRegistry(new Unlockable[] { root, vendor, child }));

            Assert.That(exception.Message, Does.Contain("town.vendor"));
        }

        [Test]
        public void ATownStopThatUnlocksWhenActive_Builds()
        {
            var root = Location("root");
            var vendor = TestTownStops.Create(created, "town.vendor");
            var child = Location("child");
            Unlock(root, vendor);
            Unlock(vendor, child, whenComplete: false);

            Assert.DoesNotThrow(() => new ProgressionRegistry(new Unlockable[] { root, vendor, child }));
        }

        [Test]
        public void ASelfLoop_Throws_NamingIt()
        {
            var root = Location("root");
            var spiral = Location("spiral");
            Unlock(root, spiral);
            Unlock(spiral, spiral);

            var exception = Assert.Throws<InvalidOperationException>(
                () => new ProgressionRegistry(new[] { root, spiral }));

            Assert.That(exception.Message, Does.Contain("spiral"));
        }
    }
}
