using NUnit.Framework;
using Submodules.Utility.Services;
using System;
using System.Collections;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// Escape on the real <c>Example</c> scene (issue #248): through a virtual keyboard, the service and the
    /// Cancel chain's scene layers, a held Package goes home first, then one press closes every panel. Boots
    /// again over the fixture's isolated Input System, like <see cref="HotkeyPlayModeTests"/>.
    /// </summary>
    public sealed class CancelPlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;

        private static InventoryContext Context => ServiceLocator.Get<IInventoryService>().ActiveContext;

        private IEnumerator OpenExample()
        {
            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load());

            keyboard = InputSystem.AddDevice<Keyboard>();

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void CloseWhatTheTestOpened()
        {
            if (ServiceLocator.IsArmed)
                ServiceLocator.Get<IInventoryService>().CloseContext();
        }

        // Input time, which the fixture holds still: a press within the repeat guard of the last is one press.
        private IEnumerator Tap(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            currentTime += 1.0;
            PressAndRelease(key);
            yield return null;
        }

        private static Type DragProviderType() =>
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()).First(type => type.Name == "DragProvider");

        [UnityTest]
        public IEnumerator Escape_ClosesEveryOpenPanelAtOnce()
        {
            yield return OpenExample();

            yield return Tap(keyboard.iKey);
            Assert.That(Context, Is.EqualTo(InventoryContext.Hero), "premise: the Hero Panel is open");

            yield return Tap(keyboard.escapeKey);

            Assert.That(Context, Is.EqualTo(InventoryContext.None));
        }

        [UnityTest]
        public IEnumerator Escape_PutsAHeldPackageBackFirst_AndClosesNothingElse()
        {
            yield return OpenExample();
            yield return Tap(keyboard.iKey);

            var hero = ServiceLocator.Get<ISession>().Hero;
            var potion = ServiceLocator.Get<IItemService>().Catalog.OfCategory(ItemCategory.Consumable).First();
            var item = new ItemInstance(potion.Id, ItemRarity.Common, 1, null);
            var drag = UnityEngine.Object.FindFirstObjectByType(DragProviderType());
            DragProviderType().GetMethod("ReplacePackage")!.Invoke(drag,
                new object[] { new Package(null, item, 1u), new PackageOrigin(hero.Inventory, Vector2Int.zero) });
            var isDragging = DragProviderType().GetProperty("IsDragging")!;
            Assert.That((bool)isDragging.GetValue(drag), Is.True, "premise: a Package is on the cursor");

            yield return Tap(keyboard.escapeKey);

            Assert.That((bool)isDragging.GetValue(drag), Is.False, "the Package went home");
            Assert.That(hero.Inventory.StoredPackages.Values.Any(package => package.Item == item), Is.True);
            Assert.That(Context, Is.EqualTo(InventoryContext.Hero), "the same press closed nothing");

            yield return Tap(keyboard.escapeKey);

            Assert.That(Context, Is.EqualTo(InventoryContext.None), "the next press closes the panel");
        }
    }
}
