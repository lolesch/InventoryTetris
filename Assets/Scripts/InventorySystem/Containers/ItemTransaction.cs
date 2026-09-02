using System;
using System.Collections.Generic;

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
        private readonly List<Action> effects = new();
        private readonly CursorHolder cursor;
        private bool finished;

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
        }
    }
}
