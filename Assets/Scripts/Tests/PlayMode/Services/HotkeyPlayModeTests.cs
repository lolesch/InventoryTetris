using NUnit.Framework;
using Submodules.Utility.Services;
using System.Collections;
using System.Linq;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The input wiring on the real <c>Example</c> scene (issue #69): a virtual keyboard and mouse through the
    /// Input System's own test fixture, so a hotkey and a click both arrive the way a player's do - through the
    /// service, the toggles' subscriptions and the scene's <c>InputSystemUIInputModule</c>. The fixture isolates
    /// the Input System, which drops the boot's actions, so each test boots again over it. Run with the PlayMode
    /// filter; <c>Run All</c> in the EditMode tab does not include it.
    /// </summary>
    public sealed class HotkeyPlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private Mouse mouse;

        private static InventoryContext Context => ServiceLocator.Get<IInventoryService>().ActiveContext;

        private IEnumerator OpenExample()
        {
            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load());

            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void CloseWhatTheTestOpened()
        {
            // The services outlive the test with domain reload off.
            if (ServiceLocator.IsArmed)
                ServiceLocator.Get<IInventoryService>().CloseContext();
        }

        [UnityTest]
        public IEnumerator EachPanelHotkey_OpensItsPanel_AndTheSameKeyClosesIt()
        {
            yield return OpenExample();

            // Not the Vendor's: it is Locked on a fresh hero, so neither its key nor its click reaches it.
            foreach (var (key, context) in new[]
            {
                (keyboard.sKey, InventoryContext.Stash),
                (keyboard.hKey, InventoryContext.Healer),
                (keyboard.iKey, InventoryContext.Hero),
            })
            {
                // Input time, which the fixture holds still: a press within the repeat guard of the last is one press.
                currentTime += 1.0;
                PressAndRelease(key);
                yield return null;
                Assert.That(Context, Is.EqualTo(context), $"{key.displayName} opens {context}");

                currentTime += 1.0;
                PressAndRelease(key);
                yield return null;
                Assert.That(Context, Is.EqualTo(InventoryContext.None), $"{key.displayName} again closes it");
            }
        }

        [UnityTest]
        public IEnumerator OnAFreshHero_TheVendorIsGreyedOut_AndItsKeyDoesNothing()
        {
            yield return OpenExample();

            var vendor = GameObject.Find("VendorToggle").GetComponent<Selectable>();
            Assert.That(vendor.interactable, Is.False, "greyed out");

            currentTime += 1.0;
            PressAndRelease(keyboard.vKey);
            yield return null;

            Assert.That(Context, Is.EqualTo(InventoryContext.None), "the key does nothing while the Vendor is Locked");
            Assert.That(GameObject.Find("StashToggle").GetComponent<Selectable>().IsInteractable(), Is.True);
            Assert.That(GameObject.Find("HealerToggle").GetComponent<Selectable>().IsInteractable(), Is.True);
        }

        [UnityTest]
        public IEnumerator CompletingThornwood_OpensTheVendorInTheSameRun_AndItsKeyOpensIt()
        {
            yield return OpenExample();

            var vendor = GameObject.Find("VendorToggle").GetComponent<Selectable>();
            var simulation = ServiceLocator.Get<ISimulationService>();
            Session.Instance.Hero.IsInvincible = true;

            simulation.Send(GameBoot.Load().Locations.First(location => location.Id == "location.thornwood"));
            for (var i = 0; i < 20000 && simulation.Run.Encounter.Phase != SimulationPhase.Complete; i++)
                simulation.Tick(0.1f);

            Assert.That(simulation.Run.Encounter.Phase, Is.EqualTo(SimulationPhase.Complete), "premise: the Population cleared");
            Assert.That(vendor.interactable, Is.True, "open in the same Run, before any Return");

            _ = simulation.Return();
            yield return null;

            currentTime += 1.0;
            PressAndRelease(keyboard.vKey);
            yield return null;

            Assert.That(Context, Is.EqualTo(InventoryContext.Vendor));
        }

        [UnityTest]
        public IEnumerator AClickOnAToggle_ReachesItThroughTheUiModule()
        {
            yield return OpenExample();

            var toggle = (RectTransform)GameObject.Find("StashToggle").transform;
            var corners = new Vector3[4];
            toggle.GetWorldCorners(corners);

            Set(mouse.position, (Vector2)((corners[0] + corners[2]) * 0.5f));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            Assert.That(Context, Is.EqualTo(InventoryContext.Stash));
        }

        [UnityTest]
        public IEnumerator AHeldModifier_IsReadThroughTheService_AndThePointerFollowsTheMouse()
        {
            yield return OpenExample();

            Press(keyboard.leftShiftKey);
            Set(mouse.position, new Vector2(120f, 45f));
            yield return null;

            Assert.That(GameInput.IsHeld(GameAction.QuickMove), Is.True);
            Assert.That(GameInput.IsHeld(GameAction.SecondSlot), Is.True);
            Assert.That(GameInput.IsHeld(GameAction.HalveStack), Is.False);
            Assert.That(GameInput.PointerPosition, Is.EqualTo(new Vector2(120f, 45f)));

            Release(keyboard.leftShiftKey);
            yield return null;

            Assert.That(GameInput.IsHeld(GameAction.QuickMove), Is.False);
        }
    }
}
