
using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Extensions;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Displays
{
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
            {
                itemName.text = package.Item.ToString(); // UIExtensions.Colored
                itemName.color = rarityColor;
            }

            if (itemType)
            {
                itemType.text = package.Item is EquipmentItem ? (package.Item as EquipmentItem).EquipmentType.ToString() : string.Empty; // UIExtensions.Colored
                itemType.color = rarityColor;
            }

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

                    var comparison = CompareStatValues(stats[i]);

                    var color = comparison == 0 ? Color.white : (comparison < 0 ? Color.red : Color.green);

                    var comparisonString = stats[i].Modifier.Type switch
                    {
                        StatModifierType.Override => $"== {stats[i].Modifier.Value:+ #.###;- #.###;#.###}",
                        StatModifierType.FlatAdd => $"{stats[i].Modifier.Value:+ #.###;- #.###;#.###}",
                        StatModifierType.PercentAdd => $"{stats[i].Modifier.Value:+ #.###;- #.###;#.###} %",
                        StatModifierType.PercentMult => $"* {stats[i].Modifier.Value:+ #.###;- #.###;#.###} %",
                        _ => $"{stats[i].Modifier.Value}", // get human readable string
                    };

                    itemStat.text = $"{Colored(comparisonString, color)} {stats[i].Stat.SplitCamelCase()}";

                    itemStat.gameObject.SetActive(true);

                    string Colored(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
                }

                int CompareStatValues(PlayerStatModifier stat)
                {
                    var other = 0f;

                    if (stat.Modifier.Type == StatModifierType.Override) // => compare to total
                        other = Character.Instance.GetStatValue(stat.Stat);
                    else if (compareTo.Item != null)
                        for (var i = 0; i < compareTo.Item.Affixes.Count; i++) // foreach stat of the other item
                            if (compareTo.Item.Affixes[i].Stat == stat.Stat) // find a corresponding stat
                                other = compareTo.Item.Affixes[i].Modifier.Value;

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
