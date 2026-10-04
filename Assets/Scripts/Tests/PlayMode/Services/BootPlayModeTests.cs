using NUnit.Framework;
using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The one thing the EditMode suite cannot reach: that the real <c>[RuntimeInitializeOnLoadMethod]</c>
    /// entry points fire. EditMode tests call <c>Arm</c> and <c>Reset</c> directly, so a renamed method or a
    /// dropped attribute would leave them green while a bare scene booted with no services and no tick.
    /// Run with the PlayMode filter; <c>Run All</c> in the EditMode tab does not include it.
    /// </summary>
    public sealed class BootPlayModeTests
    {
        [Test]
        public void TheBootRanBeforeTheFirstScene_AndLeftNoObjectBehind()
        {
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(ServiceLocator.IsArmed, Is.True, "the locator is armed by BeforeSceneLoad");
            Assert.That(GameLoop.IsInstalled, Is.True, "the loop system is installed by the boot");

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
                Assert.That(go.name.ToLowerInvariant(), Does.Not.Contain("runner").And.Not.Contain("gameloop"), go.name);
        }

        [Test]
        public void TheBootBuiltTheItemService_FromTheAuthoredConfig_AndNoItemProviderIsInTheScene()
        {
            var items = ItemService.Instance;

            Assert.That(items.Catalog, Is.Not.Null);
            Assert.That(items.MintCurrency(CurrencyType.Copper), Is.Not.Null);
            Assert.That(items.GetIcon(CurrencyType.Copper), Is.Not.Null);

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
                Assert.That(go.name, Is.Not.EqualTo("ITEM_PROVIDER"));
        }
    }
}
