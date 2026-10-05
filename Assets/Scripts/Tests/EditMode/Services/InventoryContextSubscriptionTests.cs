using NUnit.Framework;
using Submodules.Utility.Services;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The edge's Inventory Context subscription (issue #116): the guarded, detach-before-attach
    /// subscribe the views and the drag cursor share, over the booted locator. It replaces
    /// <c>InventoryProvider.TrySubscribeContextChanged</c>, so the contract that facade kept is
    /// held here: one subscription however often it is made, a no-op while nothing is armed.
    /// </summary>
    [TestFixture]
    public sealed class InventoryContextSubscriptionTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private readonly List<InventoryContext> seen = new();
        private GameConfig config;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Reset();
            GameLoop.Uninstall();
            GameLoop.Reset();

            seen.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Reset();
            GameLoop.Uninstall();

            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private void Handler(InventoryContext context) => seen.Add(context);

        private void Boot()
        {
            config = TestGameConfig.Create(created);
            GameBoot.Arm(config);
        }

        [Test]
        public void WithNothingArmed_SubscribingDoesNothing_AndUnsubscribingDoesNotThrow()
        {
            Assert.That(InventoryService.TrySubscribeContextChanged(Handler, out _), Is.False);
            Assert.DoesNotThrow(() => InventoryService.UnsubscribeContextChanged(Handler));
        }

        [Test]
        public void Subscribing_ReportsTheActiveContext_AndHearsEveryChange()
        {
            Boot();
            Session.Instance.World.Context.Set(InventoryContext.Vendor);

            Assert.That(InventoryService.TrySubscribeContextChanged(Handler, out var active), Is.True);
            Assert.That(active, Is.EqualTo(InventoryContext.Vendor));

            Session.Instance.World.Context.Close();

            Assert.That(seen, Is.EqualTo(new[] { InventoryContext.None }));
        }

        [Test]
        public void SubscribingTwice_HearsEachChangeOnce()
        {
            Boot();

            _ = InventoryService.TrySubscribeContextChanged(Handler, out _);
            _ = InventoryService.TrySubscribeContextChanged(Handler, out _);

            Session.Instance.World.Context.Set(InventoryContext.Vendor);

            Assert.That(seen, Has.Count.EqualTo(1));
        }

        [Test]
        public void Unsubscribing_StopsTheHandlerHearing()
        {
            Boot();
            _ = InventoryService.TrySubscribeContextChanged(Handler, out _);

            InventoryService.UnsubscribeContextChanged(Handler);
            Session.Instance.World.Context.Set(InventoryContext.Vendor);

            Assert.That(seen, Is.Empty);
        }

        [Test]
        public void ASubscription_SurvivesAHeroLoad_AndIsToldTheNewWorldIsClosed()
        {
            Boot();
            Session.Instance.World.Context.Set(InventoryContext.Vendor);
            _ = InventoryService.TrySubscribeContextChanged(Handler, out _);

            Assert.That(Session.Instance.TryLoad(config.DefaultHero), Is.True);

            Assert.That(seen, Is.EqualTo(new[] { InventoryContext.None }), "told once on the load");

            Session.Instance.World.Context.Set(InventoryContext.Healer);

            Assert.That(seen, Is.EqualTo(new[] { InventoryContext.None, InventoryContext.Healer }), "and still hears the new World");
        }
    }
}
