using NUnit.Framework;
using Submodules.Utility.Services;
using System.Collections;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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

            // Not the Vendor's: its toggle component is switched off in the scene, so neither its key nor its
            // click ever reached it.
            foreach (var (key, context) in new[]
            {
                (keyboard.sKey, InventoryContext.Stash),
                (keyboard.hKey, InventoryContext.Healer),
                (keyboard.iKey, InventoryContext.Hero),
            })
            {
                PressAndRelease(key);
                yield return null;
                Assert.That(Context, Is.EqualTo(context), $"{key.displayName} opens {context}");

                PressAndRelease(key);
                yield return null;
                Assert.That(Context, Is.EqualTo(InventoryContext.None), $"{key.displayName} again closes it");
            }
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
