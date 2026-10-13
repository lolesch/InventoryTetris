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

            CollectionAssert.AreEqual(new[] { "• First issue.", "• Second issue." }, tabs.KnownIssues);
            CollectionAssert.AreEqual(new[] { "• First plan.", "• Second plan." }, tabs.Roadmap);
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

        [Test]
        public void PatchNotes_ShowEachEntryAsVersionAndDateHeadingWithItsBullets_InFileOrder()
        {
            var tabs = ChangelogTabs.Read(
                "# Patch Notes\n\n## 0.2.0 - 2026-09-25\n\n- Added a thing.\n- Fixed a thing.\n\n## 0.1.0 - 2026-08-01\n\n- First build.\n\n# Known Issues\n\n# Roadmap\n");

            Assert.AreEqual(2, tabs.Entries.Count);
            Assert.AreEqual("0.2.0 - 2026-09-25", tabs.Entries[0].Heading);
            CollectionAssert.AreEqual(new[] { "• Added a thing.", "• Fixed a thing." }, tabs.Entries[0].Lines);
            Assert.AreEqual("0.1.0 - 2026-08-01", tabs.Entries[1].Heading);
            CollectionAssert.AreEqual(new[] { "• First build." }, tabs.Entries[1].Lines);
            Assert.IsNull(tabs.PatchNotesPlaceholder);
        }

        [Test]
        public void EmptyPatchNotes_ShowOnePlaceholderLineAndNoEntries()
        {
            var tabs = ChangelogTabs.Read(Empty);

            Assert.IsEmpty(tabs.Entries);
            Assert.AreEqual("No patch notes yet.", tabs.PatchNotesPlaceholder);
        }

        [Test]
        public void ParseError_ShowsTheSameFailureLineOnPatchNotes_AndNoEntries()
        {
            var tabs = ChangelogTabs.Read("# Patch Notes\n\n# Known Issues\n\nstray text\n\n# Roadmap\n");

            Assert.IsEmpty(tabs.Entries);
            Assert.AreEqual(tabs.KnownIssues[0], tabs.PatchNotesPlaceholder);
        }

        [Test]
        public void PatchNotes_ShowADateOnlyHeading_ForABuildWithoutAVersion()
        {
            var tabs = ChangelogTabs.Read("# Patch Notes\n\n## 2023-10-15\n\n- Old build.\n\n# Known Issues\n\n# Roadmap\n");

            Assert.AreEqual("2023-10-15", tabs.Entries[0].Heading);
        }

        [Test]
        public void PatchNotes_ShowTheUnreleasedEntryUnderItsOwnHeading()
        {
            var tabs = ChangelogTabs.Read(
                "# Patch Notes\n\n## Unreleased\n\n- Coming soon.\n\n## 0.1.0 - 2026-08-01\n\n- First build.\n\n# Known Issues\n\n# Roadmap\n");

            Assert.AreEqual("Unreleased", tabs.Entries[0].Heading);
            CollectionAssert.AreEqual(new[] { "• Coming soon." }, tabs.Entries[0].Lines);
            Assert.AreEqual("0.1.0 - 2026-08-01", tabs.Entries[1].Heading);
        }
    }
}
