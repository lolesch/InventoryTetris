using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

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
            InvalidOperationException thrown = null;
            _ = Add(dt =>
            {
                try { GameLoop.Add(x => { }); }
                catch (InvalidOperationException e) { thrown = e; }
            });

            GameLoop.Tick(1f);

            Assert.That(thrown, Is.Not.Null);
            Assert.DoesNotThrow(() => GameLoop.Add(x => { }), "the lock must be released once the tick ends");
        }

        [Test]
        public void ATickerThatThrows_IsLogged_AndDoesNotStopTheTickersAfterIt()
        {
            var laterRan = false;
            _ = Add(_ => throw new InvalidOperationException("boom"));
            _ = Add(_ => laterRan = true);

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            GameLoop.Tick(1f);

            Assert.That(laterRan, Is.True);
        }

        [Test]
        public void LeavingPlayMode_ClearsTheTickers_SoEditModeNeverSeesTheLastSessions()
        {
            var calls = 0;
            _ = Add(_ => calls++);

            GameLoop.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);
            GameLoop.Tick(1f);

            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void EnteringEditMode_LeavesTheTickersAlone()
        {
            var calls = 0;
            _ = Add(_ => calls++);

            GameLoop.OnPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);
            GameLoop.Tick(1f);

            Assert.That(calls, Is.EqualTo(1));
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
