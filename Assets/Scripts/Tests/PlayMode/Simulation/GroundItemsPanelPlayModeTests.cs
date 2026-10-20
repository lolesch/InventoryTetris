using System.Collections;
using System.Linq;
using NUnit.Framework;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Simulation
{
    /// <summary>
    /// The Ground Items panel (epic #359) on the real <c>Example</c> scene: up whenever no Town Stop is open, on either
    /// map, and the sim settings are their own panel behind their own HUD toggle. The panels live in the predefined
    /// assembly, so they are read by type name.
    /// </summary>
    public sealed class GroundItemsPanelPlayModeTests
    {
        private const int FadeFrames = 600;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            InventoryService.Instance.CloseContext();
            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);
        }

        private static MonoBehaviour[] Named(string typeName) =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(c => c.GetType().Name == typeName).ToArray();

        private static bool Flag(object target, string name) =>
            (bool)target.GetType().GetProperty(name).GetValue(target);

        // The SidePanel authored with the context.
        private static MonoBehaviour SidePanelOf(InventoryContext context) =>
            Named("SidePanel").Single(panel =>
                (int)panel.GetType().GetField("inventoryContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(panel) == (int)context);

        private static IEnumerator Until(MonoBehaviour panel, string flag, string because)
        {
            for (var frame = 0; !Flag(panel, flag) && frame < FadeFrames; frame++)
                yield return null;

            Assert.That(Flag(panel, flag), Is.True, because);
        }

        [UnityTest]
        public IEnumerator OnNone_TheGroundItemsPanelIsUp_AndTheHeroPanelIsNot()
        {
            yield return Until(SidePanelOf(InventoryContext.Ground), "IsExtended", "the Ground Items panel is up on None");

            Assert.That(InventoryService.Instance.ActiveContext, Is.EqualTo(InventoryContext.None));
            Assert.That(Flag(SidePanelOf(InventoryContext.Hero), "IsCollapsed"), Is.True, "the Hero Panel stays closed until asked for");
        }

        [UnityTest]
        public IEnumerator TheSimSettings_AreTheirOwnPanel_OpenedByTheirOwnHudToggle()
        {
            var settings = Named("BehaviourSlidersPanel").Single();
            var toggle = Named("PanelToggle").Single(t => t.name == "SimSettingsToggle");
            yield return Until(settings, "IsCollapsed", "premise: the settings start closed");

            ((IPointerClickHandler)toggle).OnPointerClick(
                new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            yield return Until(settings, "IsExtended", "the toggle opens the sim settings");
            Assert.That(InventoryService.Instance.ActiveContext, Is.EqualTo(InventoryContext.None), "without any Inventory Context");

            ((IPointerClickHandler)toggle).OnPointerClick(
                new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            yield return Until(settings, "IsCollapsed", "and closes them again");
        }
    }
}
