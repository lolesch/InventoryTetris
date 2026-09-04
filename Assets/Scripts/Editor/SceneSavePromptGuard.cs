using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToolSmiths.InventorySystem.EditorScripts
{
    /// <summary>
    /// Suppresses the blocking "Scene(s) Have Been Modified" save dialog that interrupts
    /// MCP / AI-assistant sessions. That modal is a native OS dialog: once it is up, no
    /// editor code runs until a human clicks it, so it cannot be auto-answered — it can
    /// only be prevented by keeping open <c>.unity</c> scenes non-dirty.
    ///
    /// Rule: a loaded scene whose unsaved changes <b>originated while Unity was in the
    /// background</b> (i.e. something was driving the Editor, not a person) has its dirty
    /// flag cleared after a short debounce. The in-memory changes remain until the next
    /// scene reload and are never written to disk — this is a discard, not a save.
    ///
    /// Changes you make with Unity focused are left completely alone, even after you alt-tab
    /// away, so hand-editing a scene never loses work. Explicit saves (Ctrl+S,
    /// <see cref="EditorSceneManager.SaveOpenScenes"/>) always win.
    ///
    /// Toggle at <c>Tools ▸ MCP ▸ Suppress Scene Save Prompt</c>. Enable
    /// <c>Tools ▸ MCP ▸ ...Even For Edits Made While Focused</c> only if you want the
    /// aggressive mode that also discards your own unsaved scene edits.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneSavePromptGuard
    {
        private const string EnabledMenu = "Tools/MCP/Suppress Scene Save Prompt";
        private const string AggressiveMenu = "Tools/MCP/Suppress Scene Save Prompt (Even For Edits Made While Focused)";
        private const string EnabledPref = "ToolSmiths.SceneSavePromptGuard.Enabled";
        private const string AggressivePref = "ToolSmiths.SceneSavePromptGuard.Aggressive";

        private const double DebounceSeconds = 1.0;
        private const double LogThrottleSeconds = 15.0;

        // ClearSceneDirtiness(Scene) is internal; reflection is fine from a compiled editor
        // assembly (only the AI-assistant's *dynamic* RunCommand scripts choke on System.Reflection).
        private static readonly MethodInfo ClearDirtiness = typeof(EditorSceneManager).GetMethod(
            "ClearSceneDirtiness",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
            null, new[] { typeof(Scene) }, null);

        private static bool _prevAnyDirty;
        private static bool _dirtOriginatedFocused;
        private static double _dirtySince = -1;
        private static double _lastLog = -1;

        static SceneSavePromptGuard()
        {
            if (ClearDirtiness == null)
                Debug.LogWarning("[SceneSavePromptGuard] EditorSceneManager.ClearSceneDirtiness not found; " +
                                 "falling back to a hard scene reload (single-scene setups only).");

            // Treat any dirt that already exists at load time as the user's — never auto-discard it.
            _prevAnyDirty = AnyGuardableDirty();
            _dirtOriginatedFocused = _prevAnyDirty;

            EditorApplication.update += Tick;
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPref, true);
            set => EditorPrefs.SetBool(EnabledPref, value);
        }

        public static bool Aggressive
        {
            get => EditorPrefs.GetBool(AggressivePref, false);
            set => EditorPrefs.SetBool(AggressivePref, value);
        }

        [MenuItem(EnabledMenu, false, 1000)]
        private static void ToggleEnabled() => Enabled = !Enabled;

        [MenuItem(EnabledMenu, true)]
        private static bool ValidateEnabled()
        {
            Menu.SetChecked(EnabledMenu, Enabled);
            return true;
        }

        [MenuItem(AggressiveMenu, false, 1001)]
        private static void ToggleAggressive() => Aggressive = !Aggressive;

        [MenuItem(AggressiveMenu, true)]
        private static bool ValidateAggressive()
        {
            Menu.SetChecked(AggressiveMenu, Aggressive);
            return Enabled;
        }

        private static void Tick()
        {
            if (!Enabled
                || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                _dirtySince = -1;
                return;
            }

            var anyDirty = AnyGuardableDirty();

            // Rising edge: remember whether a person or a background driver caused this dirt.
            if (anyDirty && !_prevAnyDirty)
                _dirtOriginatedFocused = UnityEditorInternal.InternalEditorUtility.isApplicationActive;
            _prevAnyDirty = anyDirty;

            if (!anyDirty || (_dirtOriginatedFocused && !Aggressive))
            {
                _dirtySince = -1;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (_dirtySince < 0)
            {
                _dirtySince = now;
                return;
            }
            if (now - _dirtySince < DebounceSeconds)
                return;

            var discarded = DiscardDirtyScenes();
            _dirtySince = -1;
            _prevAnyDirty = AnyGuardableDirty();

            if (discarded > 0 && now - _lastLog > LogThrottleSeconds)
            {
                _lastLog = now;
                Debug.Log($"[SceneSavePromptGuard] Discarded background-made changes in {discarded} scene(s) to " +
                          $"avoid the blocking save dialog. Toggle at '{EnabledMenu}'.");
            }
        }

        private static bool AnyGuardableDirty()
        {
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
                if (IsGuardable(EditorSceneManager.GetSceneAt(i)))
                    return true;
            return false;
        }

        private static int DiscardDirtyScenes()
        {
            var discarded = 0;

            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!IsGuardable(scene))
                    continue;

                if (ClearDirtiness != null)
                    ClearDirtiness.Invoke(null, new object[] { scene });
                else if (EditorSceneManager.sceneCount == 1)
                    EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                else
                    continue; // no safe multi-scene fallback without the internal API

                discarded++;
            }

            return discarded;
        }

        private static bool IsGuardable(Scene scene) =>
            scene.isLoaded
            && scene.isDirty
            && !string.IsNullOrEmpty(scene.path)
            && scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
    }
}
