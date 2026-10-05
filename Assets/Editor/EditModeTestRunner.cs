// allow-scratch-runner: committed on purpose. The pre-commit hook flags TestRunnerApi anywhere else.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ToolSmiths.InventorySystem.EditorScripts
{
    /// <summary>
    /// Runs the EditMode suite inside the open Editor and writes the verdict to
    /// <see cref="ResultsPath"/>, so a bridge session (<c>Unity_RunCommand</c>) can start it with one
    /// line and poll one file instead of rewriting a <c>TestRunnerApi</c> harness every time:
    /// <code>ToolSmiths.InventorySystem.EditorScripts.EditModeTestRunner.Run();</code>
    /// The file reads <c>RUNNING</c> while the run is going, then <c>passed=N failed=M</c>, one line per
    /// failed test, and a final <c>DONE</c>. Poll for <c>DONE</c>, not for the file: it exists from the
    /// start. <see cref="Run(string)"/> takes a regex over the test's full name (a namespace, say) to
    /// scope the run. The human <b>Run All</b> in the Test Runner is still the gate before closing an
    /// issue; this is the pre-check.
    /// </summary>
    public static class EditModeTestRunner
    {
        public const string ResultsPath = "Temp/editmode-results.txt";

        private const string Menu = "ToolSmiths/Tests/Run EditMode Suite";

        [MenuItem(Menu)]
        private static void RunFromMenu() => Run();

        public static void Run() => Run(null);

        public static void Run(string groupRegex)
        {
            File.WriteAllText(ResultsPath, "RUNNING\n");

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultsFile(api));

            var filter = new Filter { testMode = TestMode.EditMode };
            if (!string.IsNullOrEmpty(groupRegex))
                filter.groupNames = new[] { groupRegex };

            api.Execute(new ExecutionSettings(filter));
        }

        private sealed class ResultsFile : ICallbacks
        {
            private readonly TestRunnerApi api;
            private readonly List<string> failed = new();
            private int passed;

            public ResultsFile(TestRunnerApi api) => this.api = api;

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren)
                    return;

                if (result.TestStatus == TestStatus.Passed)
                    passed++;
                else if (result.TestStatus == TestStatus.Failed)
                    failed.Add(result.FullName);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var text = new StringBuilder();
                text.Append("passed=").Append(passed).Append(" failed=").Append(failed.Count).AppendLine();
                foreach (var name in failed)
                    text.AppendLine(name);
                text.Append("DONE");

                File.WriteAllText(ResultsPath, text.ToString());

                api.UnregisterCallbacks(this);
                Object.DestroyImmediate(api);
            }
        }
    }
}
