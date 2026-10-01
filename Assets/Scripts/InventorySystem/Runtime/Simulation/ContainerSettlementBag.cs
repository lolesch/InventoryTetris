using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Binds <see cref="ISettlementBag"/> to the live player bag (<c>InventoryProvider.Instance.Inventory</c>)
    /// for the Corpse burial, and to the player's acquisition entry point for recovery.
    /// Stateless: it resolves the provider on each call, so the one long-lived
    /// <see cref="RunSettlement"/> always acts on the current bag.
    /// </summary>
    public sealed class ContainerSettlementBag : ISettlementBag
    {
        /// <summary>
        /// One pass over the bag: collect every non-currency package's contents (a stack of
        /// <c>N</c> yields <c>N</c> instances), remove them, hand the contents back for the Corpse.
        /// Coins stay with the Wallet; equipped gear never lives in the bag.
        /// </summary>
        public IReadOnlyList<ItemInstance> TakeNonCurrencyContents()
        {
            var bag = InventoryProvider.Instance.Inventory;

            var doomed = bag.StoredPackages
                .Where(entry => ItemView.Of(entry.Value.Item).Definition.Category != ItemCategory.Currency)
                .ToList();

            var contents = doomed
                .SelectMany(entry => Enumerable.Repeat(entry.Value.Item, (int)entry.Value.Amount))
                .ToArray();

            foreach (var entry in doomed)
                _ = bag.RemoveAtPosition(entry.Key, entry.Value);

            return contents;
        }

        /// <summary>
        /// Through the player's acquisition entry point, so a recovered piece of gear
        /// auto-equips into an empty slot exactly as a fresh Drop does - not a raw bag add. With
        /// no live player it refuses, so <see cref="RunSettlement.Recover"/> grounds or re-buries
        /// the item rather than losing it (Unity's lifetime-aware <c>==</c>: a destroyed
        /// <c>LocalPlayer</c> is not literally null).
        /// </summary>
        public bool TryStore(ItemInstance item)
        {
            var provider = CharacterProvider.Instance;
            var player = provider != null ? provider.Player : null;

            return player != null && player.PickUpItem(item, 1u);
        }
    }
}
