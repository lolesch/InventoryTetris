using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;
using static ToolSmiths.InventorySystem.GUI.Displays.CharacterStatModifierDisplay;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class CharacterStatModifierDisplay : MonoBehaviour, IDisplay<CharacterStatModifierData>
    {
        public struct CharacterStatModifierData
        {
            private CharacterStatModifier statMod;
            public string displayText;
            public float displayFontSize;
            public Sprite icon;

            public CharacterStatModifierData(CharacterStatModifier characterStatModifier)
            {
                statMod = characterStatModifier;
                icon = ItemProvider.Instance.ItemTypeData.GetStatIcon(statMod.Stat);
                displayText = $"{statMod.Modifier} {statMod.Modifier.Range.ToString().Colored(Color.gray)}";
                displayFontSize = statMod.Modifier.Value.Map(statMod.Modifier.Range, 18, 24);
            }

            public CharacterStatModifierData(CharacterStatModifier characterStatModifier, Package compareTo)
            {
                statMod = characterStatModifier;

                var comparison = CompareStatValues(statMod, compareTo, out var difference);
                var comparisonColor = comparison == 0 ? Color.white : (comparison < 0 ? Color.red : Color.green);

                var differenceString = statMod.Modifier.Type switch
                {
                    StatModifierType.Overwrite => $"={difference:+ #.###;- #.###;#.###}",
                    StatModifierType.FlatAdd => $"{difference:+ #.###;- #.###;#.###}",
                    StatModifierType.PercentAdd => $"{difference:+ #.###;- #.###;#.###}%",
                    StatModifierType.PercentMult => $"*{difference:+ #.###;- #.###;#.###}%",

                    _ => $"?? {difference:+ #.###;- #.###;#.###}",
                };

                icon = ItemProvider.Instance.ItemTypeData.GetStatIcon(statMod.Stat);
                displayText = $"{statMod.Modifier} {statMod.Modifier.Range.ToString().Colored(Color.gray)} {differenceString.Colored(comparisonColor)}";
                displayFontSize = statMod.Modifier.Value.Map(statMod.Modifier.Range, 18, 24);

                static int CompareStatValues(CharacterStatModifier stat, Package compareTo, out float difference)
                {
                    difference = 0;
                    var other = 0f;

                    //if (stat.Modifier.Type == StatModifierType.Override) // => compare to total
                    //    other = Character.Instance.GetStatValue(stat.Stat);
                    //else 
                    if (compareTo.IsValid)
                        for (var i = 0; i < compareTo.Item.Affixes.Count; i++)   // foreach stat of the other item
                            if (compareTo.Item.Affixes[i].Stat == stat.Stat)     // find a corresponding stat
                                                                                 // if (compareTo.Item.Affixes[i].Modifier.Type == stat.Modifier.Type) // find a corresponding mod type
                            {
                                other = compareTo.Item.Affixes[i].Modifier.Value;
                                difference = CharacterProvider.Instance.Player.CompareStatModifiers(stat, compareTo.Item.Affixes[i].Modifier);
                            }

                    return stat.Modifier.Value.CompareTo(other);
                }
            }
        }

        [SerializeField] protected Image icon;

        [SerializeField] protected TextMeshProUGUI text;

        public void RefreshDisplay(CharacterStatModifierData newData)
        {
            icon.sprite = newData.icon;
            text.text = newData.displayText;
            text.fontSize = newData.displayFontSize;
        }
    }
}
