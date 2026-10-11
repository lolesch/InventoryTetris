using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Binds <see cref="ISettlementBag"/> to a <see cref="Hero"/>: its Inventory for the Corpse
    /// burial, and its acquisition entry point for recovery. Built over the hero the settlement
    /// is for, so a hero load needs nothing re-pointed - the next settlement is built over the new one.
    /// </summary>
    public sealed class ContainerSettlementBag : ISettlementBag
    {
        private readonly Hero _hero;

        public ContainerSettlementBag(Hero hero) =>
            _hero = hero ?? throw new ArgumentNullException(nameof(hero));

        /// <summary>
        /// One pass over the bag: collect every non-currency package's contents (a stack of
        /// <c>N</c> yields <c>N</c> instances), remove them, hand the contents back for the Corpse.
        /// Coins stay with the Wallet; equipped gear never lives in the bag.
        /// </summary>
        public IReadOnlyList<ItemInstance> TakeNonCurrencyContents()
        {
            var bag = _hero.Inventory;

            var doomed = bag.StoredPackages
                .Where(entry => bag.ViewOf(entry.Value.Item).Definition.Category != ItemCategory.Currency)
                .ToList();

            var contents = doomed
                .SelectMany(entry => Enumerable.Repeat(entry.Value.Item, (int)entry.Value.Amount))
                .ToArray();

            foreach (var entry in doomed)
                _ = bag.RemoveAtPosition(entry.Key, entry.Value);

            return contents;
        }

        /// <summary>
        /// Through the hero's acquisition entry point, so a recovered piece of gear auto-equips
        /// into an empty slot exactly as a fresh Drop does - not a raw bag add. <c>false</c> when
        /// it fits nowhere, so <see cref="RunSettlement.Recover"/> re-buries the item
        /// rather than losing it.
        /// </summary>
        public bool TryStore(ItemInstance item) => _hero.PickUpItem(item, 1u);
    }
}
