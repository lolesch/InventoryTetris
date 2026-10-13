using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Changelogs
{
    /// <summary>
    /// The parsed Changelog file: the player-facing lists the panel renders. The Patch Notes entries
    /// join this value with the entry-heading parser.
    /// </summary>
    public sealed class Changelog
    {
        public IReadOnlyList<string> KnownIssues { get; }
        public IReadOnlyList<string> Roadmap { get; }

        public Changelog(IReadOnlyList<string> knownIssues, IReadOnlyList<string> roadmap)
        {
            KnownIssues = knownIssues;
            Roadmap = roadmap;
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
