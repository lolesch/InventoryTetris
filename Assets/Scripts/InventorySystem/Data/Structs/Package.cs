using System;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

[assembly: InternalsVisibleTo("InventorySystem.Data.Tests")]

namespace ToolSmiths.InventorySystem.Data
{
    [Serializable]
    public struct Package
    {
        /// <summary>The package contains an amount of items and can be stored inside containers</summary>
        public Package(AbstractDimensionalContainer sender, ItemInstance item, uint amount)
        {
            Sender = sender;
            Item = item;
            Amount = amount;

            // ItemView.Catalog is unset during deserialization and in the pure container
            // tests that build a package before wiring a catalog - skip the check then.
            if (item != null && ItemView.Catalog != null && ItemView.Of(item).StackLimit < amount)
                Debug.LogWarning($"The Package you constructed contains more items than the item's stacking limit!");
        }

        [field: SerializeField] public AbstractDimensionalContainer Sender { get; private set; }
        // Not [SerializeField]: ItemInstance is a plain, non-[Serializable] class - a saved
        // container round-trips through ItemInstanceDto, not Unity serialization (see the spec).
        public ItemInstance Item { get; private set; }

        [field: SerializeField] public uint Amount { get; private set; }

        public readonly uint SpaceLeft => ItemView.Of(Item).StackLimit - Amount;
        public readonly bool IsValid => Item != null && 0 < Amount;

        /// <summary>Tries to add to the amount (within stacking limit).</summary>
        /// <returns>The amount that was added</returns>
        public uint IncreaseAmount(uint amountToAdd)
        {
            if (0 == amountToAdd)
                return 0;

            var added = Math.Min(SpaceLeft, amountToAdd);
            Amount += added;

            return added;
        }

        /// <summary>Tries to remove the amount from the current stack</summary>
        /// <returns>The amount that was removed</returns>
        public uint ReduceAmount(uint amountToRemove)
        {
            if (0 == amountToRemove)
                return 0;

            var removed = Math.Min(Amount, amountToRemove);
            Amount -= removed;

            return removed;
        }

        //public bool TryReturnToSender() => Sender.TryAddToContainer(ref this);
    }
}
