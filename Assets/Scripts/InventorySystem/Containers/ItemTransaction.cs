using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The scope every multi-step item move runs inside. Opening a transaction over the
    /// containers a move may touch swaps each one's
    /// <see cref="AbstractDimensionalContainer.StoredPackages"/> for a working copy: the
    /// existing placement code then mutates the copy, an observer sees nothing, and the
    /// content-changed refresh, character-stat apply/remove, drag handover and vendor
    /// currency mint/pay become entries on an effect list rather than inline side effects.
    ///
    /// <para>
    /// <see cref="Commit"/> writes every working copy back into its container's original
    /// dictionary instance - so the Inspector and any serialized reference stay pointed at
    /// live state - fires each touched container's
    /// <see cref="AbstractDimensionalContainer.OnContentChanged"/> exactly once, then runs
    /// the queued effects in order. Disposing without a commit - including an exception
    /// unwinding a <c>using</c> - rolls every container back to its snapshot and drops the
    /// effect list; the character sheet and the cursor were never touched, so they need no
    /// snapshot to be restored.
    /// </para>
    ///
    /// <para>Value is conserved by construction: an item is never removed from live state
    /// until a commit places it somewhere. The primitive depends on no provider.</para>
    /// </summary>
    public sealed class ItemTransaction : IDisposable
    {
        private readonly List<AbstractDimensionalContainer> enrolled = new();
        private readonly List<AbstractDimensionalContainer> touched = new();
        private readonly List<AbstractDimensionalContainer> reHomeChain = new();
        private readonly List<Action> effects = new();
        private readonly CursorHolder cursor;
        private bool finished;
        private bool aborted;
        private bool swapInPlace;

        /// <param name="cursor">The drag cursor as a one-capacity destination for a
        /// displaced item, or null when a move cannot touch the cursor (auto-sort, a
        /// vendor sale).</param>
        /// <param name="containers">Every container the move may add to, remove from or
        /// displace within. Nulls and repeats are ignored.</param>
        public ItemTransaction(CursorHolder cursor, params AbstractDimensionalContainer[] containers)
        {
            this.cursor = cursor;

            try
            {
                if (containers != null)
                    foreach (var container in containers)
                    {
                        if (container == null || enrolled.Contains(container))
                            continue;

                        container.JoinTransaction(this);
                        enrolled.Add(container);
                    }
            }
            catch
            {
                // A container was already in another transaction - un-enrol the ones this
                // constructor did join before letting the throw out.
                foreach (var container in enrolled)
                    container.DiscardTransaction();
                enrolled.Clear();
                throw;
            }
        }

        /// <inheritdoc cref="ItemTransaction(CursorHolder, AbstractDimensionalContainer[])"/>
        public ItemTransaction(params AbstractDimensionalContainer[] containers) : this(null, containers) { }

        /// <summary>
        /// Names the containers a displaced item is re-homed through, in order - normally
        /// just the one container the incoming item came from, so a swap stays "in place"
        /// (issue #10). Each must already be enrolled. Fluent: call it on the constructor
        /// result. Nulls and repeats are ignored.
        /// </summary>
        public ItemTransaction ReHomeThrough(params AbstractDimensionalContainer[] destinations)
        {
            if (destinations != null)
                foreach (var destination in destinations)
                {
                    if (destination == null || reHomeChain.Contains(destination))
                        continue;

                    if (!enrolled.Contains(destination))
                        throw new InvalidOperationException(
                            $"{destination.GetType().Name} must be enrolled in the transaction before it can be a re-home destination.");

                    reHomeChain.Add(destination);
                }

            return this;
        }

        /// <summary>Whether a re-home failed - <see cref="Commit"/> now rolls back instead.</summary>
        public bool Aborted => aborted;

        /// <summary>
        /// Marks this move as a right-click "swap in place": a displaced item is re-homed
        /// through the <see cref="ReHomeThrough"/> containers first and only overflows to
        /// the hand. A drag leaves this unset, and the item the player dropped onto goes
        /// straight to the hand. Fluent; no effect on a move that displaces nothing.
        /// </summary>
        public ItemTransaction SwapInPlace()
        {
            swapInPlace = true;
            return this;
        }

        /// <summary>Whether <see cref="SwapInPlace"/> was set.</summary>
        public bool SwapsInPlace => swapInPlace;

        /// <summary>
        /// Re-homes the item a drag dropped onto - the swap partner: the freed cursor first
        /// (the hand), and only if the hand is somehow already taken, each
        /// <see cref="ReHomeThrough"/> destination in order. Nothing takes it - the move is
        /// aborted and <see cref="Commit"/> rolls back.
        /// </summary>
        /// <param name="package">The item being re-homed.</param>
        /// <param name="origin">The container <paramref name="package"/> is being displaced
        /// from - forwarded to the cursor (issue #29) so a later cancel returns it there,
        /// not to wherever the drag itself started.</param>
        /// <param name="originPosition">The cell in <paramref name="origin"/>
        /// <paramref name="package"/> was displaced from.</param>
        /// <returns>False when the item found no home; <paramref name="package"/> is then
        /// whatever could not be placed.</returns>
        public bool TryReHomeToHandOrContainer(ref Package package, AbstractDimensionalContainer origin, Vector2Int originPosition)
        {
            if (!package.IsValid)
                return true;

            if (TryPlaceInHand(ref package, origin, originPosition) || TryPlaceInChain(ref package))
                return true;

            aborted = true;
            return false;
        }

        /// <summary>
        /// Re-homes a displaced item to a container only - each <see cref="ReHomeThrough"/>
        /// destination in order, at any free space - never the hand. A two-hander's
        /// collateral off-hand takes this path: it swaps back into the origin, or the whole
        /// move rolls back.
        /// </summary>
        public bool TryReHomeToContainer(ref Package package)
        {
            if (!package.IsValid)
                return true;

            if (TryPlaceInChain(ref package))
                return true;

            aborted = true;
            return false;
        }

        /// <summary>
        /// Re-homes a displaced item through the <see cref="ReHomeThrough"/> containers
        /// first and, only if none has room, into the hand. The right-click "swap in place"
        /// path, and the always-executes unequip / quick-move overflow. A second item that
        /// reaches an already-full hand aborts the move.
        /// </summary>
        /// <param name="package">The item being re-homed.</param>
        /// <param name="origin">The container <paramref name="package"/> is being displaced
        /// from - forwarded to the cursor (issue #29) so a later cancel returns it there.</param>
        /// <param name="originPosition">The cell in <paramref name="origin"/>
        /// <paramref name="package"/> was displaced from.</param>
        public bool TryReHomeToContainerOrHand(ref Package package, AbstractDimensionalContainer origin, Vector2Int originPosition)
        {
            if (!package.IsValid)
                return true;

            if (TryPlaceInChain(ref package) || TryPlaceInHand(ref package, origin, originPosition))
                return true;

            aborted = true;
            return false;
        }

        /// <summary>Tries each <see cref="ReHomeThrough"/> container in order, at any free space.</summary>
        private bool TryPlaceInChain(ref Package package)
        {
            foreach (var destination in reHomeChain)
                if (destination.TryAddToContainer(ref package))
                    return true;

            return false;
        }

        /// <summary>Hands the item to the freed cursor, once, while it is still free.</summary>
        private bool TryPlaceInHand(ref Package package, AbstractDimensionalContainer origin, Vector2Int originPosition)
        {
            if (cursor == null || !cursor.TryHold(package, origin, originPosition))
                return false;

            package = default;
            return true;
        }

        /// <summary>
        /// Appends a side effect to run once, in order, after a successful
        /// <see cref="Commit"/>. Dropped unrun if the transaction rolls back. This is where
        /// character-stat apply/remove, the drag handover and vendor currency mint/pay go
        /// so a rolled-back move leaves those surfaces exactly as they were.
        /// </summary>
        public void QueueEffect(Action effect)
        {
            if (effect != null)
                effects.Add(effect);
        }

        /// <summary>
        /// Records that an enrolled container would have raised
        /// <see cref="AbstractDimensionalContainer.OnContentChanged"/>. Kept once per
        /// container; <see cref="Commit"/> replays it exactly once, whatever the mutation
        /// count.
        /// </summary>
        internal void NoteContentChanged(AbstractDimensionalContainer container)
        {
            if (!touched.Contains(container))
                touched.Add(container);
        }

        /// <summary>
        /// Swaps every working copy in as real state, fires each touched container's
        /// content-changed once, hands the cursor its displaced item, then runs the queued
        /// effects in order. Idempotent - a second call, and the <see cref="Dispose"/> that
        /// closes the <c>using</c>, do nothing.
        /// </summary>
        public void Commit()
        {
            if (finished)
                return;

            // A re-home that found no home leaves the move un-completable: commit becomes a
            // rollback, so a caller that does not check the re-home result / Aborted stays safe.
            if (aborted)
            {
                Dispose();
                return;
            }

            finished = true;

            foreach (var container in enrolled)
                container.ApplyTransaction(touched.Contains(container));

            foreach (var container in touched)
                container.RaiseContentChangedNow();

            cursor?.Apply();

            // Index walk, not foreach: an effect is free to queue another (a currency mint
            // that spills change into a further cell), and that must not throw here.
            for (var i = 0; i < effects.Count; i++)
                effects[i]();

            effects.Clear();
        }

        /// <summary>Rolls every container back to its snapshot unless <see cref="Commit"/> already ran.</summary>
        public void Dispose()
        {
            if (finished)
                return;

            finished = true;

            cursor?.Discard();

            foreach (var container in enrolled)
                container.DiscardTransaction();

            effects.Clear();
            touched.Clear();
            reHomeChain.Clear();
        }
    }
}
