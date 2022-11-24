using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [System.Serializable]
    public abstract class AbstractItemObject : ScriptableObject
    {
        [field: SerializeField] public Sprite Icon { get; set; }
        [field: SerializeField] public Vector2Int Dimensions { get; private set; } = Vector2Int.one;
        [field: SerializeField] public ItemStackType StackLimit { get; private set; } = ItemStackType.Single;
        [field: SerializeField] public ItemRarity Rarity { get; private set; } = ItemRarity.Common;

        [SerializeField, ReadOnly] private Color rarityColor;

        // TODO: sort stats as they appear on the character
        //[field: SerializeField]
        //private List<ItemStat> Stats => stats.OrderBy(x => x.Stat).ToList();

        [field: SerializeField] public List<ItemStat> Stats { get; private set; } = new();
        // TODO: handle overTime effects => Stats != Effects => see ARPG_Combat for DoT_effects

        private void OnValidate() => rarityColor = GetRarityColor(this);

        public static Color GetRarityColor(AbstractItemObject item) => item.Rarity switch
        {
            ItemRarity.NONE => Color.clear,
            ItemRarity.Crafted => new Color(1, 0.35f, 0, 1), // orange
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => Color.gray,
            ItemRarity.Magic => Color.blue,
            ItemRarity.Rare => Color.yellow,
            ItemRarity.Set => Color.green,
            ItemRarity.Unique => new Color(0.4f, 0, 1, 1), // purple
            _ => Color.clear,
        };
    }
}