using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [System.Serializable]
    public abstract class AbstractItem : ScriptableObject
    {
        [SerializeField] private Vector2Int dimensions = Vector2Int.one;
        public Vector2Int Dimensions => dimensions;

        //public int Slots => Dimensions.x * Dimensions.y;

        [SerializeField] private uint stackLimit = 1u;
        public uint StackLimit => stackLimit;

        [SerializeField] protected internal Sprite Icon;

        [SerializeField] protected internal List<ItemStat> stats = new List<ItemStat>();
        public List<ItemStat> Stats => stats;

        protected internal void SetStackLimit(uint newLimit) => stackLimit = newLimit;
    }
}