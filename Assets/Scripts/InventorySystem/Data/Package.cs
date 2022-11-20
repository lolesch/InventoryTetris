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
        /// <summary>
        /// The package contains an amount of items and can be stored inside containers
        /// </summary>
        /// <param name="item"></param>
        /// <param name="amount"></param>
        public Package(AbstractItemObject item, uint amount = 1)
        {
            Item = item;
            Amount = amount;

            if (item != null && (uint)item.StackLimit < amount)
                Debug.LogWarning($"The Package you constructed contains more items than the item's stacking limit!");
        }

        /// <summary>The package's item.</summary>
        [field: SerializeField] public AbstractItemObject Item { get; private set; }

        /// <summary>The amount of items inside this package.</summary>
        [field: SerializeField] internal uint Amount { get; private set; }

        [SerializeField] internal uint SpaceLeft => (uint)Item.StackLimit - Amount;

        /// <summary>
        /// Tries to add to the amount (within stacking limit).
        /// </summary>
        /// <returns>The amount that was added</returns>
        public uint IncreaseAmount(uint amountToAdd)
        {
            if (null == Item || 0 == amountToAdd)
                return 0;

            amountToAdd = Math.Min(SpaceLeft, amountToAdd);
            Amount += amountToAdd;

            return amountToAdd;
        }

        /// <summary>
        /// Tries to remove the amount from the current stack
        /// </summary>
        /// <returns>The amount that was removed</returns>
        public uint ReduceAmount(uint amountToRemove)
        {
            if (null == Item || 0 == amountToRemove)
                return 0;

            amountToRemove = Math.Min(Amount, amountToRemove);
            Amount -= amountToRemove;

            return amountToRemove;
        }
    }
}
