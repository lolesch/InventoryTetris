using System;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

[assembly: InternalsVisibleTo("InventorySystem.Containers.Tests")]

// Package moved into the InventorySystem.Containers assembly with the container core
// (#15); the ToolSmiths.InventorySystem.Data namespace is kept so no call site's
// `using` changes. A namespace move to .Inventories is a separate cosmetic pass.
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
        }

        [field: SerializeField] public AbstractDimensionalContainer Sender { get; private set; }
        // Not [SerializeField]: ItemInstance is a plain, non-[Serializable] class - a saved
        // container round-trips through ItemInstanceDto, not Unity serialization (see the spec).
        public ItemInstance Item { get; private set; }

        [field: SerializeField] public uint Amount { get; private set; }

        /// <summary>How many more of <see cref="Item"/> fit in this stack, against <paramref name="catalog"/>'s stack limit.</summary>
        public readonly uint SpaceLeft(IItemCatalog catalog) => ItemView.Resolve(Item, catalog).StackLimit - Amount;
        public readonly bool IsValid => Item != null && 0 < Amount;

        /// <summary>
        /// How much of this stack a pick-up lifts: all of it, or with <paramref name="half"/>
        /// (Ctrl) the larger half of a splittable stack. The one statement of the split, so what
        /// the cursor takes and what a shelf prices for it cannot disagree.
        /// </summary>
        public readonly uint PickUpAmount(bool half) => half && 2u <= Amount ? Amount - Amount / 2u : Amount;

        /// <summary>Tries to add to the amount (within stacking limit).</summary>
        /// <returns>The amount that was added</returns>
        public uint IncreaseAmount(uint amountToAdd, IItemCatalog catalog)
        {
            if (0 == amountToAdd)
                return 0;

            var added = Math.Min(SpaceLeft(catalog), amountToAdd);
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
