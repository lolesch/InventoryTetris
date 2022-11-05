using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [System.Serializable]
    public abstract class AbstractItem : ScriptableObject
    {
        [SerializeField] protected internal Sprite Icon;

        [SerializeField] private Vector2Int dimensions = Vector2Int.one;
        public Vector2Int Dimensions => dimensions;

        //public int Slots => Dimensions.x * Dimensions.y;

        // convert to enum to use stackLimit chategories instead of individual numbers
        [SerializeField] private ItemStackType stackLimit = ItemStackType.Single;
        public ItemStackType StackLimit => stackLimit;

        [SerializeField] protected internal List<ItemStat> stats = new();
        public List<ItemStat> Stats => stats;

        protected internal void SetStackLimit(ItemStackType newLimit) => stackLimit = newLimit;
    }
}