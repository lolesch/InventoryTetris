using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [System.Serializable]
    public abstract class AbstractItemObject : ScriptableObject
    {
        [field: SerializeField, HideInInspector] public Sprite Icon { get; private set; }
        [field: SerializeField] public Vector2Int Dimensions { get; private set; } = Vector2Int.one;
        [field: SerializeField] public ItemStackType StackLimit { get; private set; } = ItemStackType.Single;
        // TODO: sort stats as they appear on the character
        //[field: SerializeField]
        //private List<ItemStat> Stats => stats.OrderBy(x => x.Stat).ToList();

        [field: SerializeField] public List<ItemStat> Stats { get; private set; } = new();
        // TODO: handle overTime effects => Stats != Effects => see ARPG_Combat for DoT_effects

        internal void SetStackLimit(ItemStackType newLimit) => StackLimit = newLimit;

        //public abstract void Use();
    }
}