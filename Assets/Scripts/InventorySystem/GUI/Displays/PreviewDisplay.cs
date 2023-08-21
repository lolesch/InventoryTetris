
using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    // TODO: inherit AbstractDisplay
    public class PreviewDisplay : MonoBehaviour
    {
        public RectTransform ItemDisplay;
        [SerializeField] private Image icon;
        [SerializeField] private Image frame;
        [SerializeField] private List<Image> horizontalLines;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI itemName;
        [SerializeField] private TextMeshProUGUI itemType;
        [SerializeField] private TextMeshProUGUI amount;
        [SerializeField] private TextMeshProUGUI itemStatPrefab;

        private Canvas rootCanvas;
        public bool IsPreviewing => ItemDisplay.gameObject.activeSelf;

        private void Awake() => ItemDisplay.gameObject.SetActive(false);

        public void SetDisplay(Package package, Package compareTo)
        {
            if (package.Item == null || package.Amount < 1)
            {
                ItemDisplay.gameObject.SetActive(false);
                return;
            }

            var rarityColor = AbstractItem.GetRarityColor(package.Item.Rarity);

            if (itemName)
                itemName.text = package.Item.ToString().Colored(rarityColor);

            if (itemType)
                itemType.text = package.Item is EquipmentItem ? (package.Item as EquipmentItem).EquipmentType.ToString().Colored(rarityColor) : string.Empty;

            if (icon)
                icon.sprite = package.Item.Icon;

            if (amount)
                amount.text = 1 < package.Amount && package.Item is ConsumableItem ? $"{package.Amount}/{(int)(package.Item as ConsumableItem).StackLimit}" : string.Empty;

            if (frame)
                frame.color = rarityColor;

            if (horizontalLines != null && 0 < horizontalLines.Count)
                for (var i = 0; i < horizontalLines.Count; i++)
                    horizontalLines[i].color = rarityColor;

            if (background)
                background.color = rarityColor * Color.gray * Color.gray;

            // TODO: make this poolable
            if (itemStatPrefab)
            {
                for (var i = itemStatPrefab.transform.parent.childCount; i-- > 1;)
                {
                    Destroy(itemStatPrefab.transform.parent.GetChild(i).gameObject);
                    DestroyImmediate(itemStatPrefab.transform.parent.GetChild(i).gameObject);
                }

                var stats = package.Item.Affixes;

                for (var i = 0; i < stats.Count; i++)
                {
                    var itemStat = Instantiate(itemStatPrefab, itemStatPrefab.transform.parent);

                    var comparison = CompareStatValues(stats[i], out var difference);

                    //var difference = comparison == 0 ? 0 : stats[i].Modifier.Value - other; // should compare character stats with 

                    var color = comparison == 0 ? Color.white : (comparison < 0 ? Color.red : Color.green);

                    var differenceString = stats[i].Modifier.Type switch
                    {
                        StatModifierType.Override => $"{difference:+ #.###;- #.###;#.###}",
                        StatModifierType.FlatAdd => $"{difference:+ #.###;- #.###;#.###}",
                        StatModifierType.PercentAdd => $"{difference:+ #.###;- #.###;#.###}%",
                        StatModifierType.PercentMult => $"{difference:+ #.###;- #.###;#.###}%",

                        _ => $"?? {difference:+ #.###;- #.###;#.###}",
                    };

                    var statName = stats[i].Stat.SplitCamelCase();

                    if (statName.Contains("Percent"))
                        statName = statName.Replace(" Percent", "");

                    // TODO: review the references
                    itemStat.text = $"{stats[i].Modifier} {statName} {stats[i].Modifier.Range} {differenceString.Colored(color)}";

                    itemStat.gameObject.SetActive(true);
                }

                int CompareStatValues(PlayerStatModifier stat, out float difference)
                {
                    var other = 0f;
                    difference = stat.Modifier.Value;

                    //if (stat.Modifier.Type == StatModifierType.Override) // => compare to total
                    //    other = Character.Instance.GetStatValue(stat.Stat);
                    //else 
                    if (compareTo.Item != null)
                        for (var i = 0; i < compareTo.Item.Affixes.Count; i++) // foreach stat of the other item
                            if (compareTo.Item.Affixes[i].Stat == stat.Stat) // find a corresponding stat
                                                                             //        if (compareTo.Item.Affixes[i].Modifier.Type == stat.Modifier.Type) // find a corresponding mod type
                            {
                                other = compareTo.Item.Affixes[i].Modifier.Value;
                                difference = ItemProvider.Instance.LocalPlayer.CompareStatModifiers(stat, compareTo.Item.Affixes[i].Modifier);
                            }

                    return stat.Modifier.Value.CompareTo(other);
                }
            }

            /*  itemValue / sellValue
             *  durability?
             *  flavor text?
             */

            ItemDisplay.gameObject.SetActive(true);
        }
    }
}
