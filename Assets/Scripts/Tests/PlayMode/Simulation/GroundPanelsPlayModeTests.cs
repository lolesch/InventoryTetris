using System.Collections;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Simulation
{
    /// <summary>
    /// The floor grid and the Corpse marker on the real <c>Example</c> scene (epic #214). Both live in the
    /// predefined assembly, out of a test assembly's reach, so they are read by type name; the boot's
    /// SimulationService is what feeds them. Run with the PlayMode filter; <c>Run All</c> in the EditMode tab
    /// does not include it.
    /// </summary>
    public sealed class GroundPanelsPlayModeTests
    {
        private const int DeathTimeoutTicks = 100;

        private LocationConfig[] _locations;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _locations = GameBoot.Load().Locations.ToArray();

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            // The services outlive the test with domain reload off: end its Run and hand the next test a fresh Hero.
            if (SimulationService.Instance.Run.Phase == RunPhase.InField)
                _ = SimulationService.Instance.Recall();

            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);
        }

        private static Component[] Named(string typeName) =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(c => c.GetType().Name == typeName).Cast<Component>().ToArray();

        private static Component[] ActiveEntries(Component panel) =>
            panel.GetComponentsInChildren<MonoBehaviour>(false)
                .Where(c => c.GetType().Name == "GroundItemSlotDisplay").Cast<Component>().ToArray();

        private static float Alpha(Component entry) => entry.GetComponent<CanvasGroup>().alpha;

        private static (Vector2, Vector2) Anchors(Component entry)
        {
            var rect = (RectTransform)entry.transform;
            return (rect.anchorMin, rect.anchorMax);
        }

        private static void Land(LootFlow lootFlow, int count)
        {
            for (var i = 0; i < count; i++)
                Assert.That(lootFlow.PlaceOnGround(new Package(null, ItemService.Instance.RollEquipment(), 1u)), Is.True);
        }

        [UnityTest]
        public IEnumerator TheGrid_DrawsEachPackage_AndFadesByAgeRank()
        {
            SimulationService.Instance.Send(_locations[0]);
            var lootFlow = SimulationService.Instance.LootFlow;
            var panel = Named("GroundGridPanel").Single();
            var oldestAlpha = (float)panel.GetType()
                .GetField("oldestAlpha", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(panel);

            Land(lootFlow, 3);
            yield return null;

            var entries = ActiveEntries(panel);
            Assert.That(entries.Length, Is.EqualTo(lootFlow.Ground.StoredPackages.Count), "one entry per package");
            Assert.That(entries.Max(Alpha), Is.EqualTo(1f), "the newest is fully opaque");
            Assert.That(entries.Min(Alpha), Is.EqualTo(oldestAlpha).Within(1e-4f), "the oldest is at the serialized minimum");
        }

        [UnityTest]
        public IEnumerator ARemovedPackage_DoesNotMoveTheOtherEntries()
        {
            SimulationService.Instance.Send(_locations[0]);
            var lootFlow = SimulationService.Instance.LootFlow;
            var panel = Named("GroundGridPanel").Single();

            Land(lootFlow, 3);
            yield return null;
            var before = ActiveEntries(panel).Select(Anchors).ToList();

            Assert.That(lootFlow.PickUpFromGround(lootFlow.GroundDrops[1].Item), Is.True, "the bag has room");
            yield return null;
            var after = ActiveEntries(panel).Select(Anchors).ToList();

            Assert.That(after.Count, Is.EqualTo(before.Count - 1));
            Assert.That(after.Except(before), Is.Empty, "every entry still sits where it did");
        }

        [UnityTest]
        public IEnumerator TheCorpseMarker_ShowsAtTheLocationOfDeath_UntilItIsRecovered()
        {
            var markers = Named("CorpseMarker");
            Assert.That(markers.Length, Is.EqualTo(_locations.Length), "one marker per Location toggle");

            Image MarkerAt(LocationConfig location) =>
                markers.First(m => ToggleLocation(m) == location).GetComponent<Image>();

            SimulationService.Instance.Send(_locations[0]);
            yield return null;
            Assert.That(MarkerAt(_locations[0]).enabled, Is.False, "no Corpse yet");

            Session.Instance.Hero.GetResource(StatName.Health).DepleteCurrent();

            // The scene's own loop may be paused or slow; the service's Tick is what a frame would call.
            for (var tick = 0; SimulationService.Instance.Run.Phase == RunPhase.InField && tick < DeathTimeoutTicks; tick++)
                SimulationService.Instance.Tick(0.5f);

            yield return null;

            Assert.That(SimulationService.Instance.Run.Phase, Is.EqualTo(RunPhase.InTown), "the Hero died and went home");
            Assert.That(MarkerAt(_locations[0]).enabled, Is.True, "the Corpse lies where the Hero fell");
            Assert.That(MarkerAt(_locations[1]).enabled, Is.False, "and nowhere else");

            SimulationService.Instance.Send(_locations[0]);
            yield return null;
            Assert.That(MarkerAt(_locations[0]).enabled, Is.False, "recovered");
        }

        private static LocationConfig ToggleLocation(Component marker)
        {
            var toggle = marker.GetComponentsInParent<MonoBehaviour>(true).First(c => c.GetType().Name == "LocationToggle");
            return (LocationConfig)toggle.GetType().GetProperty("Location").GetValue(toggle);
        }
    }
}
