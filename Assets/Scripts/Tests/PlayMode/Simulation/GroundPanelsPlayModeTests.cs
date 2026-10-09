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
    /// The ground grid and the Corpse marker on the real <c>Example</c> scene (epic #214). Both live in the
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

        private const int GroundRole = 7; // ContainerRole.Ground, as the scene stores it

        private static object Field(object target, string name)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    return field.GetValue(target);
            }

            throw new System.MissingFieldException(target.GetType().Name, name);
        }

        /// <summary>The scene's display bound to the ground: the grid beside the Ground Items List.</summary>
        private static Component GroundGrid() =>
            Named("InventoryContainerDisplay").Single(display => (int)Field(display, "role") == GroundRole);

        /// <summary>The slots of the grid that show a package.</summary>
        private static Component[] FilledSlots(Component grid) =>
            grid.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(c => c.GetType().Name == "GroundSlotDisplay" && ((CanvasGroup)Field(c, "itemGroup")).gameObject.activeSelf)
                .Cast<Component>().ToArray();

        private static float Alpha(Component slot) => ((CanvasGroup)Field(slot, "itemGroup")).alpha;

        private static Vector2Int CellOf(Component slot) => (Vector2Int)slot.GetType().GetProperty("Position").GetValue(slot);

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
            var grid = GroundGrid();
            var fade = Field(FirstSlot(grid), "fade");
            var fresh = (int)Field(fade, "fullOpacityCount");
            var step = (float)Field(fade, "fadeStep");

            Land(lootFlow, fresh + 2);
            yield return null;

            var slots = FilledSlots(grid);
            Assert.That(slots.Length, Is.EqualTo(lootFlow.Ground.StoredPackages.Count), "one slot per package");

            // Oldest first, the cells run oldest to newest: the newest few are opaque, then a step per drop.
            var alphas = lootFlow.Ground.CellsOldestFirst().Select(cell => Alpha(slots.Single(s => CellOf(s) == cell))).ToList();
            Assert.That(alphas.Skip(2), Is.All.EqualTo(1f), "the newest few are fully opaque");
            Assert.That(alphas[1], Is.EqualTo(1f - step).Within(1e-4f), "the first beyond them: one step");
            Assert.That(alphas[0], Is.EqualTo(1f - (2 * step)).Within(1e-4f), "the next: two steps");

            // The Ground Items List lists the same drops oldest first and fades them by the same rule.
            var rows = Named("GroundItemsPanel").Single().GetComponentsInChildren<MonoBehaviour>()
                .Where(c => c.GetType().Name == "GroundItemSlotDisplay")
                .Select(row => row.GetComponent<CanvasGroup>().alpha).ToList();
            Assert.That(rows, Is.EqualTo(alphas).Within(1e-4f), "one row per drop, faded like its slot");
        }

        private static Component FirstSlot(Component grid) =>
            grid.GetComponentsInChildren<MonoBehaviour>(true).First(c => c.GetType().Name == "GroundSlotDisplay");

        [UnityTest]
        public IEnumerator APickedUpPackage_EmptiesItsSlot_AndLeavesTheOthersWhereTheyLay()
        {
            SimulationService.Instance.Send(_locations[0]);
            var lootFlow = SimulationService.Instance.LootFlow;
            var grid = GroundGrid();

            Land(lootFlow, 3);
            yield return null;
            var before = FilledSlots(grid).Select(CellOf).ToList();

            Assert.That(lootFlow.PickUpFromGround(lootFlow.GroundDrops[1].Item), Is.True, "the bag has room");
            yield return null;
            var after = FilledSlots(grid).Select(CellOf).ToList();

            Assert.That(after.Count, Is.EqualTo(before.Count - 1));
            Assert.That(after.Except(before), Is.Empty, "every package still lies in its cell");
        }

        [UnityTest]
        public IEnumerator TheGrid_EmptiesWhenTheRunEnds()
        {
            SimulationService.Instance.Send(_locations[0]);
            Land(SimulationService.Instance.LootFlow, 2);
            var grid = GroundGrid();

            _ = SimulationService.Instance.Recall();
            yield return null;

            Assert.That(FilledSlots(grid), Is.Empty, "a Drop on the ground when the Run ends is gone");
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
