using NUnit.Framework;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    [TestFixture]
    public sealed class GameLoopTests
    {
        private readonly List<Action<float>> added = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var ticker in added)
                GameLoop.Remove(ticker);

            added.Clear();
            GameLoop.Uninstall();
            GameLoop.Reset();
        }

        private Action<float> Add(Action<float> ticker)
        {
            GameLoop.Add(ticker);
            added.Add(ticker);
            return ticker;
        }

        [Test]
        public void Tick_RunsEveryTicker_InTheOrderTheyWereAdded_WithTheDelta()
        {
            var seen = new List<string>();
            _ = Add(dt => seen.Add($"a{dt}"));
            _ = Add(dt => seen.Add($"b{dt}"));

            GameLoop.Tick(0.5f);

            Assert.That(seen, Is.EqualTo(new[] { "a0.5", "b0.5" }));
        }

        [Test]
        public void Remove_StopsATicker()
        {
            var calls = 0;
            var ticker = Add(_ => calls++);

            GameLoop.Remove(ticker);
            GameLoop.Tick(1f);

            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void AddingFromInsideATicker_Throws_AndTheFlagIsClearedAfterwards()
        {
            _ = Add(dt => GameLoop.Add(x => { }));

            _ = Assert.Throws<InvalidOperationException>(() => GameLoop.Tick(1f));

            Assert.DoesNotThrow(() => GameLoop.Add(x => { }), "a throwing tick must not leave the loop locked");
        }

        [Test]
        public void Install_Twice_LeavesOneSystem()
        {
            GameLoop.Install();
            GameLoop.Install();

            Assert.That(GameLoop.IsInstalled, Is.True);

            GameLoop.Uninstall();

            Assert.That(GameLoop.IsInstalled, Is.False, "one Uninstall must clear it, so it was never doubled");
        }
    }
}
