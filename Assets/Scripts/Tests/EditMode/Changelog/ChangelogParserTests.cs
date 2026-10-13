using NUnit.Framework;
using ToolSmiths.InventorySystem.Changelogs;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Changelogs
{
    /// <summary>The Changelog file grammar: text in, a Changelog or the line that is wrong out.</summary>
    [TestFixture]
    public sealed class ChangelogParserTests
    {
        private const string Empty = "# Patch Notes\n\n# Known Issues\n\n# Roadmap\n";

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
        public void BulletUnderPatchNotes_IsAnErrorUntilEntriesExist()
        {
            Assert.AreEqual(2, Reject("# Patch Notes\n- No entry\n# Known Issues\n# Roadmap\n").Line);
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
        public void SubHeading_IsAnErrorUntilEntriesExist()
        {
            Assert.AreEqual(2, Reject("# Patch Notes\n## 0.0.5_Alpha - 2026-09-25\n# Known Issues\n# Roadmap\n").Line);
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
