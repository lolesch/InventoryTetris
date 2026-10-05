// allow-scratch-runner: committed on purpose. The pre-commit hook flags EditorApplication.Exit anywhere else.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ToolSmiths.InventorySystem.EditorScripts
{
    /// <summary>
    /// The batch-mode Play Mode driver the notes describe, so a Play Mode check is its assertions and
    /// nothing else. Write a <c>public static void Run()</c> under <c>Assets/Editor/</c> that calls
    /// <see cref="Start"/> with a scene and a routine, and run it with the project closed (a worktree
    /// when the Editor holds the primary one):
    /// <code>Unity.exe -batchmode -nographics -projectPath &lt;proj&gt; -executeMethod &lt;Class&gt;.Run -logFile out.log</code>
    /// The process exit code is the verdict: 0 every <see cref="Check"/> held and nothing errored,
    /// 1 a check failed or an error was logged, 2 the run timed out.
    ///
    /// The three things that make a bare Play Mode script lie are handled here. The player loop barely
    /// advances on its own, so every tick unpauses and steps it. A routine's <c>yield return Nested()</c>
    /// does not run <c>Nested</c> under a bare <c>MoveNext()</c> loop, so the driver keeps a stack and
    /// pushes whatever the top yields. And the Editor's own Unity Search startup exception is not
    /// counted as a failure. Wait with <see cref="Wait"/> (on <c>Time.time</c>), never on frame count or
    /// wall clock.
    ///
    /// <see cref="SelfTest"/> exercises the driver itself and is the template for a real check.
    /// </summary>
    public static class PlayModeDriver
    {
        private const double TimeoutSeconds = 300;

        private static readonly Stack<IEnumerator> routines = new();
        private static readonly List<string> failures = new();
        private static Func<IEnumerator> checks;
        private static bool started;
        private static double startedAt;

        /// <summary>Opens <paramref name="scenePath"/> (an empty scene when null), enters Play Mode and runs the routine.</summary>
        public static void Start(string scenePath, Func<IEnumerator> checkRoutine)
        {
            routines.Clear();
            failures.Clear();
            checks = checkRoutine;
            started = false;
            startedAt = EditorApplication.timeSinceStartup;

            if (string.IsNullOrEmpty(scenePath))
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            else
                EditorSceneManager.OpenScene(scenePath);

            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        /// <summary>Records a failure when <paramref name="condition"/> is false.</summary>
        public static void Check(bool condition, string message)
        {
            if (condition)
                return;

            failures.Add(message);
            Debug.Log($"[PlayModeDriver] FAIL: {message}");
        }

        /// <summary>Waits <paramref name="seconds"/> of <c>Time.time</c>.</summary>
        public static IEnumerator Wait(float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        /// <summary>The driver's own check: <c>-executeMethod ToolSmiths.InventorySystem.EditorScripts.PlayModeDriver.SelfTest</c>.</summary>
        public static void SelfTest() => Start(null, SelfTestChecks);

        private static IEnumerator SelfTestChecks()
        {
            var before = Time.time;
            yield return Wait(1f);
            Check(Time.time - before >= 1f, "Time.time advanced across Wait(1)");

            var nestedRan = false;
            yield return Nested(() => nestedRan = true);
            Check(nestedRan, "a yielded routine ran to completion before the next line");
        }

        private static IEnumerator Nested(Action done)
        {
            yield return null;
            done();
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
            {
                Debug.Log("[PlayModeDriver] timed out");
                Finish(2);
                return;
            }

            if (!EditorApplication.isPlaying)
                return;

            if (!started)
            {
                started = true;
                routines.Push(checks());
            }

            EditorApplication.isPaused = false;
            EditorApplication.Step();

            if (routines.Count == 0)
                return;

            var top = routines.Peek();
            if (!top.MoveNext())
                routines.Pop();
            else if (top.Current is IEnumerator nested)
                routines.Push(nested);

            if (routines.Count == 0)
                Finish(failures.Count == 0 ? 0 : 1);
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning)
                return;

            if (stackTrace != null && stackTrace.Contains("SearchDatabase"))
                return;

            failures.Add($"{type}: {condition}");
        }

        private static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;

            Debug.Log($"[PlayModeDriver] {failures.Count} failure(s), exit {exitCode}");
            foreach (var failure in failures)
                Debug.Log($"[PlayModeDriver]   {failure}");

            EditorApplication.Exit(exitCode);
        }
    }
}
