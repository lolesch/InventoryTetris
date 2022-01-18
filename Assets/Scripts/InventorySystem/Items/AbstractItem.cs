using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [System.Serializable]
    public abstract class AbstractItem : ScriptableObject
    {
        [SerializeField] protected internal Vector2Int dimensions = Vector2Int.one;
        public Vector2Int Dimensions => dimensions;
        public int Slots => Dimensions.x * Dimensions.y;

        [SerializeField] protected internal uint stackLimit = 1u;
        public uint Stacklimit => stackLimit;

        [SerializeField] protected internal Sprite Icon;

        [SerializeField] protected internal List<ItemStat> stats = new List<ItemStat>();

        protected internal void SetStackLimit(uint newLimit) => stackLimit = newLimit;
    }
}