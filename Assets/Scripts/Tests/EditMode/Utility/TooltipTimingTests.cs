using NUnit.Framework;
using Submodules.Utility.UI;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Utility
{
    /// <summary>
    /// Drives <see cref="TooltipTiming{T}"/> by hand - <c>timing.Tick(dt)</c>, no scene, no
    /// <c>MonoBehaviour</c> - exactly the manual-tick style <see cref="TweenTests"/> use for
    /// <c>Tween</c>. Pins external behaviour only: whether <c>OnShow</c> / <c>OnHide</c> fired,
    /// how many times, and with what content.
    /// </summary>
    [TestFixture]
    public sealed class TooltipTimingTests
    {
        private const float ShowDelay = 0.5f;
        private const float HideDebounce = 0.1f;

        private static TooltipTiming<string> NewTiming() => new(ShowDelay, HideDebounce);

        [Test]
        public void Request_DoesNotShowSynchronously()
        {
            var shown = 0;
            var timing = NewTiming();
            timing.OnShow += _ => shown++;

            timing.Request("hint");

            Assert.That(shown, Is.Zero);
            Assert.That(timing.State, Is.EqualTo(TooltipVisibility.PendingShow));
        }

        [Test]
        public void Tick_PastTheShowDelay_FiresOnShowExactlyOnce_WithTheRequestedContent()
        {
            var received = default(string);
            var shown = 0;
            var timing = NewTiming();
            timing.OnShow += value => { received = value; shown++; };

            timing.Request("hint");
            timing.Tick(ShowDelay * 0.6f);
            Assert.That(shown, Is.Zero, "not before the delay is up");

            timing.Tick(ShowDelay * 0.6f);
            Assert.That(shown, Is.EqualTo(1), "once, as the delay elapses");
            Assert.That(received, Is.EqualTo("hint"));
            Assert.That(timing.State, Is.EqualTo(TooltipVisibility.Visible));

            timing.Tick(1f);
            Assert.That(shown, Is.EqualTo(1), "and never again from the same request");
        }

        [Test]
        public void Cancel_WhilePendingShow_NeverFiresOnShow()
        {
            var shown = 0;
            var timing = NewTiming();
            timing.OnShow += _ => shown++;

            timing.Request("hint");
            timing.Tick(ShowDelay * 0.5f);
            timing.Cancel();
            timing.Tick(1f);

            Assert.That(shown, Is.Zero);
            Assert.That(timing.State, Is.EqualTo(TooltipVisibility.Hidden));
        }

        [Test]
        public void RapidEnterExitEnter_WithinTheShowDelay_NeverFlashesThePanel()
        {
            var shown = 0;
            var timing = NewTiming();
            timing.OnShow += _ => shown++;

            // enter -> exit -> enter, all well inside the show delay.
            timing.Request("A");
            timing.Tick(ShowDelay * 0.2f);
            timing.Cancel();
            timing.Tick(ShowDelay * 0.2f);
            timing.Request("B");
            timing.Tick(ShowDelay * 0.2f);

            Assert.That(shown, Is.Zero, "still pending - the cancel restarted from Hidden");

            timing.Tick(ShowDelay);
            Assert.That(shown, Is.EqualTo(1), "shows exactly once, for the surviving hover");
        }

        [Test]
        public void Request_WhileVisible_SwapsContentImmediately_WithoutARepeatDelay()
        {
            var received = default(string);
            var shown = 0;
            var timing = NewTiming();
            timing.OnShow += value => { received = value; shown++; };

            timing.Request("A");
            timing.Tick(ShowDelay);
            Assert.That(shown, Is.EqualTo(1));

            timing.Request("B");

            Assert.That(shown, Is.EqualTo(2), "swaps synchronously - no second show-delay");
            Assert.That(received, Is.EqualTo("B"));
            Assert.That(timing.State, Is.EqualTo(TooltipVisibility.Visible));
        }

        [Test]
        public void Cancel_WhileVisible_DoesNotHideSynchronously()
        {
            var hidden = 0;
            var timing = NewTiming();
            timing.OnHide += () => hidden++;

            timing.Request("hint");
            timing.Tick(ShowDelay);

            timing.Cancel();

            Assert.That(hidden, Is.Zero);
            Assert.That(timing.State, Is.EqualTo(TooltipVisibility.PendingHide));
        }

        [Test]
        public void Tick_PastTheHideDebounce_FiresOnHideExactlyOnce()
        {
            var hidden = 0;
            var timing = NewTiming();
            timing.OnHide += () => hidden++;

            timing.Request("hint");
            timing.Tick(ShowDelay);
            timing.Cancel();

            timing.Tick(HideDebounce * 0.5f);
            Assert.That(hidden, Is.Zero, "not before the debounce is up");

            timing.Tick(HideDebounce * 0.5f);
            Assert.That(hidden, Is.EqualTo(1), "once, as the debounce elapses");
            Assert.That(timing.State, Is.EqualTo(TooltipVisibility.Hidden));

            timing.Tick(1f);
            Assert.That(hidden, Is.EqualTo(1), "and never again");
        }

        [Test]
        public void Request_WithinTheHideDebounce_CancelsTheHide_AndSwapsContent_WithoutFlashing()
        {
            var received = default(string);
            var shown = 0;
            var hidden = 0;
            var timing = NewTiming();
            timing.OnShow += value => { received = value; shown++; };
            timing.OnHide += () => hidden++;

            timing.Request("A");
            timing.Tick(ShowDelay);
            Assert.That(shown, Is.EqualTo(1));

            timing.Cancel(); // exit -> pending-hide
            timing.Tick(HideDebounce * 0.5f);
            timing.Request("B"); // re-enter (a different target) before the debounce elapses

            timing.Tick(1f);

            Assert.That(hidden, Is.Zero, "the pending hide was cancelled by the re-entry");
            Assert.That(shown, Is.EqualTo(2));
            Assert.That(received, Is.EqualTo("B"));
            Assert.That(timing.State, Is.EqualTo(TooltipVisibility.Visible));
        }

        [Test]
        public void OnlyOneHintIsEverCurrent()
        {
            var timing = NewTiming();

            timing.Request("A");
            timing.Tick(ShowDelay);
            timing.Request("B");

            Assert.That(timing.Content, Is.EqualTo("B"));
        }
    }
}
