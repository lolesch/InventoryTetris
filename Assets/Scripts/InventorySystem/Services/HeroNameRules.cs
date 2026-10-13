using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>What <see cref="HeroNameRules.Check"/> decided about a typed name.</summary>
    public readonly struct HeroNameCheck
    {
        private HeroNameCheck(string name, string reason)
        {
            Name = name;
            Reason = reason;
        }

        public static HeroNameCheck Accepted(string name) => new(name, string.Empty);
        public static HeroNameCheck Refused(string reason) => new(null, reason);

        /// <summary>Whether the name may be used to create a hero.</summary>
        public bool IsValid => Name != null;

        /// <summary>The trimmed name; <c>null</c> when refused.</summary>
        public string Name { get; }

        /// <summary>A short player-facing cause; empty when the name is valid.</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// The rules a new hero's name passes before anything is created: trimmed, one to
    /// <see cref="MaxLength"/> characters, none a file name cannot hold. Duplicates are allowed: the
    /// generated id is the identity. The refused set is fixed (Windows' plus control characters), not
    /// the running platform's, so a name is valid on every platform the save may move between.
    /// </summary>
    public static class HeroNameRules
    {
        public const int MaxLength = 20;

        private const string Forbidden = "<>:\"/\\|?*";

        public static HeroNameCheck Check(string name)
        {
            var trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0)
                return HeroNameCheck.Refused("Enter a name.");

            if (trimmed.Length > MaxLength)
                return HeroNameCheck.Refused($"A name is at most {MaxLength} characters; this one has {trimmed.Length}.");

            var offending = new List<string>();

            foreach (var character in trimmed)
            {
                var shown = char.IsControl(character) ? "a control character" : character.ToString();

                if ((char.IsControl(character) || Forbidden.IndexOf(character) >= 0) && !offending.Contains(shown))
                    offending.Add(shown);
            }

            return offending.Count > 0
                ? HeroNameCheck.Refused("A name cannot contain " + string.Join(" ", offending))
                : HeroNameCheck.Accepted(trimmed);
        }
    }
}
