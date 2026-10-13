using System;
using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Changelogs
{
    /// <summary>The parsed Changelog file: the player-facing content the panel renders.</summary>
    public sealed class Changelog
    {
        /// <summary>Patch Notes entries, newest first; an <c>Unreleased</c> entry can only be the first.</summary>
        public IReadOnlyList<PatchNotesEntry> Entries { get; }
        public IReadOnlyList<string> KnownIssues { get; }
        public IReadOnlyList<string> Roadmap { get; }

        public Changelog(IReadOnlyList<PatchNotesEntry> entries, IReadOnlyList<string> knownIssues, IReadOnlyList<string> roadmap)
        {
            Entries = entries;
            KnownIssues = knownIssues;
            Roadmap = roadmap;
        }
    }

    /// <summary>One build's Patch Notes: a version, a date for builds that predate versions, or <c>Unreleased</c>.</summary>
    public sealed class PatchNotesEntry
    {
        public const string UnreleasedLabel = "Unreleased";

        /// <summary>The version, the date text of a version-less build, or <see cref="UnreleasedLabel"/>.</summary>
        public string Label { get; }

        /// <summary>Null only for the <c>Unreleased</c> entry.</summary>
        public DateTime? Date { get; }

        public IReadOnlyList<string> Bullets { get; }
        public bool IsUnreleased => Date == null;

        public PatchNotesEntry(string label, DateTime? date, IReadOnlyList<string> bullets)
        {
            Label = label;
            Date = date;
            Bullets = bullets;
        }
    }

    /// <summary>Why the file was rejected, and the 1-based line the author has to fix.</summary>
    public readonly struct ChangelogParseError
    {
        public int Line { get; }
        public string Message { get; }

        public ChangelogParseError(int line, string message)
        {
            Line = line;
            Message = message;
        }
    }
}
