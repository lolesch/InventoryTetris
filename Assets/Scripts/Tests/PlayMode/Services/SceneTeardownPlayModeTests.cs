using NUnit.Framework;
using Submodules.Utility.Services;
using System;
using System.Collections;
using System.Linq;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// Issue #278: the input module resets its pointers as a scene unloads, which fires a pointer-exit on the
    /// hovered slot after the scene's <c>DragProvider</c> and <c>PreviewProvider</c> are gone. Asking them for
    /// <c>Instance</c> then made an unconfigured replacement whose <c>Awake</c> threw, and every PlayMode
    /// fixture that swapped scenes with a hovered slot failed on the next one. Headless the Game view is
    /// 640x480 and no panel is on screen, so the test fires the exit itself instead of hovering.
    /// </summary>
    public sealed class SceneTeardownPlayModeTests : InputTestFixture
    {
        [TearDown]
        public void CloseWhatTheTestOpened()
        {
            LogAssert.ignoreFailingMessages = false;

            // The services outlive the test with domain reload off.
            if (ServiceLocator.IsArmed)
                ServiceLocator.Get<IInventoryService>().CloseContext();
        }

        [UnityTest]
        public IEnumerator APointerExit_AfterTheProvidersAreDestroyed_MakesNoReplacement()
        {
            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load());

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;

            var slot = (Component)UnityEngine.Object.FindFirstObjectByType(TypeNamed("InventorySlotDisplay"));
            Assert.That(slot, Is.Not.Null, "premise: the scene has an inventory slot");

            var providers = new[] { "DragProvider", "PreviewProvider" };

            foreach (var name in providers)
                UnityEngine.Object.DestroyImmediate(UnityEngine.Object.FindFirstObjectByType(TypeNamed(name)));

            ExecuteEvents.Execute(slot.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);

            foreach (var name in providers)
                Assert.That(UnityEngine.Object.FindObjectsByType(TypeNamed(name), FindObjectsSortMode.None), Is.Empty,
                    $"a release on a teardown path must not conjure a {name}");

            // The slots that still poll the destroyed DragProvider in Update throw: not what this test is about.
            LogAssert.ignoreFailingMessages = true;

            yield return SceneManager.LoadSceneAsync("MainMenu");
        }

        private static Type TypeNamed(string name) =>
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()).First(type => type.Name == name);
    }
}
