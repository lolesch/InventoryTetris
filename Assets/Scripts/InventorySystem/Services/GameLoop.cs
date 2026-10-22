using Submodules.Utility.Tools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The per-frame tick, hosted by the player loop rather than a <c>MonoBehaviour</c>: boot
    /// installs one <see cref="PlayerLoopHook"/> system, and whatever needs the frame (the
    /// simulation, from #113) adds a ticker. No GameObject exists, so there is nothing to hide,
    /// persist or leak, and the Play-entry reset is symmetric with the locator's. It runs at the
    /// end of <c>Update</c>, after every <c>MonoBehaviour.Update</c>.
    ///
    /// Tickers receive the raw <c>Time.deltaTime</c>; scaling by sim speed is the ticker's job
    /// (ADR-0008: sim speed never touches <c>Time.timeScale</c>).
    /// </summary>
    public static class GameLoop
    {
        private static readonly List<Action<float>> tickers = new();
        private static bool ticking;

        public static bool IsInstalled => PlayerLoopHook.IsInstalled(typeof(GameLoop));

        /// <summary>Adds <paramref name="ticker"/>; tickers run in the order they were added. Throws
        /// when called from inside a ticker.</summary>
        public static void Add(Action<float> ticker)
        {
            if (ticker == null)
                throw new ArgumentNullException(nameof(ticker));

            ThrowIfTicking();
            tickers.Add(ticker);
        }

        public static void Remove(Action<float> ticker)
        {
            ThrowIfTicking();
            _ = tickers.Remove(ticker);
        }

        /// <summary>Installs the loop system once; installing again replaces it, never doubles it.</summary>
        public static void Install()
        {
            PlayerLoopHook.Install<Update>(typeof(GameLoop), () => Tick(Time.deltaTime));

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        public static void Uninstall() => PlayerLoopHook.Remove(typeof(GameLoop));

        /// <summary>Runs every ticker once. A ticker that throws is logged and does not stop the
        /// ones after it, so one broken system cannot starve the rest of the frame.</summary>
        internal static void Tick(float deltaTime)
        {
            ticking = true;

            try
            {
                // Forward on purpose, unlike the repo's reverse-loop convention: tickers run in
                // the order they were added. Add/Remove are blocked while ticking, so the list
                // cannot change under the loop. Do not "fix" this to a reverse loop.
                for (var i = 0; i < tickers.Count; i++)
                {
                    try
                    {
                        tickers[i](deltaTime);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            finally
            {
                ticking = false;
            }
        }

        // Fires on every Play entry even with domain reload disabled, so a ticker added in the
        // previous session cannot survive into the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            tickers.Clear();
            ticking = false;
        }

#if UNITY_EDITOR
        // The loop system is removed on ExitingPlayMode (PlayerLoopHook), so nothing ticks while the
        // scene is torn down; the registrations are cleared on entering Edit Mode, after that
        // teardown, so Edit Mode never sees the last session's registrations. Same moment as the
        // locator's clear.
        internal static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                Reset();
        }
#endif

        private static void ThrowIfTicking()
        {
            if (ticking)
                throw new InvalidOperationException($"{nameof(GameLoop)} tickers cannot be added or removed from inside a ticker.");
        }
    }
}
