using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Services
{
    /// <summary>
    /// A scene or prefab writes component types down as text: a class identifier, and the target type of every
    /// persistent <c>UnityEvent</c> call. A rename that misses one fails silently (a dead button, no exception),
    /// so every such name must still resolve to a type in the predefined assembly.
    /// </summary>
    [TestFixture]
    public sealed class SerializedTypeNameTests
    {
        private const string Assembly = "Assembly-CSharp";

        // Dead components already on main when this test was written (missing scripts in Demo and Example): a new
        // entry fails the test, and deleting the dead component is its own change.
        private static readonly HashSet<string> KnownDead = new()
        {
            "Demo.unity: ToolSmiths.InventorySystem.GUI.Components.Buttons.TestButton",
            "Demo.unity: ToolSmiths.InventorySystem.GUI.Components.Toggles.Toggle",
            "Example.unity: ToolSmiths.InventorySystem.Runtime.Simulation.GoVentureButton",
        };

        private static readonly Regex EventTarget = new(@"(ToolSmiths\.InventorySystem\.[\w.]+), " + Assembly + @"\b");
        private static readonly Regex ClassIdentifier = new(Assembly + @"::(ToolSmiths\.InventorySystem\.[\w.]+)");

        [Test]
        public void EveryTypeNameAScenePrefabWritesDown_StillResolves()
        {
            var files = Directory.GetFiles(Application.dataPath, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".unity") || path.EndsWith(".prefab"));

            var stale = new SortedSet<string>();
            var checkedNames = 0;

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);

                foreach (var name in EventTarget.Matches(text).Concat(ClassIdentifier.Matches(text)).Select(match => match.Groups[1].Value))
                {
                    checkedNames++;

                    if (Type.GetType($"{name}, {Assembly}") == null)
                        _ = stale.Add($"{Path.GetFileName(file)}: {name}");
                }
            }

            Assert.That(checkedNames, Is.GreaterThan(0), "the scan found no type name to check");
            stale.ExceptWith(KnownDead);

            Assert.That(stale, Is.Empty,"a scene or prefab names a type that no longer exists");
        }
    }
}
