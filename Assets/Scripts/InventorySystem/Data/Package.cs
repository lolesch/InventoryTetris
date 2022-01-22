using System;
using System.Runtime.CompilerServices;
using TeppichsTools.Logging;
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
        public Package(AbstractItem item, uint amount = 1)
        {
            Item = item;
            Amount = amount;
            if (item.StackLimit < amount)
                EditorDebug.LogWarning($"{nameof(Package)} \t The Package you constructed contains more items than the item's stacking limit!");

            randomColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
        }

        // remove me later
        public Color randomColor;

        /// <summary>
        /// The package's item
        /// </summary>
        [SerializeField] internal AbstractItem Item;

        /// <summary>
        /// The amount of items inside this package
        /// </summary>
        [SerializeField] internal uint Amount;

        [SerializeField] internal uint SpaceLeft => Item.StackLimit - Amount;

        /// <summary>
        /// Tries to add to the amount (within stacking limit).
        /// </summary>
        /// <param name="itemToAdd"></param>
        /// <param name="amountToAdd"></param>
        /// <returns>The amount that was added</returns>
        public uint IncreaseAmount(uint amountToAdd)
        {
            if (null == Item || 0 == amountToAdd)
                return 0;

            var added = Math.Min(Item.StackLimit - Amount, amountToAdd);
            Amount += added;

            return added;
        }

        /// <summary>
        /// Tries to remove the amount from the current stack
        /// </summary>
        /// <param name="amountToRemove"></param>
        /// <returns>The amount that was removed</returns>
        public uint ReduceAmount(uint amountToRemove)
        {
            if (null == Item || 0 == amountToRemove)
                return 0;

            var removed = Math.Min(Amount, amountToRemove);
            Amount -= removed;

            return removed;
        }
    }
}
