using System.Collections.Generic;
using System.Globalization;

namespace ToolSmiths.InventorySystem.Changelogs
{
    /// <summary>What the Changelog tabs show for the file's text, so the panel only binds lines onto rows.</summary>
    public sealed class ChangelogTabs
    {
        /// <summary>Starts every bullet line; the glyph is in the default TMP font's atlas.</summary>
        private const string BulletPrefix = "• ";

        private const string NoPatchNotes = "No patch notes yet.";
        private const string NoKnownIssues = "No known issues.";
        private const string NoRoadmap = "Nothing planned yet.";

        /// <summary>The Patch Notes entries in file order; empty when the file did not parse or has none.</summary>
        public IReadOnlyList<PatchNotesGroup> Entries { get; }

        /// <summary>The one line the Patch Notes tab shows when there are no entries; null otherwise.</summary>
        public string PatchNotesPlaceholder { get; }

        public IReadOnlyList<string> KnownIssues { get; }
        public IReadOnlyList<string> Roadmap { get; }

        /// <summary>True when the file did not parse: every tab then shows the failure line.</summary>
        public bool Failed => FailureLog != null;

        /// <summary>The error with its file line for the log; null when the file parsed.</summary>
        public string FailureLog { get; }

        private ChangelogTabs(IReadOnlyList<PatchNotesGroup> entries, string patchNotesPlaceholder,
            IReadOnlyList<string> knownIssues, IReadOnlyList<string> roadmap, string failureLog = null)
        {
            Entries = entries;
            PatchNotesPlaceholder = patchNotesPlaceholder;
            KnownIssues = knownIssues;
            Roadmap = roadmap;
            FailureLog = failureLog;
        }

        /// <param name="development">True in the Editor and development builds, where the <c>Unreleased</c> entry is shown.</param>
        public static ChangelogTabs Read(string text, bool development)
        {
            if (!ChangelogParser.TryParse(text, out var changelog, out var error))
            {
                var failure = new[] { $"The changelog could not be read (line {error.Line})." };
                return new ChangelogTabs(System.Array.Empty<PatchNotesGroup>(), failure[0], failure, failure, $"Changelog line {error.Line}: {error.Message}");
            }

            var shown = Shown(changelog.Entries, development);
            var entries = new PatchNotesGroup[shown.Count];
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = shown[i];
                entries[i] = new PatchNotesGroup(Heading(entry), Lines(entry.Bullets));
            }

            return new ChangelogTabs(entries, entries.Length == 0 ? NoPatchNotes : null, Lines(changelog.KnownIssues, NoKnownIssues), Lines(changelog.Roadmap, NoRoadmap));
        }

        /// <summary>The entries a viewer sees: <c>Unreleased</c> is for the developer checking wording, so players never see it.</summary>
        internal static IReadOnlyList<PatchNotesEntry> Shown(IReadOnlyList<PatchNotesEntry> entries, bool development)
        {
            var shown = new List<PatchNotesEntry>(entries.Count);
            foreach (var entry in entries)
                if (development || !entry.IsUnreleased) shown.Add(entry);
            return shown;
        }

        /// <summary>A version-less build's label already is its date, and Unreleased has none.</summary>
        private static string Heading(PatchNotesEntry entry)
        {
            if (!entry.HasVersion) return entry.Label;

            return $"{entry.Label} - {entry.Date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        }

        private static IReadOnlyList<string> Lines(IReadOnlyList<string> bullets)
        {
            var lines = new string[bullets.Count];
            for (var i = 0; i < lines.Length; i++) lines[i] = BulletPrefix + bullets[i];
            return lines;
        }

        private static IReadOnlyList<string> Lines(IReadOnlyList<string> bullets, string placeholder)
        {
            return bullets.Count == 0 ? new[] { placeholder } : Lines(bullets);
        }
    }

    /// <summary>One Patch Notes entry as the tab shows it: a heading, then its bullet lines.</summary>
    public sealed class PatchNotesGroup
    {
        public string Heading { get; }
        public IReadOnlyList<string> Lines { get; }

        internal PatchNotesGroup(string heading, IReadOnlyList<string> lines)
        {
            Heading = heading;
            Lines = lines;
        }
    }
}
