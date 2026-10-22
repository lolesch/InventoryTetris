using System.Collections.Generic;
using System.Linq;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>Which saved container a package came from.</summary>
    public enum SavedContainer
    {
        Equipment,
        Inventory,
        Stash,

        /// <summary>The Hero's Corpse. Its items have no cell, so a skipped entry's x and y are 0 and its amount 1.</summary>
        Corpse,
    }

    /// <summary>Why a saved package was left out of the restored hero.</summary>
    public enum SkipReason
    {
        /// <summary>The definition id is not in the catalog any more.</summary>
        UnknownDefinition,

        /// <summary>It no longer fits its saved cell, and the Inventory has no free cell for it either.</summary>
        DoesNotFit,

        /// <summary>The saved stack size is zero, so there is nothing to place.</summary>
        EmptyStack,

        /// <summary>The Corpse's Location id is not an authored Location any more, so its items have nowhere to be recovered.</summary>
        UnknownLocation,

        /// <summary>The item holds a value this build cannot read (an enum name that no longer exists), so it cannot be rebuilt.</summary>
        Unreadable,
    }

    /// <summary>
    /// A saved package the restore could not place. <see cref="Package"/> is the entry as saved,
    /// except that <c>amount</c> is what was left unplaced, so a sidecar written from it never
    /// claims more than was lost.
    /// </summary>
    public readonly struct SkippedPackage
    {
        public SkippedPackage(SavedContainer container, PackageDto package, SkipReason reason)
        {
            Container = container;
            Package = package;
            Reason = reason;
        }

        public SavedContainer Container { get; }
        public PackageDto Package { get; }
        public SkipReason Reason { get; }
    }

    /// <summary>What a restore left out. Empty when every saved package landed.</summary>
    public sealed class RestoreReport
    {
        public RestoreReport(IReadOnlyList<SkippedPackage> skipped) => Skipped = skipped;

        public IReadOnlyList<SkippedPackage> Skipped { get; }

        public bool IsClean => Skipped.Count == 0;

        /// <summary>This report followed by <paramref name="other"/>, so the parts of one restore read as one list.</summary>
        public RestoreReport Plus(RestoreReport other) =>
            new(Skipped.Concat(other.Skipped).ToArray());
    }
}
