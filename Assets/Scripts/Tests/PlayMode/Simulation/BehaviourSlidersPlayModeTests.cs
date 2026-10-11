using System.Collections;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Simulation
{
    /// <summary>
    /// The Combat Panel's behaviour sliders on the real <c>Example</c> scene: a slider moved while a Run is on
    /// writes the Hero's live <see cref="HeroBehaviour"/>, which is what the sim reads. The panel lives in the
    /// predefined assembly, out of a test assembly's reach, so it is read by type name.
    /// </summary>
    public sealed class BehaviourSlidersPlayModeTests
    {
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
            if (SimulationService.Instance.Run.Phase == RunPhase.InField)
                _ = SimulationService.Instance.Recall();

            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);
        }

        private static Component Panel() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Single(c => c.GetType().Name == "BehaviourSlidersPanel");

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

        /// <summary>Moves a slider as a drag would and returns where it landed: a stepped slider snaps.</summary>
        private static float Drag(string sliderField, float value)
        {
            var slider = Field(Panel(), sliderField);
            _ = slider.GetType().GetMethod("SetValue").Invoke(slider, new object[] { value });
            return (float)slider.GetType().GetProperty("Value").GetValue(slider);
        }

        private static string Diagnose()
        {
            var panel = Panel();
            var slider = Field(panel, "retreatHealthSlider");
            var changed = (System.Delegate)Field(slider, "OnValueChanged");
            var group = panel.GetComponent<CanvasGroup>();
            var radio = Field(panel, "<RadioGroup>k__BackingField");
            var active = radio?.GetType().GetProperty("ActiveMember")?.GetValue(radio) as Component;
            var bound = Field(panel, "_behaviour");
            return $"alpha={group.alpha} interactable={group.interactable} blocks={group.blocksRaycasts} "
                   + $"panelActiveInHierarchy={panel.gameObject.activeInHierarchy} _behaviour={(bound == null ? "null" : "set")} "
                   + $"sameAsHero={ReferenceEquals(bound, Session.Instance.Hero.Behaviour)} "
                   + $"listeners={(changed == null ? 0 : changed.GetInvocationList().Length)} activeMember={(active ? active.name : "none")}";
        }

        private static void AssertEverySliderWritesTheLiveBehaviour(string when)
        {
            var behaviour = Session.Instance.Hero.Behaviour;

            var landed = Drag("retreatHealthSlider", 0.77f);
            Assert.That(behaviour.RetreatHealthFraction, Is.EqualTo(landed).Within(1e-4f), $"retreat health, {when}: {Diagnose()}");

            landed = Drag("recallBagFillSlider", 0.33f);
            Assert.That(behaviour.RecallBagFillFraction, Is.EqualTo(landed).Within(1e-4f), $"recall bag fill, {when}");

            landed = Drag("resourceReserveSlider", 0.61f);
            Assert.That(behaviour.CastThreshold, Is.EqualTo(landed).Within(1e-4f), $"cast threshold, {when}");

            landed = Drag("originWeightSlider", 0.19f);
            Assert.That(behaviour.OriginWeight, Is.EqualTo(landed).Within(1e-4f), $"origin weight, {when}");

            landed = Drag("simSpeedSlider", 0.5f);
            Assert.That(behaviour.SimSpeed, Is.EqualTo(HeroBehaviour.SliderToSimSpeed(landed)).Within(1e-4f), $"sim speed, {when}");

            Drag("lootFilterSlider", 1f);
            Assert.That(behaviour.LootFilterMinimum, Is.EqualTo(HeroBehaviour.RaritySteps.Last()), $"loot filter, {when}");
        }

        [UnityTest]
        public IEnumerator TheSliders_WriteTheLiveBehaviour_WhileARunIsOn()
        {
            SimulationService.Instance.Send(_locations[0]);
            yield return null;
            yield return null;

            AssertEverySliderWritesTheLiveBehaviour("first Run");
        }

        [UnityTest]
        public IEnumerator TheSliders_WriteTheLiveBehaviour_OnTheSecondRun()
        {
            SimulationService.Instance.Send(_locations[0]);
            yield return null;
            _ = SimulationService.Instance.Recall();
            yield return null;

            SimulationService.Instance.Send(_locations[0]);
            yield return null;
            yield return null;

            AssertEverySliderWritesTheLiveBehaviour("second Run");
        }

        [UnityTest]
        public IEnumerator TheSliders_WriteTheLiveBehaviour_AfterAHeroLoad()
        {
            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);
            yield return null;

            SimulationService.Instance.Send(_locations[0]);
            yield return null;
            yield return null;

            AssertEverySliderWritesTheLiveBehaviour("after a hero load");
        }
    }
}
