// TEMP bridge runner for issue #25 — invokes the Locations EditMode suite and writes results
// to a file the bridge can read back. Do NOT commit. Removed once verified green.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ToolSmiths.InventorySystem.EditorTests
{
    public static class LocationsSuiteRunner
    {
        private static readonly string OutPath =
            Path.Combine(Application.dataPath, "..", "Temp", "locations-suite-results.txt");

        public static void RunLocationsSuite()
        {
            Debug.Log("[LocSuite] dispatch");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new CallbacksProbe());
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "InventorySystem.Locations.Tests" }
            }));
            Object.DestroyImmediate(api);
        }

        private sealed class CallbacksProbe : ICallbacks
        {
            public void RunFinished(ITestResultAdaptor tests)
            {
                int pass = 0, fail = 0, skip = 0, total = 0;
                var sb = new StringBuilder();
                sb.AppendLine("LOCATIONS EDITMODE RUN");
                Collect(tests, 0, sb, ref pass, ref fail, ref skip, ref total);
                sb.AppendLine($"=== passed={pass} failed={fail} skipped={skip} total={total} ===");
                try
                {
                    File.WriteAllText(OutPath, sb.ToString());
                    Debug.Log("[LocSuite] results written to " + OutPath);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[LocSuite] file write failed: " + e.Message);
                }
            }

            private static void Collect(
                ITestResultAdaptor node, int depth, StringBuilder sb,
                ref int pass, ref int fail, ref int skip, ref int total)
            {
                if (node.HasChildren)
                {
                    foreach (var child in node.Children)
                        Collect(child, depth + 1, sb, ref pass, ref fail, ref skip, ref total);
                }
                else
                {
                    total++;
                    var s = node.ResultState.ToString();
                    sb.AppendLine($"{s} :: {node.FullName}");
                    if (!string.IsNullOrEmpty(node.Message))
                        sb.AppendLine($"    msg: {node.Message.Replace("\n", " | ")}");
                    if (s.StartsWith("Passed")) pass++;
                    else if (s.StartsWith("Failed")) fail++;
                    else skip++;
                }
            }

            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}