using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>Which saved container a package came from.</summary>
    public enum SavedContainer
    {
        Equipment,
        Inventory,
        Stash,
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
    }
}
