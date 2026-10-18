using System.Collections;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Simulation
{
    /// <summary>
    /// The Combat Panel follows the Inventory Context (epic #359) on the real <c>Example</c> scene: the Field face
    /// appearing requests nothing, so the Combat Panel is up and the Hero Panel is not, and opening the hero's bag
    /// and closing it again brings the Combat Panel back with no request. The panels live in the predefined
    /// assembly, so they are read by type name.
    /// </summary>
    public sealed class CombatPanelPlayModeTests
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

        // The SidePanel authored with the context, by the scene's stored enum value (Hero = 1).
        private static MonoBehaviour SidePanelOf(int context) =>
            Named("SidePanel").Single(panel =>
                (int)panel.GetType().GetField("inventoryContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(panel) == context);

        private static IEnumerator Until(MonoBehaviour panel, string flag, string because)
        {
            for (var frame = 0; !Flag(panel, flag) && frame < FadeFrames; frame++)
                yield return null;

            Assert.That(Flag(panel, flag), Is.True, because);
        }

        [UnityTest]
        public IEnumerator TheFieldFaceAppearing_OpensTheCombatPanel_AndNotTheHeroPanel()
        {
            var face = Named("FieldFacePanel").Single();
            face.GetType().GetMethod("ToggleState").Invoke(face, new object[] { true });

            yield return Until(SidePanelOf(5), "IsExtended", "the Combat Panel is up on None");
            Assert.That(InventoryService.Instance.ActiveContext, Is.EqualTo(InventoryContext.None), "the face requested nothing");
            Assert.That(Flag(SidePanelOf(1), "IsCollapsed"), Is.True, "the Hero Panel stays closed until asked for");
        }

        [UnityTest]
        public IEnumerator OpeningTheHeroPanelAndClosingIt_BringsTheCombatPanelBack()
        {
            var face = Named("FieldFacePanel").Single();
            face.GetType().GetMethod("ToggleState").Invoke(face, new object[] { true });
            yield return Until(SidePanelOf(5), "IsExtended", "premise: the Combat Panel is up");

            InventoryService.Instance.SetContext(InventoryContext.Hero);
            yield return Until(SidePanelOf(1), "IsExtended", "the Hero Panel opens");
            yield return Until(SidePanelOf(5), "IsCollapsed", "the Combat Panel gives way on Hero");

            InventoryService.Instance.CloseContext();
            yield return Until(SidePanelOf(5), "IsExtended", "closing the bag brings the Combat Panel back by itself");
            yield return Until(SidePanelOf(1), "IsCollapsed", "the Hero Panel closes");
        }
    }
}
