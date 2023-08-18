using System;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Data
{
    [Serializable]
    public struct Package
    {
        /// <summary>The package contains an amount of items and can be stored inside containers</summary>
        public Package(AbstractItem item, uint amount = 1)
        {
            Item = item;
            Amount = amount;

            if (item != null && (uint)item.StackLimit < amount)
                Debug.LogWarning($"The Package you constructed contains more items than the item's stacking limit!");
        }

        [field: SerializeField] public AbstractItem Item { get; private set; }

        [field: SerializeField] public uint Amount { get; private set; }

        [SerializeField] public uint SpaceLeft => (uint)Item.StackLimit - Amount;

        /// <summary>Tries to add to the amount (within stacking limit).</summary>
        /// <returns>The amount that was added</returns>
        public uint IncreaseAmount(uint amountToAdd)
        {
            if (0 == amountToAdd)
                return 0;

            amountToAdd = Math.Min(SpaceLeft, amountToAdd);
            Amount += amountToAdd;

            return amountToAdd;
        }

        /// <summary>Tries to remove the amount from the current stack</summary>
        /// <returns>The amount that was removed</returns>
        public uint ReduceAmount(uint amountToRemove)
        {
            if (0 == amountToRemove)
                return 0;

            amountToRemove = Math.Min(Amount, amountToRemove);
            Amount -= amountToRemove;

            return amountToRemove;
        }
    }
}
