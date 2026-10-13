using NUnit.Framework;
using ToolSmiths.InventorySystem.Changelogs;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Changelogs
{
    /// <summary>What the Known Issues and Roadmap tabs show for a Changelog file's text.</summary>
    [TestFixture]
    public sealed class ChangelogTabsTests
    {
        private const string Sample =
            "# Patch Notes\n\n# Known Issues\n\n- First issue.\n- Second issue.\n\n# Roadmap\n\n- First plan.\n- Second plan.\n";

        private const string Empty = "# Patch Notes\n\n# Known Issues\n\n# Roadmap\n";

        [Test]
        public void Bullets_AreShownInFileOrder()
        {
            var tabs = ChangelogTabs.Read(Sample);

            CollectionAssert.AreEqual(new[] { "First issue.", "Second issue." }, tabs.KnownIssues);
            CollectionAssert.AreEqual(new[] { "First plan.", "Second plan." }, tabs.Roadmap);
        }

        [Test]
        public void EmptyLists_ShowOnePlaceholderLine()
        {
            var tabs = ChangelogTabs.Read(Empty);

            CollectionAssert.AreEqual(new[] { "No known issues." }, tabs.KnownIssues);
            CollectionAssert.AreEqual(new[] { "Nothing planned yet." }, tabs.Roadmap);
        }

        [Test]
        public void ParseError_ShowsOneFailureLineOnBothTabs_AndNamesTheLineForTheLog()
        {
            var tabs = ChangelogTabs.Read("# Patch Notes\n\n# Known Issues\n\nstray text\n\n# Roadmap\n");

            Assert.IsTrue(tabs.Failed);
            CollectionAssert.AreEqual(new[] { "The changelog could not be read (line 5)." }, tabs.KnownIssues);
            CollectionAssert.AreEqual(tabs.KnownIssues, tabs.Roadmap);
            StringAssert.StartsWith("Changelog line 5: ", tabs.FailureLog);
        }

        [Test]
        public void ReadableFile_HasNoFailure()
        {
            Assert.IsFalse(ChangelogTabs.Read(Sample).Failed);
        }
    }
}
