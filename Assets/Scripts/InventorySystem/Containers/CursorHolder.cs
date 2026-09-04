using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The drag cursor dressed as a one-capacity destination, so "the displaced item goes
    /// to the cursor" is the same shape as "the displaced item goes to the inventory" and
    /// a displace cascade can try it with the same code.
    ///
    /// <para>A move opens with the dragged item already in hand; the drop places it,
    /// freeing the cursor, and the cascade may hand one displaced item back here. Nothing
    /// reaches the real <see cref="ICursorSink"/> until
    /// <see cref="ItemTransaction.Commit"/>; a rolled-back move never calls the sink, so
    /// the dragged item stays in hand untouched.</para>
    /// </summary>
    public sealed class CursorHolder
    {
        private readonly ICursorSink sink;
        private Package pending;
        private AbstractDimensionalContainer pendingOrigin;
        private Vector2Int pendingOriginPosition;

        /// <param name="sink">The drag cursor a held package is handed to on commit. Null
        /// in a test that only checks the holder's book-keeping.</param>
        public CursorHolder(ICursorSink sink) => this.sink = sink;

        /// <summary>Whether the freed cursor can still take a displaced item this move.</summary>
        public bool IsFree => !pending.IsValid;

        /// <summary>
        /// Records the item the cursor will hold once the transaction commits, together
        /// with the container and cell it is being displaced from - what a later cancel
        /// must return it to (issue #29's mid-drag-swap gap). One capacity: a second call
        /// while already holding fails, and the caller moves on to the next destination.
        /// </summary>
        /// <returns>False when the holder is already full or the package is invalid.</returns>
        public bool TryHold(Package package, AbstractDimensionalContainer origin, Vector2Int originPosition)
        {
            if (!IsFree || !package.IsValid)
                return false;

            pending = package;
            pendingOrigin = origin;
            pendingOriginPosition = originPosition;
            return true;
        }

        /// <summary>Commit: hand the held package to the real cursor. A no-op if nothing was held.</summary>
        internal void Apply()
        {
            if (pending.IsValid)
                sink?.ReplacePackage(pending, pendingOrigin, pendingOriginPosition);
        }

        /// <summary>Rollback: forget the held package. The sink was never called.</summary>
        internal void Discard()
        {
            pending = default;
            pendingOrigin = null;
            pendingOriginPosition = default;
        }
    }
}
