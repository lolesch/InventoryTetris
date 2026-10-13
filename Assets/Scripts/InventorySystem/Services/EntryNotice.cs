using Submodules.Utility.Persistence;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Persistence;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What the game scene tells the player once on entering, after a load that went wrong: the latest save was
    /// damaged and its backup was loaded, and/or some saved packages could not be placed. Picked from the load's
    /// <see cref="LoadStatus"/> and <see cref="RestoreReport"/> by <see cref="From"/>; a clean load has none.
    /// </summary>
    public sealed class EntryNotice
    {
        /// <summary>Said for a hero that was loaded from its backup.</summary>
        public const string BackupText = "Your latest save was damaged. Loaded the previous one, so recent progress may be missing.";

        /// <summary>The most names the dialog has room for.</summary>
        public const int MaxListed = 8;

        private EntryNotice(bool restoredFromBackup, IReadOnlyList<string> unplacedNames)
        {
            RestoredFromBackup = restoredFromBackup;
            UnplacedNames = unplacedNames;
        }

        /// <summary>
        /// The notice for a load, or <c>null</c> when there is nothing to say. <paramref name="report"/> is
        /// <c>null</c> for a load that did not produce a hero.
        /// </summary>
        public static EntryNotice From(LoadStatus status, RestoreReport report)
        {
            var restoredFromBackup = status == LoadStatus.RestoredFromBackup;
            var names = report == null ? System.Array.Empty<string>() : report.Skipped.Select(NameOf).ToArray();

            return restoredFromBackup || names.Length != 0 ? new EntryNotice(restoredFromBackup, names) : null;
        }

        /// <summary>Whether the hero came from its backup because the latest file was unusable.</summary>
        public bool RestoredFromBackup { get; }

        /// <summary>One entry per saved package the restore left out, each naming what it was and where it was kept.</summary>
        public IReadOnlyList<string> UnplacedNames { get; }

        public int UnplacedCount => UnplacedNames.Count;

        /// <summary>"3 items couldn't be restored"; empty when every package landed.</summary>
        public string UnplacedText => UnplacedCount switch
        {
            0 => string.Empty,
            1 => "1 item couldn't be restored",
            _ => $"{UnplacedCount} items couldn't be restored",
        };

        /// <summary>The names behind "Show", one per line: the first <see cref="MaxListed"/>, then how many more.</summary>
        public string NamesText => UnplacedCount <= MaxListed
            ? string.Join("\n", UnplacedNames)
            : string.Join("\n", UnplacedNames.Take(MaxListed).Append($"and {UnplacedCount - MaxListed} more"));

        // The id is all that is left of an item the catalog no longer knows, so it is the name.
        private static string NameOf(SkippedPackage skipped)
        {
            var amount = skipped.Package.amount > 1u ? $" x{skipped.Package.amount}" : string.Empty;
            return $"{skipped.Package.instance.definitionId}{amount} ({skipped.Container})";
        }
    }
}
