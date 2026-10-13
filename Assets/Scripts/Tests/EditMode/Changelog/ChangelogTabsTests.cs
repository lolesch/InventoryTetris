using System;
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

        private const string Empty = ChangelogParserTests.Empty;

        private const string Unparsable = "# Patch Notes\n\n# Known Issues\n\nstray text\n\n# Roadmap\n";

        private const string WithUnreleased =
            "# Patch Notes\n\n## Unreleased\n\n- Coming soon.\n\n## 0.1.0 - 2026-08-01\n\n- First build.\n\n# Known Issues\n\n# Roadmap\n";

        private const string OnlyUnreleased = "# Patch Notes\n\n## Unreleased\n\n- Coming soon.\n\n# Known Issues\n\n# Roadmap\n";

        /// <summary>The tabs as the Editor or a development build shows them, where Unreleased is visible.</summary>
        private static ChangelogTabs ReadInDevelopment(string text) => ChangelogTabs.Read(text, true);

        [Test]
        public void Bullets_AreShownInFileOrder()
        {
            var tabs = ReadInDevelopment(Sample);

            CollectionAssert.AreEqual(new[] { "• First issue.", "• Second issue." }, tabs.KnownIssues);
            CollectionAssert.AreEqual(new[] { "• First plan.", "• Second plan." }, tabs.Roadmap);
        }

        [Test]
        public void EmptyLists_ShowOnePlaceholderLine()
        {
            var tabs = ReadInDevelopment(Empty);

            CollectionAssert.AreEqual(new[] { "No known issues." }, tabs.KnownIssues);
            CollectionAssert.AreEqual(new[] { "Nothing planned yet." }, tabs.Roadmap);
        }

        [Test]
        public void ParseError_ShowsOneFailureLineOnBothTabs_AndNamesTheLineForTheLog()
        {
            var tabs = ReadInDevelopment(Unparsable);

            Assert.IsTrue(tabs.Failed);
            CollectionAssert.AreEqual(new[] { "The changelog could not be read (line 5)." }, tabs.KnownIssues);
            CollectionAssert.AreEqual(tabs.KnownIssues, tabs.Roadmap);
            StringAssert.StartsWith("Changelog line 5: ", tabs.FailureLog);
        }

        [Test]
        public void ReadableFile_HasNoFailure()
        {
            Assert.IsFalse(ReadInDevelopment(Sample).Failed);
        }

        [Test]
        public void PatchNotes_ShowEachEntryAsVersionAndDateHeadingWithItsBullets_InFileOrder()
        {
            var tabs = ReadInDevelopment(
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
            var tabs = ReadInDevelopment(Empty);

            Assert.IsEmpty(tabs.Entries);
            Assert.AreEqual("No patch notes yet.", tabs.PatchNotesPlaceholder);
        }

        [Test]
        public void ParseError_ShowsTheSameFailureLineOnPatchNotes_AndNoEntries()
        {
            var tabs = ReadInDevelopment(Unparsable);

            Assert.IsEmpty(tabs.Entries);
            Assert.AreEqual(tabs.KnownIssues[0], tabs.PatchNotesPlaceholder);
        }

        [Test]
        public void PatchNotes_ShowADateOnlyHeading_ForABuildWithoutAVersion()
        {
            var tabs = ReadInDevelopment("# Patch Notes\n\n## 2023-10-15\n\n- Old build.\n\n# Known Issues\n\n# Roadmap\n");

            Assert.AreEqual("2023-10-15", tabs.Entries[0].Heading);
        }

        [Test]
        public void PatchNotes_ShowTheUnreleasedEntryUnderItsOwnHeading()
        {
            var tabs = ReadInDevelopment(WithUnreleased);

            Assert.AreEqual(2, tabs.Entries.Count);
            Assert.AreEqual("Unreleased", tabs.Entries[0].Heading);
            CollectionAssert.AreEqual(new[] { "• Coming soon." }, tabs.Entries[0].Lines);
            Assert.AreEqual("0.1.0 - 2026-08-01", tabs.Entries[1].Heading);
        }

        private static readonly PatchNotesEntry Unreleased =
            new PatchNotesEntry(PatchNotesEntry.UnreleasedLabel, null, new[] { "Coming soon." });

        private static readonly PatchNotesEntry Released =
            new PatchNotesEntry("0.1.0", new DateTime(2026, 8, 1), new[] { "First build." });

        [Test]
        public void Shown_DropsTheUnreleasedEntry_WhenNotDevelopment()
        {
            var shown = ChangelogTabs.Shown(new[] { Unreleased, Released }, false);

            CollectionAssert.AreEqual(new[] { Released }, shown);
        }

        [Test]
        public void Shown_KeepsEveryEntryInOrder_WhenDevelopment()
        {
            var shown = ChangelogTabs.Shown(new[] { Unreleased, Released }, true);

            CollectionAssert.AreEqual(new[] { Unreleased, Released }, shown);
        }

        [Test]
        public void Shown_KeepsEveryEntry_WhenThereIsNoUnreleasedEntry_OnOrOff()
        {
            var entries = new[] { Released };

            CollectionAssert.AreEqual(entries, ChangelogTabs.Shown(entries, true));
            CollectionAssert.AreEqual(entries, ChangelogTabs.Shown(entries, false));
        }

        [Test]
        public void Shown_TreatsAnUnreleasedEntryWithoutBulletsLikeAnyOther()
        {
            var empty = new PatchNotesEntry(PatchNotesEntry.UnreleasedLabel, null, new string[0]);

            CollectionAssert.AreEqual(new[] { empty, Released }, ChangelogTabs.Shown(new[] { empty, Released }, true));
            CollectionAssert.AreEqual(new[] { Released }, ChangelogTabs.Shown(new[] { empty, Released }, false));
        }

        [Test]
        public void PatchNotes_DropTheUnreleasedEntry_WhenNotDevelopment_AndKeepTheReleasedOnes()
        {
            var tabs = ChangelogTabs.Read(WithUnreleased, false);

            Assert.AreEqual(1, tabs.Entries.Count);
            Assert.AreEqual("0.1.0 - 2026-08-01", tabs.Entries[0].Heading);
            Assert.IsNull(tabs.PatchNotesPlaceholder);
        }

        [Test]
        public void PatchNotes_ShowThePlaceholder_WhenNotDevelopment_AndUnreleasedWasTheOnlyEntry()
        {
            var tabs = ChangelogTabs.Read(OnlyUnreleased, false);

            Assert.IsEmpty(tabs.Entries);
            Assert.AreEqual("No patch notes yet.", tabs.PatchNotesPlaceholder);
        }
    }
}
