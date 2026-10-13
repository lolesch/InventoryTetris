using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ToolSmiths.InventorySystem.Changelogs
{
    /// <summary>Turns the Changelog file's text into a <see cref="Changelog"/>, or names the line it cannot read.</summary>
    public static class ChangelogParser
    {
        private static readonly string[] Sections = { "Patch Notes", "Known Issues", "Roadmap" };
        private const int KnownIssues = 1;
        private const int Roadmap = 2;

        private static readonly Regex Link = new Regex(@"\[[^\]]*\]\([^)]*\)");

        public static bool TryParse(string text, out Changelog changelog, out ChangelogParseError error)
        {
            changelog = null;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var lineCount = lines.Length;
            if (lines[lineCount - 1].Length == 0) lineCount--; // the newline that ends the file

            var knownIssues = new List<string>();
            var roadmap = new List<string>();
            var section = -1;

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
                    if (!TryOpenSection(line, number, ref section, out error)) return false;
                    continue;
                }

                if (line[0] == '|')
                {
                    error = new ChangelogParseError(number, "Tables are not supported.");
                    return false;
                }

                if (line != "-" && !line.StartsWith("- "))
                {
                    error = new ChangelogParseError(number, "Text outside a '- ' bullet is not supported.");
                    return false;
                }

                var bullet = line.Substring(1).Trim();
                if (!IsPlainBullet(bullet, number, section, out error)) return false;

                (section == KnownIssues ? knownIssues : roadmap).Add(bullet);
            }

            if (section < Roadmap)
            {
                error = new ChangelogParseError(lineCount + 1, $"Missing section '# {Sections[section + 1]}'.");
                return false;
            }

            changelog = new Changelog(knownIssues, roadmap);
            error = default;
            return true;
        }

        private static bool TryOpenSection(string heading, int number, ref int section, out ChangelogParseError error)
        {
            error = default;
            if (heading.StartsWith("##"))
            {
                error = new ChangelogParseError(number, "Only '# ' headings are supported.");
                return false;
            }

            var name = heading.StartsWith("# ") ? heading.Substring(2).Trim() : null;
            var index = System.Array.IndexOf(Sections, name);
            if (index < 0)
            {
                error = new ChangelogParseError(number, "Expected '# Patch Notes', '# Known Issues' or '# Roadmap'.");
                return false;
            }

            if (index <= section)
            {
                error = new ChangelogParseError(number, $"Duplicate section '# {name}'.");
                return false;
            }

            if (index != section + 1)
            {
                error = new ChangelogParseError(number, $"Expected '# {Sections[section + 1]}' before '# {name}'.");
                return false;
            }

            section = index;
            return true;
        }

        private static bool IsPlainBullet(string bullet, int number, int section, out ChangelogParseError error)
        {
            error = default;
            string message = null;
            if (section < KnownIssues) message = "A bullet belongs under '# Known Issues' or '# Roadmap'; Patch Notes bullets need an entry heading.";
            else if (bullet.Length == 0) message = "Empty bullet.";
            else if (bullet.Contains("**")) message = "Bold is not supported.";
            else if (Link.IsMatch(bullet)) message = "Links are not supported.";

            if (message == null) return true;
            error = new ChangelogParseError(number, message);
            return false;
        }
    }
}
