using System.Collections;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The Field map's Location toggles on the real <c>Example</c> scene show the hero's Unlock States (issue #337,
    /// ADR-0023): Hidden is not shown, Locked is shown greyed out and does nothing on click, Active and Complete
    /// work. They follow a Location's Complete and a hero load. The toggles live in the predefined assembly, so they
    /// are read by type name; "shown" and "greyed out" are the alpha and raycast flags of the toggle's group, and
    /// "works" is a click that Sends or Relocates the hero there.
    /// </summary>
    public sealed class LocationTogglesPlayModeTests
    {
        private const string Thornwood = "location.thornwood";
        private const string Ashfall = "location.ashfall";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (SimulationService.Instance.Run.Phase == RunPhase.InField)
                _ = SimulationService.Instance.Return();

            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);
        }

        private static ISimulationService Simulation => SimulationService.Instance;

        private static MonoBehaviour ToggleOf(string id) =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(c => c.GetType().Name == "LocationToggle"
                            && ((Unlockable)c.GetType().GetProperty("Location").GetValue(c)).Id == id);

        private static CanvasGroup GroupOf(string id) => ToggleOf(id).GetComponent<CanvasGroup>();

        private static bool IsSeen(string id) => GroupOf(id).alpha > 0f && GroupOf(id).blocksRaycasts;

        private static bool IsGreyedOut(string id) => GroupOf(id).alpha < 1f;

        // A click that goes through has selected the toggle's Location on the hero, with the Run out in the Field.
        private static bool ClickGoesThrough(string id)
        {
            var toggle = ToggleOf(id);
            toggle.GetType().GetMethod("OnPointerClick").Invoke(toggle, new object[] { new PointerEventData(EventSystem.current) });

            return Session.Instance.Hero.SelectedLocation?.Id == id && Simulation.Run.Phase == RunPhase.InField;
        }

        private static void CompleteThornwood()
        {
            Session.Instance.Hero.IsInvincible = true;
            Simulation.Send(GameBoot.Load().Locations.First(l => l.Id == Thornwood));

            for (var tick = 0; tick < 20000 && Simulation.Run.Encounter.Phase != SimulationPhase.Complete; tick++)
                Simulation.Tick(0.1f);

            Assert.That(Simulation.Run.Encounter.Phase, Is.EqualTo(SimulationPhase.Complete), "premise: the Population cleared");
        }

        [UnityTest]
        public IEnumerator AFreshHero_SeesThornwoodUsable_AndAshfallShownButGreyedOut()
        {
            yield return null;

            Assert.That(IsSeen(Thornwood) && !IsGreyedOut(Thornwood), Is.True, "Thornwood is open");
            Assert.That(IsSeen(Ashfall), Is.True, "Ashfall is shown");
            Assert.That(IsGreyedOut(Ashfall), Is.True, "greyed out");
        }

        [UnityTest]
        public IEnumerator AGreyedOutLocation_DoesNothingOnClick()
        {
            yield return null;

            Assert.That(ClickGoesThrough(Ashfall), Is.False);
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InTown));
        }

        [UnityTest]
        public IEnumerator AnActiveLocation_WorksOnClick()
        {
            yield return null;

            Assert.That(ClickGoesThrough(Thornwood), Is.True);
        }

        [UnityTest]
        public IEnumerator CompletingThornwood_MakesAshfallUsable_WithoutLeavingTheFieldMap()
        {
            yield return null;
            CompleteThornwood();
            yield return null;

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InField), "premise: still out in the Field");
            Assert.That(IsGreyedOut(Ashfall), Is.False);
            Assert.That(ClickGoesThrough(Ashfall), Is.True);
        }

        [UnityTest]
        public IEnumerator LoadingAnotherHero_ShowsThatHerosStates()
        {
            yield return null;
            var veteran = Session.Instance.Hero;
            veteran.MarkCompleted(Thornwood);
            var save = HeroMapper.ToDto(veteran, "veteran", "Veteran", Simulation.Locations);

            Assert.That(Session.Instance.TryLoad(GameBoot.Load().DefaultHero), Is.True, "premise: a fresh hero loads");
            Assert.That(IsGreyedOut(Ashfall), Is.True, "the fresh hero has not opened Ashfall");

            Assert.That(Session.Instance.TryLoad(save, Simulation.Locations, out _), Is.True);
            Assert.That(IsGreyedOut(Ashfall), Is.False, "the veteran has");
            Assert.That(ClickGoesThrough(Ashfall), Is.True);
        }

        [UnityTest]
        public IEnumerator AHiddenLocation_IsNotShown_AndDoesNothing()
        {
            yield return null;

            var toggle = ToggleOf(Ashfall);
            toggle.GetType().GetMethod("Show", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(toggle, new object[] { UnlockState.Hidden });

            Assert.That(IsSeen(Ashfall), Is.False);
            Assert.That(ClickGoesThrough(Ashfall), Is.False);
        }
    }
}
