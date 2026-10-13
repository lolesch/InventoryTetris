using System;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Changelogs;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Changelogs
{
    /// <summary>The Changelog file grammar: text in, a Changelog or the line that is wrong out.</summary>
    [TestFixture]
    public sealed class ChangelogParserTests
    {
        private const string Empty = "# Patch Notes\n\n# Known Issues\n\n# Roadmap\n";

        private static string PatchNotes(string entries) => "# Patch Notes\n" + entries + "# Known Issues\n# Roadmap\n";

        private static Changelog Parse(string text)
        {
            Assert.IsTrue(ChangelogParser.TryParse(text, out var changelog, out var error),
                $"expected a parse, got line {error.Line}: {error.Message}");
            return changelog;
        }

        private static ChangelogParseError Reject(string text)
        {
            Assert.IsFalse(ChangelogParser.TryParse(text, out var changelog, out var error), "expected a parse error");
            Assert.IsNull(changelog);
            return error;
        }

        [Test]
        public void ThreeEmptySections_ParseToEmptyLists()
        {
            var changelog = Parse(Empty);

            Assert.IsEmpty(changelog.KnownIssues);
            Assert.IsEmpty(changelog.Roadmap);
        }

        [Test]
        public void MissingSection_IsAnErrorAtTheEndOfTheFile()
        {
            Assert.AreEqual(5, Reject("# Patch Notes\n\n# Known Issues\n\n").Line);
        }

        [Test]
        public void MissingMiddleSection_IsAnErrorAtTheHeadingThatSkipsIt()
        {
            Assert.AreEqual(3, Reject("# Patch Notes\n\n# Roadmap\n").Line);
        }

        [Test]
        public void DuplicateSection_IsAnErrorAtTheSecondHeading()
        {
            Assert.AreEqual(7, Reject("# Patch Notes\n\n# Known Issues\n\n# Roadmap\n\n# Roadmap\n").Line);
        }

        [Test]
        public void OutOfOrderSections_AreAnErrorAtTheFirstMisplacedHeading()
        {
            Assert.AreEqual(3, Reject("# Patch Notes\n\n# Roadmap\n\n# Known Issues\n").Line);
        }

        [Test]
        public void EmptyText_IsAnErrorOnLineOne()
        {
            Assert.AreEqual(1, Reject("").Line);
        }

        [Test]
        public void UnknownSection_IsAnErrorAtItsHeading()
        {
            Assert.AreEqual(2, Reject("# Patch Notes\n# Ideas\n").Line);
        }

        [Test]
        public void KnownIssuesAndRoadmap_KeepTheirBulletsInOrder()
        {
            var changelog = Parse(
                "# Patch Notes\n\n# Known Issues\n\n- Second slot is not compared.\n- Numbers are wrong.\n\n# Roadmap\n\n- Pause menu\n- Loot filter\n- Item design\n");

            CollectionAssert.AreEqual(new[] { "Second slot is not compared.", "Numbers are wrong." }, changelog.KnownIssues);
            CollectionAssert.AreEqual(new[] { "Pause menu", "Loot filter", "Item design" }, changelog.Roadmap);
        }

        [Test]
        public void CrlfLineEndings_ParseLikeLf()
        {
            var changelog = Parse("# Patch Notes\r\n\r\n# Known Issues\r\n\r\n- One\r\n\r\n# Roadmap\r\n- Two\r\n");

            CollectionAssert.AreEqual(new[] { "One" }, changelog.KnownIssues);
            CollectionAssert.AreEqual(new[] { "Two" }, changelog.Roadmap);
        }

        [Test]
        public void MissingTrailingNewline_IsAccepted()
        {
            var changelog = Parse("# Patch Notes\n# Known Issues\n# Roadmap\n- Last");

            CollectionAssert.AreEqual(new[] { "Last" }, changelog.Roadmap);
        }

        [Test]
        public void BulletBeforeAnySection_IsAnError()
        {
            Assert.AreEqual(1, Reject("- Stray\n" + Empty).Line);
        }

        [Test]
        public void BulletBeforeAnyEntryHeading_IsAnErrorInPatchNotes()
        {
            Assert.AreEqual(2, Reject("# Patch Notes\n- No entry\n# Known Issues\n# Roadmap\n").Line);
        }

        [Test]
        public void VersionedEntry_CarriesLabelDateAndBulletsInOrder()
        {
            var entry = Parse(PatchNotes("## 0.0.5_Alpha - 2026-09-25\n\n- First\n- Second\n")).Entries.Single();

            Assert.AreEqual("0.0.5_Alpha", entry.Label);
            Assert.AreEqual(new DateTime(2026, 9, 25), entry.Date);
            Assert.IsFalse(entry.IsUnreleased);
            CollectionAssert.AreEqual(new[] { "First", "Second" }, entry.Bullets);
        }

        [Test]
        public void DateOnlyEntry_IsLabelledWithItsDate()
        {
            var entry = Parse(PatchNotes("## 2023-10-15\n- Old\n")).Entries.Single();

            Assert.AreEqual("2023-10-15", entry.Label);
            Assert.AreEqual(new DateTime(2023, 10, 15), entry.Date);
            Assert.IsFalse(entry.IsUnreleased);
        }

        [Test]
        public void UnreleasedEntry_HasNoDateAndIsFlagged()
        {
            var entry = Parse(PatchNotes("## Unreleased\n- Next\n")).Entries.Single();

            Assert.AreEqual("Unreleased", entry.Label);
            Assert.IsNull(entry.Date);
            Assert.IsTrue(entry.IsUnreleased);
            CollectionAssert.AreEqual(new[] { "Next" }, entry.Bullets);
        }

        [Test]
        public void EntryWithoutBullets_IsAllowed()
        {
            var changelog = Parse(PatchNotes("## Unreleased\n\n## 0.0.5_Alpha - 2026-09-25\n"));

            CollectionAssert.AreEqual(new[] { "Unreleased", "0.0.5_Alpha" }, changelog.Entries.Select(e => e.Label));
            Assert.IsEmpty(changelog.Entries[0].Bullets);
            Assert.IsEmpty(changelog.Entries[1].Bullets);
        }

        [Test]
        public void NoEntries_ParseToAnEmptyList()
        {
            Assert.IsEmpty(Parse(Empty).Entries);
        }

        [Test]
        public void Entries_KeepTheirFileOrderAndOwnBullets()
        {
            var changelog = Parse(PatchNotes(
                "## Unreleased\n- A\n## 0.0.5_Alpha - 2026-09-25\n- B\n- C\n## 2023-10-15\n- D\n## 2022-01-23\n- E\n"));

            CollectionAssert.AreEqual(new[] { "Unreleased", "0.0.5_Alpha", "2023-10-15", "2022-01-23" },
                changelog.Entries.Select(e => e.Label));
            CollectionAssert.AreEqual(new[] { "B", "C" }, changelog.Entries[1].Bullets);
            CollectionAssert.AreEqual(new[] { "D" }, changelog.Entries[2].Bullets);
        }

        [Test]
        public void BulletsAfterAnEntry_DoNotLeakIntoKnownIssues()
        {
            var changelog = Parse("# Patch Notes\n## 2023-10-15\n- Old\n# Known Issues\n- Issue\n# Roadmap\n");

            CollectionAssert.AreEqual(new[] { "Old" }, changelog.Entries.Single().Bullets);
            CollectionAssert.AreEqual(new[] { "Issue" }, changelog.KnownIssues);
        }

        [Test]
        public void UnreleasedAfterTheFirstEntry_IsAnErrorAtItsHeading()
        {
            Assert.AreEqual(3, Reject(PatchNotes("## 0.0.5_Alpha - 2026-09-25\n## Unreleased\n")).Line);
        }

        [Test]
        public void SecondUnreleased_IsAnErrorAtItsHeading()
        {
            Assert.AreEqual(3, Reject(PatchNotes("## Unreleased\n## Unreleased\n")).Line);
        }

        [Test]
        public void DuplicateVersion_IsAnErrorAtTheSecondHeading()
        {
            Assert.AreEqual(4, Reject(PatchNotes("## 0.0.5_Alpha - 2026-09-25\n- A\n## 0.0.5_Alpha - 2026-09-24\n")).Line);
        }

        [Test]
        public void DuplicateDateOnlyEntry_IsAnErrorAtTheSecondHeading()
        {
            Assert.AreEqual(4, Reject(PatchNotes("## 2023-10-15\n- A\n## 2023-10-15\n")).Line);
        }

        [Test]
        public void EntryNewerThanThePreviousOne_IsAnErrorAtItsHeading()
        {
            Assert.AreEqual(4, Reject(PatchNotes("## 2022-01-23\n- A\n## 2023-10-15\n")).Line);
        }

        [Test]
        public void VersionedEntryNewerThanThePreviousOne_IsAnErrorAtItsHeading()
        {
            Assert.AreEqual(4, Reject(PatchNotes("## 0.0.4_Alpha - 2026-09-01\n- A\n## 0.0.5_Alpha - 2026-09-25\n")).Line);
        }

        [Test]
        public void TwoEntriesOnTheSameDay_AreAccepted()
        {
            var changelog = Parse(PatchNotes("## 0.0.6_Alpha - 2026-09-25\n## 0.0.5_Alpha - 2026-09-25\n"));

            Assert.AreEqual(2, changelog.Entries.Count);
        }

        [TestCase("## ")]
        [TestCase("##")]
        [TestCase("## Released")]
        [TestCase("## unreleased")]
        [TestCase("## 2023-13-45")]
        [TestCase("## 2023-10-15 - 0.0.5_Alpha")]
        [TestCase("## 0.0.5_Alpha")]
        [TestCase("## 0.0.5_Alpha - soon")]
        [TestCase("## - 2026-09-25")]
        [TestCase("## 0.0.5_Alpha+1b3c7aa - 2026-09-25")]
        public void MalformedEntryHeading_IsAnErrorAtItsLine(string heading)
        {
            Assert.AreEqual(2, Reject(PatchNotes(heading + "\n")).Line);
        }

        [Test]
        public void EntryHeadingOutsidePatchNotes_IsAnError()
        {
            Assert.AreEqual(4, Reject("# Patch Notes\n# Known Issues\n- One\n## 2023-10-15\n# Roadmap\n").Line);
        }

        [Test]
        public void TextOutsideABullet_IsAnError()
        {
            Assert.AreEqual(4, Reject("# Patch Notes\n# Known Issues\n- One\nloose text\n# Roadmap\n").Line);
        }

        [Test]
        public void NestedBullet_IsAnError()
        {
            Assert.AreEqual(4, Reject("# Patch Notes\n# Known Issues\n- One\n  - Nested\n# Roadmap\n").Line);
        }

        [Test]
        public void TabIndentedBullet_IsAnError()
        {
            Assert.AreEqual(4, Reject("# Patch Notes\n# Known Issues\n- One\n\t- Nested\n# Roadmap\n").Line);
        }

        [Test]
        public void Bold_IsAnError()
        {
            Assert.AreEqual(4, Reject("# Patch Notes\n# Known Issues\n# Roadmap\n- A **loud** word\n").Line);
        }

        [Test]
        public void Link_IsAnError()
        {
            Assert.AreEqual(4, Reject("# Patch Notes\n# Known Issues\n# Roadmap\n- See [the page](https://example.com)\n").Line);
        }

        [Test]
        public void TableRow_IsAnError()
        {
            Assert.AreEqual(4, Reject("# Patch Notes\n# Known Issues\n# Roadmap\n| a | b |\n").Line);
        }

        [Test]
        public void DeeperHeading_IsAnError()
        {
            Assert.AreEqual(4, Reject("# Patch Notes\n# Known Issues\n# Roadmap\n### Deep\n").Line);
        }

        [Test]
        public void StarBullet_IsAnError()
        {
            Assert.AreEqual(4, Reject("# Patch Notes\n# Known Issues\n# Roadmap\n* Star\n").Line);
        }

        [Test]
        public void EmptyBullet_IsAnError()
        {
            Assert.AreEqual(4, Reject("# Patch Notes\n# Known Issues\n# Roadmap\n- \n").Line);
        }
    }
}
