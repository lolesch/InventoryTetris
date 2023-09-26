using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [System.Serializable]
    public abstract class AbstractItemObject : ScriptableObject
    {
        public abstract AbstractItem GetItem();

        private void OnValidate()
        {
            var item = GetItem();
            for (var i = 0; i < item.Affixes.Count; i++)
            {
                var value = item.Affixes[i].Modifier.Value;
                var rangeX = item.Affixes[i].Modifier.Range.x;
                var rangeY = item.Affixes[i].Modifier.Range.y;
                var type = item.Affixes[i].Modifier.Type;

                var statMod = new StatModifier();

                if (value < rangeX)
                    statMod = new StatModifier(new Vector2Int(Mathf.FloorToInt(value), rangeY), value, type);
                else if (value > rangeY)
                    statMod = new StatModifier(new Vector2Int(rangeX, Mathf.CeilToInt(value)), value, type);

                item.Affixes[i] = new CharacterStatModifier(item.Affixes[i].Stat, statMod);
            }
        }
    }
}