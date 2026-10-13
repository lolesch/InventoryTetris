using NUnit.Framework;
using ToolSmiths.InventorySystem.Changelogs;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Changelogs
{
    /// <summary>The Changelog file as the game ships it: a bad edit fails here instead of breaking the panel.</summary>
    [TestFixture]
    public sealed class ShippedChangelogTests
    {
        private const string ShippedPath = "Assets/Changelog/Changelog.md";

        [Test]
        public void ShippedFile_ParsesWithTheProductionParser()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ShippedPath);
            Assert.IsNotNull(asset, $"no Changelog file at {ShippedPath}");

            Assert.IsTrue(ChangelogParser.TryParse(asset.text, out var changelog, out var error),
                $"{ShippedPath} line {error.Line}: {error.Message}");
            Assert.IsNotNull(changelog);
        }
    }
}
