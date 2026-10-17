using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// What the hero wears and carries as <see cref="RunSettlement"/> needs to see it (ADR-0009):
    /// read and empty all of it on a Death - gear, bag and coins, each with the cell it lay in - and
    /// offer one package back on a recovery. The engine binds this to the live equipment and
    /// inventory; a test binds an in-memory list. A Death fee is the Wallet's
    /// (<see cref="ISettlementLedger"/>) and is taken before this is read.
    /// </summary>
    public interface ISettlementBag
    {
        /// <summary>
        /// Collect everything the hero wears and carries and remove it, so he is left bare. A stack
        /// of <c>N</c> is one entry of amount <c>N</c>.
        /// </summary>
        IReadOnlyList<CorpseEntry> TakeEverything();

        /// <summary>
        /// Put <paramref name="entry"/> back where it lay when that cell is free, else wherever it fits
        /// (a bag item never auto-equips). Returns how much of the stack found no room, <c>0</c> when all did.
        /// </summary>
        uint Restore(CorpseEntry entry);
    }
}
