using System;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The one commit-or-rollback move through <see cref="IItemReceiver.PickUpItem"/> - #35's
    /// own still-open question ("does PickUpItem need to become transactional?"), answered
    /// once here instead of hand-rolled per caller. #35 wired the right-click vendor buy
    /// through <see cref="IItemReceiver"/> but left the shift-click buy on a plain bag add
    /// (<c>AbstractSlotDisplay.cs</c>'s <see cref="QuickMoveIntentKind.Buy"/> arm), and #86's
    /// Stash-retrieval row then reinvented this same enroll/remove/pick-up/commit-or-rollback
    /// sequence independently - two near-identical copies with no ticket forcing either to
    /// notice the other, and nothing stopping a third caller from skipping
    /// <see cref="IItemReceiver"/> altogether the way the shift-click buy did. This is that
    /// one place, so a future acquisition path calls here rather than choosing between
    /// hand-rolling the wrapper again or bypassing auto-equip entirely.
    /// </summary>
    public static class PickUpTransaction
    {
        /// <summary>
        /// Removes the Package at <paramref name="sourceCell"/> from <paramref name="source"/>
        /// and hands it to <paramref name="player"/>. A pick-up that finds no room anywhere
        /// rolls the whole move back - the item stays at <paramref name="sourceCell"/>, and
        /// <paramref name="onAcquired"/> never runs. Only once the pick-up succeeds does
        /// <paramref name="onAcquired"/> get a chance to queue a further commit-time effect
        /// (a vendor's payment) on the same transaction, before <see cref="ItemTransaction.Commit"/>.
        /// </summary>
        /// <param name="inventory">Enrolled so a plain bag add rolls back with everything else.</param>
        /// <param name="equipment">Enrolled so an auto-equip inside the pick-up rolls back
        /// with everything else.</param>
        /// <param name="stash">Enrolled so the pick-up's debug stash-fallback (if it fires)
        /// rolls back with everything else. Null-safely omitted when the source already is
        /// the Stash - enrolling the same container twice is a no-op, but callers that never
        /// touch the Stash (a shelf buy) have nothing to pass.</param>
        /// <param name="onAcquired">Runs only after a successful pick-up, before commit - the
        /// seam a vendor buy queues its payment through.</param>
        /// <returns>Whether the Package was placed.</returns>
        public static bool Run(AbstractDimensionalContainer source, Vector2Int sourceCell,
            IItemReceiver player, AbstractDimensionalContainer inventory, AbstractDimensionalContainer equipment,
            AbstractDimensionalContainer stash = null, Action<ItemTransaction> onAcquired = null)
        {
            if (source == null || player == null
                || !source.TryGetPackageAt(sourceCell, out var stored) || !stored.IsValid)
                return false;

            using var transaction = new ItemTransaction(source, inventory, equipment, stash);

            _ = source.RemoveAtPosition(sourceCell, stored);

            if (!player.PickUpItem(stored.Item, stored.Amount))
                return false; // dispose rolls back - the item stays at sourceCell

            onAcquired?.Invoke(transaction);

            transaction.Commit();
            return true;
        }
    }
}
