using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ToolSmiths.InventorySystem.Changelogs
{
    /// <summary>Turns the Changelog file's text into a <see cref="Changelog"/>, or names the line it cannot read.</summary>
    internal static class ChangelogParser
    {
        /// <summary>The file's sections in the order it must list them; <c>None</c> before the first heading.</summary>
        private enum Section { None = -1, PatchNotes, KnownIssues, Roadmap }

        private static readonly string[] SectionNames = { "Patch Notes", "Known Issues", "Roadmap" };
        private const string UnreleasedHeading = "## " + PatchNotesEntry.UnreleasedLabel;

        private static readonly Regex EntryHeading = new Regex(@"^## (?:(?<version>[^\s+]+) - )?(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})$");

        private static readonly Regex Link = new Regex(@"\[[^\]]*\]\([^)]*\)");

        /// <summary>A tag-like <c>&lt;b&gt;</c> or <c>&lt;color=red&gt;</c>; TextMeshPro would render it as rich text.</summary>
        private static readonly Regex Tag = new Regex(@"<\s*/?\s*[A-Za-z#!][^>]*>");

        private static readonly Regex VersionNumbers = new Regex(@"^[0-9]+(?:\.[0-9]+)*");

        public static bool TryParse(string text, out Changelog changelog, out ChangelogParseError error)
        {
            changelog = null;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var lineCount = lines.Length;
            if (lines[lineCount - 1].Length == 0) lineCount--; // the newline that ends the file

            var entries = new List<PatchNotesEntry>();
            var knownIssues = new List<string>();
            var roadmap = new List<string>();
            var section = Section.None;
            List<string> entryBullets = null; // the open entry's list, null until the first entry heading

            for (var i = 0; i < lineCount; i++)
            {
                var number = i + 1;
                var line = lines[i].TrimEnd();
                if (line.Length == 0) continue;

                if (char.IsWhiteSpace(line[0]))
                {
                    error = new ChangelogParseError(number, "Indented text is not supported; nested bullets are not allowed.");
                    return false;
                }

                if (line[0] == '#')
                {
                    if (IsEntryHeading(line))
                    {
                        if (section != Section.PatchNotes)
                        {
                            error = new ChangelogParseError(number, "Entry headings belong under '# Patch Notes'.");
                            return false;
                        }

                        entryBullets = new List<string>();
                        if (!TryOpenEntry(line, number, entries, entryBullets, out error)) return false;
                    }
                    else if (!TryOpenSection(line, number, ref section, out error)) return false;

                    continue;
                }

                if (line[0] == '|')
                {
                    error = new ChangelogParseError(number, "Tables are not supported.");
                    return false;
                }

                if (line != "-" && !line.StartsWith("- ", StringComparison.Ordinal))
                {
                    error = new ChangelogParseError(number, "Text outside a '- ' bullet is not supported.");
                    return false;
                }

                var bullet = line.Substring(1).Trim();
                if (!IsPlainBullet(bullet, number, entryBullets != null || section > Section.PatchNotes, out error)) return false;

                (section == Section.PatchNotes ? entryBullets : section == Section.KnownIssues ? knownIssues : roadmap).Add(bullet);
            }

            if (section < Section.Roadmap)
            {
                error = new ChangelogParseError(lineCount + 1, $"Missing section '# {SectionNames[(int)section + 1]}'.");
                return false;
            }

            changelog = new Changelog(entries, knownIssues, roadmap);
            error = default;
            return true;
        }

        private static bool IsEntryHeading(string heading) => heading.StartsWith("##", StringComparison.Ordinal) && !heading.StartsWith("###", StringComparison.Ordinal);

        private static bool TryOpenEntry(string heading, int number, List<PatchNotesEntry> entries, List<string> bullets, out ChangelogParseError error)
        {
            error = default;
            var previous = entries.Count > 0 ? entries[entries.Count - 1] : null;

            if (heading == UnreleasedHeading)
            {
                if (previous != null)
                {
                    error = new ChangelogParseError(number, "'Unreleased' is only allowed as the first entry.");
                    return false;
                }

                entries.Add(new PatchNotesEntry(PatchNotesEntry.UnreleasedLabel, null, bullets));
                return true;
            }

            var match = EntryHeading.Match(heading);
            if (!match.Success || !DateTime.TryParseExact(match.Groups["date"].Value, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                error = new ChangelogParseError(number, "Expected '## <version> - <yyyy-MM-dd>', '## <yyyy-MM-dd>' or '## Unreleased'.");
                return false;
            }

            var label = match.Groups["version"].Success ? match.Groups["version"].Value : match.Groups["date"].Value;
            if (label == PatchNotesEntry.UnreleasedLabel)
            {
                error = new ChangelogParseError(number, "'Unreleased' is reserved for the heading '## Unreleased' and takes no date.");
                return false;
            }

            if (entries.Exists(e => e.Label == label))
            {
                error = new ChangelogParseError(number, $"Duplicate entry '{label}'.");
                return false;
            }

            if (previous?.Date != null && date > previous.Date)
            {
                error = new ChangelogParseError(number, $"Entries are newest first; '{label}' is newer than '{previous.Label}'.");
                return false;
            }

            if (match.Groups["version"].Success && previous != null && previous.HasVersion && IsNewerVersion(label, previous.Label))
            {
                error = new ChangelogParseError(number, $"Entries are newest first; version '{label}' is higher than '{previous.Label}'.");
                return false;
            }

            entries.Add(new PatchNotesEntry(label, date, bullets));
            return true;
        }

        /// <summary>Compares the leading numbers of two versions segment by segment; versions without any are not ordered.</summary>
        private static bool IsNewerVersion(string version, string other)
        {
            if (!TryNumbers(version, out var a) || !TryNumbers(other, out var b)) return false;

            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var x = i < a.Length ? a[i] : 0;
                var y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }

            return false;
        }

        private static bool TryNumbers(string version, out int[] numbers)
        {
            numbers = null;
            var match = VersionNumbers.Match(version);
            if (!match.Success) return false;

            var parts = match.Value.Split('.');
            numbers = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return false;
            return true;
        }

        private static bool TryOpenSection(string heading, int number, ref Section section, out ChangelogParseError error)
        {
            error = default;
            if (heading.StartsWith("###", StringComparison.Ordinal))
            {
                error = new ChangelogParseError(number, "Only '# ' and '## ' headings are supported.");
                return false;
            }

            var name = heading.StartsWith("# ", StringComparison.Ordinal) ? heading.Substring(2).Trim() : null;
            var index = Array.IndexOf(SectionNames, name);
            if (index < 0)
            {
                error = new ChangelogParseError(number, "Expected '# Patch Notes', '# Known Issues' or '# Roadmap'.");
                return false;
            }

            if (index <= (int)section)
            {
                error = new ChangelogParseError(number, $"Duplicate section '# {name}'.");
                return false;
            }

            if (index != (int)section + 1)
            {
                error = new ChangelogParseError(number, $"Expected '# {SectionNames[(int)section + 1]}' before '# {name}'.");
                return false;
            }

            section = (Section)index;
            return true;
        }

        private static bool IsPlainBullet(string bullet, int number, bool hasList, out ChangelogParseError error)
        {
            error = default;
            string message = null;
            if (!hasList) message = "A bullet belongs under an entry heading, '# Known Issues' or '# Roadmap'.";
            else if (bullet.Length == 0) message = "Empty bullet.";
            else if (bullet.Contains("**")) message = "Bold is not supported.";
            else if (Link.IsMatch(bullet)) message = "Links are not supported.";
            else if (bullet.Contains('*') || bullet.Contains('`')) message = "Italics and inline code are not supported.";
            else if (bullet[0] == '|') message = "Tables are not supported.";
            else if (Tag.IsMatch(bullet)) message = "Tags such as <b> are not supported.";

            if (message == null) return true;
            error = new ChangelogParseError(number, message);
            return false;
        }
    }
}
