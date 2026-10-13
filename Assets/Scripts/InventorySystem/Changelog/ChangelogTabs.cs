using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Changelogs
{
    /// <summary>What the Changelog tabs show for the file's text, so the panel only binds lines onto rows.</summary>
    public sealed class ChangelogTabs
    {
        /// <summary>Starts every bullet line; the glyph is in the default TMP font's atlas.</summary>
        public const string BulletPrefix = "• ";

        public const string NoKnownIssues = "No known issues.";
        public const string NoRoadmap = "Nothing planned yet.";

        public IReadOnlyList<string> KnownIssues { get; }
        public IReadOnlyList<string> Roadmap { get; }

        /// <summary>True when the file did not parse: both tabs then show the failure line.</summary>
        public bool Failed => FailureLog != null;

        /// <summary>The error with its file line for the log; null when the file parsed.</summary>
        public string FailureLog { get; }

        private ChangelogTabs(IReadOnlyList<string> knownIssues, IReadOnlyList<string> roadmap, string failureLog = null)
        {
            KnownIssues = knownIssues;
            Roadmap = roadmap;
            FailureLog = failureLog;
        }

        public static ChangelogTabs Read(string text)
        {
            if (!ChangelogParser.TryParse(text, out var changelog, out var error))
            {
                var failure = new[] { $"The changelog could not be read (line {error.Line})." };
                return new ChangelogTabs(failure, failure, $"Changelog line {error.Line}: {error.Message}");
            }

            return new ChangelogTabs(Lines(changelog.KnownIssues, NoKnownIssues), Lines(changelog.Roadmap, NoRoadmap));
        }

        private static IReadOnlyList<string> Lines(IReadOnlyList<string> bullets, string placeholder)
        {
            if (bullets.Count == 0) return new[] { placeholder };

            var lines = new string[bullets.Count];
            for (var i = 0; i < lines.Length; i++) lines[i] = BulletPrefix + bullets[i];
            return lines;
        }
    }
}
