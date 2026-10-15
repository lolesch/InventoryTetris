using NUnit.Framework;
using Submodules.Utility.Services;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The Cancel chain: each press goes to the first rank whose layer consumes it and stops there. Engine-free
    /// over a <see cref="FakeInput"/>; the one test that arms the boot proves the Context layer is installed.
    /// </summary>
    [TestFixture]
    public sealed class CancelChainTests
    {
        private FakeInput input;
        private CancelChain chain;
        private List<string> heard;

        [SetUp]
        public void SetUp()
        {
            input = new FakeInput();
            chain = new CancelChain(input);
            heard = new List<string>();
        }

        // A layer that records its name and answers whether it consumes.
        private System.Func<bool> Layer(string name, bool consumes) => () =>
        {
            heard.Add(name);
            return consumes;
        };

        [Test]
        public void ThePressGoesToTheTopRankFirst_AndStopsAtTheFirstThatConsumes()
        {
            _ = chain.Register(CancelRank.PauseMenu, Layer("pause", true));
            _ = chain.Register(CancelRank.Context, Layer("context", true));
            _ = chain.Register(CancelRank.Drag, Layer("drag", true));
            _ = chain.Register(CancelRank.Popup, Layer("popup", true));

            Assert.That(chain.Cancel(), Is.True);
            Assert.That(heard, Is.EqualTo(new[] { "drag" }), "one press never fires two layers");
        }

        [Test]
        public void ALayerThatDoesNotConsume_PassesThePressDown()
        {
            _ = chain.Register(CancelRank.Drag, Layer("drag", false));
            _ = chain.Register(CancelRank.Popup, Layer("popup", false));
            _ = chain.Register(CancelRank.Context, Layer("context", true));
            _ = chain.Register(CancelRank.PauseMenu, Layer("pause", true));

            Assert.That(chain.Cancel(), Is.True);
            Assert.That(heard, Is.EqualTo(new[] { "drag", "popup", "context" }));
        }

        [Test]
        public void WithNothingToConsume_TheChainSaysSo()
        {
            Assert.That(chain.Cancel(), Is.False, "an empty chain");

            _ = chain.Register(CancelRank.Drag, Layer("drag", false));

            Assert.That(chain.Cancel(), Is.False);
            Assert.That(heard, Is.EqualTo(new[] { "drag" }));
        }

        [Test]
        public void ALayerThatRegisters_IsHeard_AndOneThatLeaves_IsNot()
        {
            var layer = chain.Register(CancelRank.Popup, Layer("popup", true));
            _ = chain.Register(CancelRank.Context, Layer("context", true));

            _ = chain.Cancel();
            layer.Dispose();
            _ = chain.Cancel();

            Assert.That(heard, Is.EqualTo(new[] { "popup", "context" }));
        }

        [Test]
        public void WithinARank_TheLatestLayerIsAskedFirst()
        {
            _ = chain.Register(CancelRank.Popup, Layer("settings", false));
            _ = chain.Register(CancelRank.Popup, Layer("confirm", false));

            _ = chain.Cancel();

            Assert.That(heard, Is.EqualTo(new[] { "confirm", "settings" }), "the screen opened last closes first");
        }

        [Test]
        public void ALayerThatLeavesWhileItHandles_DoesNotDisturbThePress()
        {
            System.IDisposable self = null;
            self = chain.Register(CancelRank.Popup, () =>
            {
                heard.Add("popup");
                self.Dispose();
                return true;
            });

            Assert.That(chain.Cancel(), Is.True);
            Assert.That(chain.Cancel(), Is.False, "it closed itself, so it is gone");
            Assert.That(heard, Is.EqualTo(new[] { "popup" }));
        }

        [Test]
        public void OnlyTheCancelKey_PressesTheChain()
        {
            _ = chain.Register(CancelRank.Drag, Layer("drag", true));

            input.Press(GameAction.TogglePause);
            input.Press(GameAction.ToggleHeroPanel);
            Assert.That(heard, Is.Empty);

            input.Press(GameAction.Cancel);
            Assert.That(heard, Is.EqualTo(new[] { "drag" }));
        }

        [Test]
        public void WithNothingArmed_RegisteringALayerFromAViewIsInert()
        {
            ServiceLocator.Reset();

            Assert.That(CancelChain.TryRegister(CancelRank.Drag, Layer("drag", true)), Is.Null);
        }

        [Test]
        public void CancelIsNotOfferedToTheRebindingScreen()
        {
            Assert.That(RebindableActions.All, Has.No.Member(GameAction.Cancel));
            Assert.That(RebindableActions.All, Has.No.Member(GameAction.None));
            Assert.That(RebindableActions.All, Has.Member(GameAction.TogglePause));
            Assert.That(RebindableActions.All.Count, Is.EqualTo(System.Enum.GetValues(typeof(GameAction)).Length - 2));
        }

        [Test]
        public void TheBoot_InstallsTheChain_WithTheContextLayerThatClosesEveryPanelAtOnce()
        {
            var created = new List<UnityEngine.Object>();
            ServiceLocator.Reset();

            try
            {
                GameBoot.Arm(TestGameConfig.Create(created));
                var inventory = ServiceLocator.Get<IInventoryService>();
                var cancel = ServiceLocator.Get<ICancelChain>();
                inventory.SetContext(InventoryContext.Hero);
                Assert.That(inventory.ActiveContext, Is.Not.EqualTo(InventoryContext.None), "premise: a panel is open");

                Assert.That(cancel.Cancel(), Is.True);
                Assert.That(inventory.ActiveContext, Is.EqualTo(InventoryContext.None));
                Assert.That(cancel.Cancel(), Is.False, "nothing is left to peel");
            }
            finally
            {
                ServiceLocator.Reset();
                GameLoop.Uninstall();

                foreach (var asset in created.Where(asset => asset != null))
                    UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
